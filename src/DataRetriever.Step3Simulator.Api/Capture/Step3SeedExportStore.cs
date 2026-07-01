using System.Text.Json;
using DataRetriever.Application.Step3Load.Models;
using Microsoft.Extensions.Options;

namespace DataRetriever.Step3Simulator.Api.Capture;

public sealed class Step3SeedExportStore(
    IOptions<Step3CaptureOptions> options,
    ILogger<Step3SeedExportStore> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Step3CaptureOptions _options = options.Value;

    public async Task WriteAsync(CapturedHttpResponse response, CancellationToken cancellationToken)
    {
        if (!_options.ExportSeedData)
        {
            return;
        }

        if (response.StatusCode is < 200 or >= 300)
        {
            logger.LogInformation("Skipping Step 3 seed export because captured response status was {StatusCode}.", response.StatusCode);
            return;
        }

        Step3ResponseDto capturedResponse = DeserializeCapturedResponse(response.Body);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            Step3ResponseDto existingResponse = await ReadExistingAsync(cancellationToken);
            Step3ResponseDto mergedResponse = Merge(existingResponse, capturedResponse);
            await WriteMergedAsync(mergedResponse, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static Step3ResponseDto DeserializeCapturedResponse(byte[] body)
    {
        try
        {
            return JsonSerializer.Deserialize<Step3ResponseDto>(body, JsonOptions)
                ?? throw new InvalidOperationException("Captured Step 3 response body was empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Captured Step 3 response body could not be exported as seed data because it does not match the Step 3 response contract.",
                exception);
        }
    }

    private async Task<Step3ResponseDto> ReadExistingAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_options.SeedExportPath))
        {
            return new Step3ResponseDto([]);
        }

        await using FileStream stream = File.OpenRead(_options.SeedExportPath);
        return await JsonSerializer.DeserializeAsync<Step3ResponseDto>(stream, JsonOptions, cancellationToken)
            ?? new Step3ResponseDto([]);
    }

    private async Task WriteMergedAsync(Step3ResponseDto response, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_options.SeedExportPath)!);

        string tempPath = $"{_options.SeedExportPath}.{Guid.NewGuid():N}.tmp";
        await using (FileStream stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, response, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, _options.SeedExportPath, overwrite: true);
    }

    private static Step3ResponseDto Merge(Step3ResponseDto existingResponse, Step3ResponseDto capturedResponse)
    {
        List<Step3ResponseItemDto> mergedItems = [.. existingResponse.Items];

        foreach (Step3ResponseItemDto capturedItem in capturedResponse.Items)
        {
            if (string.IsNullOrWhiteSpace(capturedItem.ExternalId2))
            {
                mergedItems.Add(capturedItem);
                continue;
            }

            int existingIndex = mergedItems.FindIndex(item => string.Equals(
                item.ExternalId2,
                capturedItem.ExternalId2,
                StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                mergedItems[existingIndex] = capturedItem;
            }
            else
            {
                mergedItems.Add(capturedItem);
            }
        }

        return new Step3ResponseDto(mergedItems);
    }
}
