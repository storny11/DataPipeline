// Rendering caps for the full email template. The RunReport snapshot always carries every
// issue and row; only the rendered email truncates, so an oversized run still delivers a
// readable email instead of one that breaks SMTP size limits or the reader's client.
namespace RunReporting;

internal static class ReportRenderLimits
{
    /// <summary>Maximum rendered rows per issues section and per result table; section
    /// headings keep the true totals and a trailing note states how many rows were omitted.</summary>
    public const int MaxRows = 30;
}
