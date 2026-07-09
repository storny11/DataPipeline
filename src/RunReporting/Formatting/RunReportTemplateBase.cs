// Typed contract for custom Razor report templates.
using Microsoft.AspNetCore.Components;

namespace RunReporting;

public abstract class RunReportTemplateBase : ComponentBase
{
    [Parameter, EditorRequired]
    public RunReport Report { get; set; } = null!;

    [Parameter, EditorRequired]
    public RunReportingOptions Options { get; set; } = null!;
}
