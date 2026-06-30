using System.Text.Json;
using Microsoft.Extensions.Options;

namespace DataRetriever.Step3Simulator.Api.Capture;

public sealed class CaptureStore(IOptions<Step3CaptureOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly Step3CaptureOptions _options = options.Value;

    public async Task<CapturedInteraction?> TryReadAsync(string key, CancellationToken cancellationToken)
    {
        string path = GetCapturePath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        await using FileStream stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<CapturedInteraction>(stream, JsonOptions, cancellationToken);
    }

    public async Task WriteAsync(string key, CapturedInteraction interaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        string path = GetCapturePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await using (FileStream stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, interaction, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    private string GetCapturePath(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return Path.Combine(_options.CaptureDirectory, $"{key}.json");
    }
}
