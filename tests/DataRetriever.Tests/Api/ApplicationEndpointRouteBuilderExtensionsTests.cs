using DataRetriever.Api;
using DataRetriever.Api.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataRetriever.Tests.Api;

public sealed class ApplicationEndpointRouteBuilderExtensionsTests
{
    [Fact]
    public async Task MapApplicationEndpoints_MapsEachApplicationRouteOnce()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Application:Name"] = "Test application",
            ["AdapterMode"] = "Simulator",
            ["EmailReport:Enabled"] = "false"
        });
        builder.Services.AddDataRetrieverApi(builder.Configuration);

        await using var app = builder.Build();
        app.MapApplicationEndpoints();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["/", "/api/data-retrieval/runs", "/api/data-retrieval/status", "/health"],
            routes);
    }
}
