using System.Text;
using DataRetriever.Step3Simulator.Api.Capture;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DataRetriever.Tests.Step3Simulator;

public sealed class Step3CaptureTests
{
    [Fact]
    public void Build_WithDifferentBodyBytes_ReturnsDifferentKeys()
    {
        CaptureKeyBuilder builder = new();

        string first = builder.Build("POST", "/api/step3", Encoding.UTF8.GetBytes("{\"ids\":[\"A\",\"B\"]}"));
        string second = builder.Build("POST", "/api/step3", Encoding.UTF8.GetBytes("{ \"ids\": [\"A\", \"B\"] }"));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Build_WithDifferentPathAndQuery_ReturnsDifferentKeys()
    {
        CaptureKeyBuilder builder = new();
        byte[] body = Encoding.UTF8.GetBytes("{\"ids\":[\"A\"]}");

        string first = builder.Build("POST", "/api/step3?currency=USD", body);
        string second = builder.Build("POST", "/api/step3?currency=GBP", body);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task CaptureStore_WriteThenRead_PreservesInteraction()
    {
        string directory = CreateTempDirectory();
        CaptureStore store = new(Options.Create(new Step3CaptureOptions { CaptureDirectory = directory }));
        CapturedInteraction interaction = new(
            DateTimeOffset.UtcNow,
            "POST",
            "/api/step3",
            "application/json",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"request\":true}")),
            StatusCodes.Status201Created,
            "application/json",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"response\":true}")));

        await store.WriteAsync("abc", interaction, CancellationToken.None);

        CapturedInteraction? read = await store.TryReadAsync("abc", CancellationToken.None);

        Assert.Equal(interaction, read);
    }

    [Fact]
    public async Task HandleAsync_ReplayMissingCapture_ReturnsNotFound()
    {
        string directory = CreateTempDirectory();
        Step3CaptureProxy proxy = CreateProxy(directory, Step3CaptureMode.Replay, new StubUpstreamStep3Client());
        DefaultHttpContext context = CreateContext("/api/step3", "{\"request\":true}");

        await proxy.HandleAsync(context, CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
        Assert.Contains("No Step 3 capture found", ReadResponseBody(context));
    }

    [Fact]
    public async Task HandleAsync_ReplayExistingCapture_ReturnsCapturedResponse()
    {
        string directory = CreateTempDirectory();
        CaptureKeyBuilder keyBuilder = new();
        string requestBody = "{\"request\":true}";
        string key = keyBuilder.Build("POST", "/api/step3", Encoding.UTF8.GetBytes(requestBody));
        CaptureStore store = new(Options.Create(new Step3CaptureOptions { CaptureDirectory = directory }));
        CapturedInteraction interaction = new(
            DateTimeOffset.UtcNow,
            "POST",
            "/api/step3",
            "application/json",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(requestBody)),
            StatusCodes.Status202Accepted,
            "application/json",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"captured\":true}")));
        await store.WriteAsync(key, interaction, CancellationToken.None);

        Step3CaptureProxy proxy = CreateProxy(directory, Step3CaptureMode.Replay, new StubUpstreamStep3Client(), keyBuilder);
        DefaultHttpContext context = CreateContext("/api/step3", requestBody);

        await proxy.HandleAsync(context, CancellationToken.None);

        Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
        Assert.Equal("{\"captured\":true}", ReadResponseBody(context));
    }

    [Fact]
    public async Task HandleAsync_CaptureMode_CallsUpstreamAndWritesCapture()
    {
        string directory = CreateTempDirectory();
        StubUpstreamStep3Client upstreamClient = new(StatusCodes.Status200OK, "application/json", "{\"upstream\":true}");
        Step3CaptureProxy proxy = CreateProxy(directory, Step3CaptureMode.Capture, upstreamClient);
        DefaultHttpContext context = CreateContext("/api/step3?x=1", "{\"request\":true}");

        await proxy.HandleAsync(context, CancellationToken.None);

        Assert.Equal(1, upstreamClient.CallCount);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("{\"upstream\":true}", ReadResponseBody(context));
        Assert.Single(Directory.GetFiles(directory, "*.json"));
    }

    private static Step3CaptureProxy CreateProxy(
        string captureDirectory,
        Step3CaptureMode mode,
        IUpstreamStep3Client upstreamClient,
        CaptureKeyBuilder? keyBuilder = null)
    {
        Step3CaptureOptions options = new()
        {
            Mode = mode,
            CaptureDirectory = captureDirectory,
            UpstreamBaseUrl = "https://example.test"
        };

        return new Step3CaptureProxy(
            keyBuilder ?? new CaptureKeyBuilder(),
            new CaptureStore(Options.Create(options)),
            upstreamClient,
            Options.Create(options),
            NullLogger<Step3CaptureProxy>.Instance);
    }

    private static DefaultHttpContext CreateContext(string pathAndQuery, string body)
    {
        DefaultHttpContext context = new();
        context.Request.Method = "POST";
        context.Request.ContentType = "application/json";
        context.Request.Path = pathAndQuery.Split('?')[0];
        if (pathAndQuery.Contains('?'))
        {
            context.Request.QueryString = new QueryString(pathAndQuery[pathAndQuery.IndexOf('?')..]);
        }

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Response.Body = new MemoryStream();

        return context;
    }

    private static string ReadResponseBody(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using StreamReader reader = new(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"step3-captures-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class StubUpstreamStep3Client(
        int statusCode = StatusCodes.Status200OK,
        string? contentType = "application/json",
        string body = "{}") : IUpstreamStep3Client
    {
        public int CallCount { get; private set; }

        public Task<CapturedHttpResponse> SendAsync(
            HttpContext context,
            byte[] requestBody,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new CapturedHttpResponse(
                statusCode,
                contentType,
                Encoding.UTF8.GetBytes(body)));
        }
    }
}
