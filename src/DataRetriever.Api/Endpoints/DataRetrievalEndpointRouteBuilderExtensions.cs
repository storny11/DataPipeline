// Maps the data retrieval HTTP endpoints in one place to keep Program.cs small.
using DataRetriever.Api.Contracts;

namespace DataRetriever.Api.Endpoints;

public static class DataRetrievalEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapDataRetrievalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/data-retrieval");

        group.MapPost("/runs", RunDataRetrievalEndpoint.HandleAsync);
        group.MapGet("/status", GetDataRetrievalStatusEndpoint.HandleAsync);

        return endpoints;
    }
}
