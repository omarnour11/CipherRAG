using Microsoft.AspNetCore.Routing;

namespace CipherRAG.Endpoints;

public static class AppEndpoints
{
    public static void MapAppEndpoints(this IEndpointRouteBuilder app)
    {   
        app.MapHealthEndpoints();
        app.MapDocumentEndpoints();
        app.MapRagEndpoints();
        app.MapUserEndpoints();
        app.MapRoleEndpoints();
        app.MapAuditLogEndpoints();
        app.MapAuthEndpoints();
        app.MapPolicyEndpoints();
    }
}