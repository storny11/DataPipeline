using DataRetriever.Api;
using DataRetriever.Api.Composition;
using DataRetriever.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataRetrieverApi(builder.Configuration);

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "DataRetriever" }));
app.MapDataRetrievalEndpoints();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
