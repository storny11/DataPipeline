// Points the Razor formatter at a typed custom template component; register via UseRunReportTemplate.
namespace RunReporting;

public sealed record RunReportTemplate(Type ComponentType);
