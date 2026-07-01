using System.ComponentModel.DataAnnotations;

namespace DataRetriever.Step3Simulator.Api.Capture;

public sealed class Step3CaptureOptions
{
    public const string SectionName = "Step3Capture";

    public Step3CaptureMode Mode { get; init; } = Step3CaptureMode.Replay;

    [Required]
    public string CaptureDirectory { get; init; } = "captures/step3";

    public bool ExportSeedData { get; init; } = true;

    [Required]
    public string SeedExportPath { get; init; } = "captures/seed/step3.json";

    public string? UpstreamBaseUrl { get; init; }
}
