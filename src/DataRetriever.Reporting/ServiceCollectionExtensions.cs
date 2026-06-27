// Registers reporting builders and email formatters.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataRetriever.Reporting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataRetrieverReporting(this IServiceCollection services)
    {
        return services.AddDataRetrieverReporting(new ConfigurationBuilder().Build());
    }

    public static IServiceCollection AddDataRetrieverReporting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<RunReportBuilder>();
        services.Configure<RunReportEmailOptions>(configuration.GetSection(RunReportEmailOptions.SectionName));
        services.AddSingleton<IRunReportEmailFormatter, RazorRunReportEmailFormatter>();
        return services;
    }
}
