using Microsoft.Extensions.DependencyInjection;

namespace DataRetriever.Reporting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataRetrieverReporting(this IServiceCollection services)
    {
        services.AddSingleton<RunReportBuilder>();
        services.AddSingleton<IRunReportEmailFormatter, RazorRunReportEmailFormatter>();
        return services;
    }
}
