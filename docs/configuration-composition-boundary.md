# Configuration and Composition Boundary

## Purpose

This pattern separates three operations that are often combined in one large startup helper:

1. build the configuration provider pipeline;
2. read values that must decide service composition;
3. bind and validate normal runtime options.

Keeping these operations separate makes provider precedence explicit, prevents duplicate binding, and ensures startup failures are logged by the bootstrap logger.

## The three configuration categories

### Provider inputs

Provider inputs are needed to construct `IConfiguration`. Examples include:

- a path to an optional instance settings file;
- credentials or an identifier needed to load a remote provider;
- compatibility aliases that must be converted to canonical keys;
- a generated absolute log-file path.

These belong in the provider phase. The provider phase must not return a broad runtime options object.

### Composition-driving values

Composition-driving values decide which implementations are registered. They must be read before the service provider exists.

Examples include an implementation mode or a choice between local and external adapters.

Read and validate each composition-driving value once after configuration and bootstrap logging are available. Pass the validated typed value into application registration. Registration must not read the same setting again.

### Runtime options

Runtime options are consumed after the host is composed. Their owning modules should bind them through `AddOptions<T>()`, validate them, and use `ValidateOnStart()`.

Do not materialize these options in `Program.cs`. Consumers should inject `IOptions<T>` unless genuine reload behavior requires `IOptionsMonitor<T>`.

## Recommended startup order

```csharp
var logFilePath = ApplicationLogPath.ResolveBootstrapFilePath(args);
ApplicationLogging.EnableBootstrapFile(bootstrapLogger, logFilePath);

var environmentName = ApplicationEnvironment.ReadRequired(args);
ApplicationLogPath.EnsureLaunchIsValid(args, environmentName);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
    EnvironmentName = environmentName
});

builder.AddApplicationConfiguration(args, logFilePath);
builder.ConfigureApplicationHost();

var mode = ApplicationModeConfiguration.ReadRequired(builder.Configuration);
builder.Services.AddApplication(builder.Configuration, mode);

var app = builder.Build();
app.MapApplicationEndpoints();
await app.RunAsync();
```

The phases are intentionally visible:

- `EnableBootstrapFile(...)` makes early failures durable and fails startup if the file cannot be opened;
- `CreateBuilder(...)` fixes the environment and installs the standard .NET providers;
- `AddApplicationConfiguration(args, logFilePath)` inserts real application-specific providers, restores final runtime override precedence, and protects the resolved log destination;
- `ConfigureApplicationHost()` validates the configured File sink, enables strict host policies, and registers final Serilog;
- `ReadRequired(...)` validates a composition input once;
- `AddApplication(...)` registers modules from typed composition inputs and `IConfiguration`;
- each module owns its normal options pipeline.

## Preserve the standard providers

`WebApplication.CreateBuilder(...)` already adds the normal providers, including:

- `appsettings.json`;
- `appsettings.{Environment}.json`;
- user secrets in Development when configured;
- environment variables;
- command-line arguments.

Do not clear and manually reconstruct all sources merely to insert an application provider. Clearing the collection also removes framework sources such as user secrets and host/application fallback providers.

There is one deliberate exception to the general no-duplication rule. When an external provider and `appsettings.local.json` must sit before environment variables and command-line arguments, append environment variables and command-line arguments once after those custom sources. Their initial copies make bootstrap selectors available; their final copies restore the documented runtime precedence.

If the application has no custom provider, omit the method entirely. In this example application, the method is meaningful because it adds the generated, validated absolute Serilog file path as a final in-memory override.

## Provider precedence

Write the intended provider order down before editing code. Later providers win.

For a service with custom providers, a reasonable conceptual order is:

1. standard base JSON;
2. environment-specific JSON;
3. remote or instance configuration;
4. optional local JSON;
5. environment variables;
6. command-line values;
7. generated safety-critical overrides whose destination must not be redirected.

The exact order is application-specific. A generated durable log path may intentionally be last so an arbitrary configured value cannot bypass path validation.

Do not append command-line configuration repeatedly after derived providers. When custom providers must sit before command-line values, append it once at the end of the normal provider pipeline and document why the initial bootstrap copy and final precedence copy both exist.

See [environment-configuration-layering.md](environment-configuration-layering.md) for the complete environment and local-file pattern.

## Compatibility mappings

Compatibility aliases should be an adaptation layer, not a second options system.

When aliases are required:

1. read the source aliases from the completed source configuration;
2. map them to canonical keys in one dictionary;
3. exclude absent values;
4. add one in-memory provider;
5. bind and validate only the canonical options path.

```csharp
var overrides = new Dictionary<string, string?>
{
    ["Application:Name"] = configuration["applicationName"],
    ["Application:Port"] = configuration["httpPort"]
}
.Where(entry => entry.Value is not null);

configuration.AddInMemoryCollection(overrides);
```

Do not scatter several mapping helpers throughout startup. Remove compatibility mappings when the legacy input contract is no longer required.

## Composition contract

For one value, pass the validated enum or primitive directly:

```csharp
var mode = ApplicationModeConfiguration.ReadRequired(configuration);
services.AddApplication(configuration, mode);
```

Do not introduce a wrapper record for a single enum. Introduce a small immutable composition record only when multiple related values are required:

```csharp
public sealed record ApplicationComposition(
    ApplicationMode Mode,
    string InstanceName);
```

The registration method consumes this contract and does not reread those keys:

```csharp
public static IServiceCollection AddApplication(
    this IServiceCollection services,
    IConfiguration configuration,
    ApplicationMode mode)
{
    services.AddApplicationOptions(configuration);

    if (mode == ApplicationMode.External)
    {
        services.AddExternalAdapters(configuration);
    }
    else
    {
        services.AddLocalAdapters();
    }

    return services;
}
```

## Derived settings

Prefer passing typed validated values directly to the registration that needs them. Avoid converting typed values back into `IConfiguration` unless an external package accepts only configuration.

If an external package requires derived configuration:

- isolate it in one focused adapter method;
- add one in-memory provider;
- document its precedence;
- do not re-add command-line providers repeatedly to compensate;
- test that explicit overrides still win according to the documented policy.

## Reload behavior

Use `reloadOnChange: true` only when the complete consuming path supports reload. Reloading a JSON provider does not update:

- composition decisions already used for registration;
- concrete options instances materialized manually;
- derived in-memory mappings computed only during startup.

Static service configuration should normally use `reloadOnChange: false`. Use `IOptionsMonitor<T>` only when invalid reload handling and component reconfiguration have been designed and tested.

## Tests

Tests should prove the boundaries rather than implementation details:

- the application-specific provider adds its final canonical value;
- a configured placeholder cannot override a generated safety-critical path;
- missing, invalid, and valid composition values are handled by the dedicated reader;
- application registration accepts a typed composition input and does not require that key in `IConfiguration`;
- normal options still fail through `OptionsValidationException` during host startup;
- invalid runtime options prevent hosted work from starting;
- bootstrap and final events share the intended log destination.

## Agent checklist

- [ ] Standard providers are not duplicated.
- [ ] Custom providers are complete before host logging is configured.
- [ ] Provider precedence is documented.
- [ ] Compatibility mappings use one canonical override layer.
- [ ] Composition-driving values are read and validated once.
- [ ] Registration consumes typed composition values instead of rereading configuration.
- [ ] Normal options remain owned by their consuming modules.
- [ ] `ValidateOnStart()` protects hosted work.
- [ ] Derived settings are passed directly where possible.
- [ ] Configuration reload is disabled unless fully supported.
- [ ] Tests cover provider, composition, and runtime-option boundaries.
