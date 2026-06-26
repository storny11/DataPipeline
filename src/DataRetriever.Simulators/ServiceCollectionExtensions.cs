using DataRetriever.Application.Step1Load;
using DataRetriever.Application.Step2Load;
using DataRetriever.Application.Step3Load;
using DataRetriever.Application.Step4Persist;
using DataRetriever.Monitoring;
using DataRetriever.Reporting;
using DataRetriever.Simulators.Reporting;
using DataRetriever.Simulators.Step1Load;
using DataRetriever.Simulators.Step2Load;
using DataRetriever.Simulators.Step3Load;
using DataRetriever.Simulators.Step4Persist;
using Microsoft.Extensions.DependencyInjection;

namespace DataRetriever.Simulators;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataRetrieverSimulators(this IServiceCollection services)
    {
        services.AddSingleton<SimulatorSeedData>();
        services.AddScoped<IStep1SourceClient, Step1SourceSimulator>();
        services.AddScoped<IStep2SourceClient, Step2SourceSimulator>();
        services.AddScoped<IStep3SourceClient, Step3SourceSimulator>();
        services.AddScoped<IStep4SinkClient, Step4SinkSimulator>();
        services.AddSingleton<IRunReportPublisher, SimulatedEmailRunReportPublisher>();
        return services;
    }
}
