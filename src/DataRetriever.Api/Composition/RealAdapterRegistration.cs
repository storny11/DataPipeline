namespace DataRetriever.Api.Composition;

public static class RealAdapterRegistration
{
    public static IServiceCollection AddRealAdapters(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        throw new NotSupportedException(
            "AdapterMode.Real is not available until the real source and sink adapters are implemented.");
    }
}
