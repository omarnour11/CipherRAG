# Multi-stage build for CipherRAG API (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for optimal Docker layer caching
COPY ["CipherRAG.sln", "./"]
COPY ["CipherRAG.Domain/CipherRAG.Domain.csproj", "CipherRAG.Domain/"]
COPY ["CipherRAG.Application/CipherRAG.Application.csproj", "CipherRAG.Application/"]
COPY ["CipherRAG.Infrastructure/CipherRAG.Infrastructure.csproj", "CipherRAG.Infrastructure/"]
COPY ["CipherRAG.API/CipherRAG.API.csproj", "CipherRAG.API/"]
COPY ["tests/CipherRAG.Domain.Tests/CipherRAG.Domain.Tests.csproj", "tests/CipherRAG.Domain.Tests/"]

RUN dotnet restore "CipherRAG.sln"

# Copy everything else and publish Release build
COPY . .
WORKDIR /src/CipherRAG.API
RUN dotnet publish "CipherRAG.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Render sets PORT dynamically (defaults to 8080 if not set)
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CipherRAG.API.dll"]

