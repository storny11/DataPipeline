// Provides safe application identity used by diagnostics and the root endpoint.
using System.ComponentModel.DataAnnotations;

namespace DataRetriever.Api.Configuration;

public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; init; } = "";
}
