using DataRetriever.Step3Simulator.Api.Capture;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<Step3CaptureOptions>()
    .Bind(builder.Configuration.GetSection(Step3CaptureOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<CaptureKeyBuilder>();
builder.Services.AddSingleton<CaptureStore>();
builder.Services.AddSingleton<Step3SeedExportStore>();
builder.Services.AddScoped<Step3CaptureProxy>();
builder.Services.AddHttpClient<IUpstreamStep3Client, HttpUpstreamStep3Client>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapMethods(
    "/{**path}",
    ["GET", "POST", "PUT", "PATCH", "DELETE"],
    async (HttpContext context, Step3CaptureProxy proxy, CancellationToken cancellationToken) =>
    {
        await proxy.HandleAsync(context, cancellationToken);
    });

app.Run();

public partial class Program;
