# Thin `Program.cs` Composition-Root Pattern

## Purpose

This guide defines a reusable startup shape for an ASP.NET Core service. The goal is not to hide startup behind a framework. The goal is to keep `Program.cs` as a short, readable process boundary while each cohesive module owns its registration, options, endpoints, and runtime lifecycle.

The resulting `Program.cs` should show the application phases at a glance:

1. establish emergency logging;
2. create and configure one builder;
3. register application modules;
4. build one host;
5. map endpoint modules;
6. run the host;
7. log fatal failures once and flush.

## Responsibility boundaries

### `Program.cs` owns

- creation of the bootstrap logger;
- the top-level `try`/`catch`/`finally` process boundary;
- creation of one `WebApplicationBuilder`;
- calls to host-infrastructure and application-composition extensions;
- one call to `Build()`;
- the top-level endpoint-mapping call;
- one call to `RunAsync()`;
- the failure policy: log and rethrow, or log and return a nonzero exit code;
- final logger flushing;
- `public partial class Program` when integration tests need an entry-point marker.

These lifecycle steps should remain visible. Moving the entire file into a generic startup runner would make ordering harder to review and debug.

### The host-infrastructure extension owns

- the bootstrap-safe log path and bootstrap logger reload;
- delayed validation of launch input after durable logging has been attempted;
- `ValidateOnBuild` and `ValidateScopes` policy;
- registration of the final host logger;
- other process-wide host mechanics that are independent of business features.

Keep this extension cohesive. It should not register feature handlers, clients, repositories, schedulers, or endpoint implementations.

### The application-composition extension owns

- calls to feature and infrastructure registration modules;
- explicit selection of implementations from validated composition-driving configuration;
- registration of hosted lifecycle adapters;
- no direct startup or manual service resolution.

Each feature module still owns its own options binding and validation. A root composition method coordinates modules; it does not copy their internal registrations.

### Endpoint extensions own

- route groups;
- handlers;
- health and diagnostics routes;
- root or metadata routes;
- authorization, filters, and endpoint metadata.

`Program.cs` should call one high-level endpoint method instead of containing inline handlers and individual route declarations.

### Hosted services own runtime startup and shutdown

Listeners, consumers, schedulers, instrumentation adapters, and background workers must participate in the host lifecycle through `IHostedService`, `BackgroundService`, or `IHostedLifecycleService`.

Do not resolve a runtime service from `app.Services` and call a custom `Start()` method before `RunAsync()`. `ValidateOnStart()` executes during host startup; manual work before that point can run with invalid configuration.

## Required ordering

Use the following sequence. The order is part of the design.

1. Create a console-capable emergency/bootstrap logger before the `try` block.
2. Enter the top-level `try` block.
3. Create the application builder exactly once.
4. Add every custom configuration provider that can affect startup.
5. Resolve and inject any bootstrap-safe logging path override.
6. Reload the bootstrap logger from the completed configuration.
7. Reject invalid launch arguments only after the durable logging attempt.
8. Enable strict dependency-injection validation.
9. Register the final logger using the same logging configuration and resolved path.
10. Register application modules.
11. Build the host exactly once.
12. Map endpoint modules.
13. Run the host exactly once.
14. At the process boundary, log known configuration failures or unexpected failures once.
15. Preserve the chosen hard-failure signal by rethrowing or returning a nonzero code.
16. Flush logging in `finally`.

If custom configuration is currently loaded by a method that also binds or validates host options, split it into two operations:

```csharp
builder.AddApplicationConfiguration(args);  // providers only
builder.ConfigureApplicationHost(args, bootstrapLogger);
builder.Services.AddApplication(builder.Configuration); // binding and validation
```

No configuration provider that can change logging may be appended after the bootstrap logger is configured. A final in-memory provider used to inject the resolved file path must remain the highest-priority value for that path.

## Reference shape

### `Program.cs`

```csharp
var bootstrapLogger = ApplicationLogging.CreateFallbackBootstrapLogger();
Log.Logger = bootstrapLogger;

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.ConfigureApplicationHost(args, bootstrapLogger);
    builder.Services.AddApplication(builder.Configuration);

    var app = builder.Build();
    app.MapApplicationEndpoints();

    await app.RunAsync();
    return 0;
}
catch (OptionsValidationException exception)
{
    Log.Fatal(
        exception,
        "Application configuration is invalid: {ValidationFailures}",
        exception.Failures);
    throw;
}
catch (Exception exception)
{
    Log.Fatal(exception, "Application failed during startup or execution.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
```

### Host infrastructure

```csharp
internal static WebApplicationBuilder ConfigureApplicationHost(
    this WebApplicationBuilder builder,
    string[] args,
    ReloadableLogger bootstrapLogger)
{
    var logPath = ApplicationLogPath.Resolve(args, builder.Environment.EnvironmentName);
    var configurationLoggingAvailable = ApplicationLogging.TryConfigureBootstrapLogger(
        bootstrapLogger,
        builder.Configuration,
        logPath.LogFilePath);

    logPath.EnsureLaunchIsValid();

    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateOnBuild = true;
        options.ValidateScopes = true;
    });

    builder.Services.AddSerilog((services, loggerConfiguration) =>
        ApplicationLogging.ConfigureFinalLogger(
            services,
            loggerConfiguration,
            builder.Configuration,
            configurationLoggingAvailable));

    return builder;
}
```

### Application composition

```csharp
public static IServiceCollection AddApplication(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddFeatureOne(configuration);
    services.AddFeatureTwo(configuration);
    services.AddHostedService<ApplicationWorker>();
    services.AddHealthChecks();

    return services;
}
```

### Endpoint composition

```csharp
public static WebApplication MapApplicationEndpoints(this WebApplication app)
{
    app.MapFeatureOneEndpoints();
    app.MapFeatureTwoEndpoints();
    app.MapHealthChecks("/health");

    return app;
}
```

## Configuration and options rules

- Add configuration providers once and document their precedence.
- Bind each runtime options type once in the module that consumes it.
- Use `ValidateOnStart()` for normal static runtime options.
- Read and validate a composition-driving value immediately only when it must select service registrations before the provider exists.
- Register an already validated composition value if runtime consumers also need it; do not bind it again.
- An optional component may skip dependent validation when explicitly disabled. When enabled, all of its required values must validate.
- Do not call `Get<T>()` in `Program.cs` merely to log or inspect runtime settings.
- Do not log whole options objects. Use an allow-listed startup summary from a hosted service after validation succeeds.

See [startup-configuration-pattern.md](startup-configuration-pattern.md) for the detailed options and lifecycle rules.

## Logging rules

- Keep a minimal console fallback that does not depend on application configuration.
- Let normal Serilog configuration own levels, enrichers, formatters, and sinks.
- Let code inject only values that must be known before normal startup, such as a resolved file path.
- Use the same concrete file destination for bootstrap and final logger phases.
- Do not add the same console or file sink in both code and configuration.
- Configure the final logger through the host so it can read registered services.
- Log a fatal exception once at the process boundary and flush in `finally`.
- Do not assume a process supervisor persists standard output unless that behavior has been verified.

See [bootstrap-safe-log-path-plan-update.md](bootstrap-safe-log-path-plan-update.md) for the detailed one-file bootstrap and final-logger pattern.

## Refactoring procedure for an existing service

1. Capture the current build and test baseline, including unrelated failures.
2. Mark every line in `Program.cs` as process lifecycle, host infrastructure, application composition, endpoint mapping, or runtime work.
3. Keep process lifecycle lines in `Program.cs`.
4. Move host-wide logging and dependency-injection policy into one host extension.
5. Move feature registrations into their owning `Add...` methods and keep one application-composition method that calls them.
6. Move inline routes into endpoint mapping extensions.
7. Move manual runtime start/stop calls into hosted lifecycle services.
8. Remove direct runtime options materialization and whole-object configuration logging from `Program.cs`.
9. Preserve configuration precedence and startup ordering while moving code.
10. Build and test after each responsibility boundary moves.

Do not redesign every module merely to shorten `Program.cs`. The composition root should be thin because ownership is clear, not because code was hidden in arbitrary helpers.

## Verification

At minimum, verify:

- a valid host passes `ValidateOnBuild` and `ValidateScopes`;
- missing or invalid runtime options fail during host startup;
- no hosted application work starts before options validation;
- each route is mapped once;
- an early startup failure and a runtime event reach the intended shared application log;
- failure to initialize configured logging leaves a usable console fallback;
- invalid launch input produces the intended hard process failure;
- fatal events are not duplicated through multiple logger/provider paths;
- shutdown flushes buffered events;
- baseline failures are distinguished from regressions caused by the refactor.

Useful review searches include:

```text
Program.cs: Get<
Program.cs: GetRequiredSection
Program.cs: LogInformation("Loaded
Program.cs: app.Services.Get
Program.cs: .Start(
Program.cs: MapGet / MapPost / MapGroup
```

Any remaining match should have a clear process-boundary reason.

## Definition of done

- [ ] `Program.cs` reads as ordered orchestration and process policy.
- [ ] All custom configuration providers run before host logging is finalized.
- [ ] Host infrastructure is cohesive and independent of application features.
- [ ] Each active options type is bound and validated by its owner.
- [ ] Composition-driving configuration is read once.
- [ ] Runtime work starts through the host lifecycle.
- [ ] Endpoint declarations are outside `Program.cs`.
- [ ] Startup metadata is allow-listed rather than whole-object logging.
- [ ] Strict dependency-injection validation is enabled.
- [ ] Bootstrap and final logging preserve one intended flow.
- [ ] Fatal failures retain the deployment's required hard-failure signal.
- [ ] Formatting, build, tests, and startup smoke checks pass or have documented baselines.
