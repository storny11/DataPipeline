using DataRetriever.Application.Runs;
using DataRetriever.Application.Step1Load;
using DataRetriever.Application.Step1Load.Models;
using DataRetriever.Application.Step2Load;
using DataRetriever.Application.Step2Load.Models;
using DataRetriever.Application.Step3Load;
using DataRetriever.Application.Step3Load.Models;
using DataRetriever.Application.Step4Persist;
using DataRetriever.Application.Step4Persist.Models;
using DataRetriever.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace DataRetriever.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataRetrieverApplication(this IServiceCollection services)
    {
        services.AddSingleton<SingleRunGuard>();
        services.AddScoped<RunInstrumentationWriter>();
        services.AddScoped<StepRunner>();
        services.AddScoped<DataRetrievalReportSummaryBuilder>();
        services.AddScoped<Step4ReportTableBuilder>();
        services.AddScoped<RunReportFinalizer>();
        services.AddScoped<DataRetrievalOrchestrator>();

        services.AddScoped<Step1Mapper>();
        services.AddScoped<Step1Validator>();
        services.AddScoped<IStep<Step1Input, Step1Output>, Step1Loader>();

        services.AddScoped<Step2ResponseMapper>();
        services.AddScoped<Step2Selector>();
        services.AddScoped<IStep<Step1Output, Step2Output>, Step2Loader>();

        services.AddScoped<ExternalId2Normalizer>();
        services.AddScoped<Step3RequestMapper>();
        services.AddScoped<Step3ResponseMapper>();
        services.AddScoped<Step3ResponseValidator>();
        services.AddScoped<IStep<Step2Output, Step3Output>, Step3Loader>();

        services.AddScoped<Step4RequestMapper>();
        services.AddScoped<IStep<Step3Output, Step4Output>, Step4Persister>();

        return services;
    }
}
