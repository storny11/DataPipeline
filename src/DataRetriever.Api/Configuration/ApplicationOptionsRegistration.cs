// Registers normal runtime options once and forces validation before hosted work starts.
namespace DataRetriever.Api.Configuration;

public static class ApplicationOptionsRegistration
{
    public static IServiceCollection AddApplicationOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(ApplicationOptions.SectionName);

        services
            .AddOptions<ApplicationOptions>()
            .Bind(section)
            .Validate(
                _ => section.Exists(),
                $"Required configuration section '{ApplicationOptions.SectionName}' is missing.")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
