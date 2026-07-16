// Maps application HTTP endpoints in one place to keep Program.cs small.
using DataRetriever.Api.Configuration;
using DataRetriever.Api.Contracts;
using Microsoft.Extensions.Options;

namespace DataRetriever.Api.Endpoints;

public static class ApplicationEndpointRouteBuilderExtensions
{
    public static WebApplication MapApplicationEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(
            "/",
            (IOptions<ApplicationOptions> options) => Results.Ok(new { service = options.Value.Name }));
        app.MapDataRetrievalEndpoints();
        app.MapHealthChecks("/health");

        return app;
    }

    public static IEndpointRouteBuilder MapDataRetrievalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/data-retrieval");

        group.MapPost("/runs", RunDataRetrievalEndpoint.HandleAsync);
        group.MapGet("/status", GetDataRetrievalStatusEndpoint.HandleAsync);

        return endpoints;
    }
}
