// Controls optional email-layout behavior such as displaying statistics.
namespace DataRetriever.Reporting;

public sealed class RunReportEmailOptions
{
    public const string SectionName = "RunReportEmail";

    public bool DisplayStats { get; init; }
}
