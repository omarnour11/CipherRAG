using System.IO.Compression;
using System.Threading.RateLimiting;
using CipherRAG.Application.Interfaces;
using CipherRAG.Endpoints;
using CipherRAG.Infrastructure.Caching;
using CipherRAG.Infrastructure.Persistence;
using CipherRAG.Infrastructure.Persistence.Repositories;
using CipherRAG.Infrastructure.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Configure Database Connection (SQL Server)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// Register Repositories & UnitOfWork
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// HIPAA §164.312(d) — Password hashing service
builder.Services.AddSingleton<CipherRAG.Application.Interfaces.IPasswordHasher, CipherRAG.Infrastructure.Services.BcryptPasswordHasher>();

// Clean Architecture Memory Caching & Infrastructure Services
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

builder.Services.AddSingleton<IVectorEmbeddingService, VectorEmbeddingService>();
builder.Services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();
builder.Services.AddHttpClient<ILlmProvider, LlmProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient<IPresidioService, PresidioService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient<IGuardrailService, PromptGuardService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

// Add Swagger/OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token in the format: Bearer {token}"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
    });
});
builder.Services.AddCors(options =>
{
  options.AddDefaultPolicy(policy =>
  {
      var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
      var origins = configuredOrigins?.Where(origin => Uri.TryCreate(origin, UriKind.Absolute, out _)).ToArray()
          ?? ["https://localhost:7254"];
      policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod().AllowCredentials();
  });
   
});

builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration);

});

builder.Services.AddScoped<ITokenProvider,TokenProvider>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options=>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],

        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],

        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),

        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is missing"))),
        NameClaimType = ClaimTypes.NameIdentifier,
        RoleClaimType = ClaimTypes.Role
    };
});

builder.Services.AddTransient<IAppDbContext,AppDbContext>();

builder.Services.AddAuthorization(options=>
{
    options.AddPolicy("AdminPolicy", policy => policy.RequireRole("SystemAdmin","Admin"));
    options.AddPolicy("AuditViewPolicy", policy => policy.RequireRole("SystemAdmin","Admin","Auditor"));
    options.AddPolicy("DocumentManagePolicy", policy => policy.RequireRole("SystemAdmin","Admin","User","Doctor"));
});

builder.Services.AddOpenTelemetry()
.ConfigureResource(resource => resource.AddService("CipherRAGGateway")) 
                .WithTracing(tracing =>
                {
                    tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri("http://localhost:5341/ingest/otlp/v1/traces");
                
                        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    });
                })
                .WithMetrics(metrics =>
                {
                    metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
                    
                   metrics.AddPrometheusExporter();
                
                  
                });

builder.Services.AddResponseCompression(options =>
{
    
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();

    options.MimeTypes = new[]
    {
        "application/json",
        "text/plain",
        "text/html",
        "application/xml"
    };

});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.AddRateLimiter(options=>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddSlidingWindowLimiter ("DefaultPolicy", LimiterOptions =>
    {
        LimiterOptions.Window = TimeSpan.FromMinutes(1);
        LimiterOptions.PermitLimit = 10;
        LimiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        LimiterOptions.QueueLimit =10;

        LimiterOptions.SegmentsPerWindow = 6;
        LimiterOptions.AutoReplenishment = true;
    });
    options.AddFixedWindowLimiter("AuthPolicy", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.PermitLimit = 5;
        limiterOptions.QueueLimit = 0;
    });
    options.AddSlidingWindowLimiter("RagPolicy", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.PermitLimit = 20;
        limiterOptions.QueueLimit = 0;
        limiterOptions.SegmentsPerWindow = 6;
    });
});

builder.Services.AddExceptionHandler<CipherRAG.Infrastructure.Middleware.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
var app = builder.Build();

app.UseStatusCodePages();
app.UseExceptionHandler();


if(app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}


app.UseHttpsRedirection();
app.UseCors();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseResponseCompression();

// Enable Swagger UI in Development
if (app.Environment.IsDevelopment())
{   
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CipherRAG API v1");
        c.RoutePrefix = "swagger"; // Serve Swagger at http://localhost:port/swagger
    });
}

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        ctx.Context.Response.Headers.Append("Pragma", "no-cache");
        ctx.Context.Response.Headers.Append("Expires", "0");
    }
});


// Database changes and demo data are opt-in outside development.
if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();

    if (app.Configuration.GetValue<bool>("Database:SeedOnStartup"))
    {
        await DbInitializer.SeedAsync(context);
    }
}

// Map Minimal API Endpoints
app.MapAppEndpoints();
app.MapPrometheusScrapingEndpoint();

app.Run();

//make JWT 
// Response caching
//Rate Limitting
//
