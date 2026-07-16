# Startup and Configuration Validation Pattern

This document defines a reusable startup pattern for a .NET service template. Its goals are:

- every active configuration value has one owner and one binding path;
- invalid required configuration prevents the process from accepting work;
- no background consumer, scheduler, listener, or other hosted work starts before validation;
- early failures are persisted even when the normal logger cannot be created;
- validation messages are useful without exposing secrets;
- the composition root stays explicit and small.

## Non-negotiable invariants

1. Add configuration providers once, before registering options or services.
2. Bind each options type once.
3. The component that consumes an options type owns its registration and validation.
4. Validate normal static options with `ValidateOnStart()`.
5. Validate composition-driving values immediately because they are needed before the service provider exists.
6. Start application work through the host lifecycle, never by calling a custom start method before `RunAsync()`.
7. Keep dependency-injection validation enabled.
8. Maintain an independent bootstrap logging path for failures before the final logger exists.
9. Never log an entire configuration object.
10. Never silently replace an invalid value with a working-looking default.

## Classify configuration before changing code

Inventory every `*Options` and `*Settings` type and every use of:

- `Get<T>()`;
- `Bind()`;
- `Configure<T>()`;
- `IOptions<T>`;
- `IOptionsMonitor<T>`;
- direct `IConfiguration` indexing;
- custom configuration helper methods.

Classify each value into one of the following groups.

### Normal static runtime options

These values are consumed after the host is built, such as endpoint, timeout, formatting, or delivery settings.

Use:

```csharp
services
    .AddOptions<ApplicationOptions>()
    .Bind(section)
    .Validate(_ => section.Exists(), "Required configuration section 'Application' is missing.")
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

Consumers inject `IOptions<ApplicationOptions>`. Do not also call `section.Get<ApplicationOptions>()` in `Program.cs`.

### Composition-driving configuration

Some values decide which implementations are registered. They must be known while the service collection is being composed, before `IOptions<T>` can be resolved.

Read these values once and validate them immediately:

```csharp
var configured = configuration["ImplementationMode"];

if (string.IsNullOrWhiteSpace(configured))
{
    throw new InvalidOperationException(
        "Required configuration value 'ImplementationMode' is missing.");
}

if (!Enum.TryParse<ImplementationMode>(configured, true, out var mode) ||
    !Enum.IsDefined(mode))
{
    throw new InvalidOperationException(
        "Configuration value 'ImplementationMode' is invalid.");
}
```

Do not silently use a default when the value is absent, misspelled, or numeric-but-undefined. If runtime consumers also need this value, register the already validated instance rather than binding it again.

### Optional components

Optional behavior must be disabled explicitly:

```json
{
  "Delivery": {
    "Enabled": false
  }
}
```

When `Enabled` is `true`, every required dependent value must validate. Invalid enabled configuration is fatal. Do not catch validation and silently change `Enabled` to `false`.

### Reloadable options

Use `IOptionsMonitor<T>` only when the component genuinely supports live reload. `ValidateOnStart()` validates the startup value; later monitor values are validated when created. Decide explicitly what the application does when a reload is invalid.

Most service infrastructure options should remain static through `IOptions<T>` unless reload behavior is a real requirement.

### Dormant or placeholder settings

An options class for an unimplemented or unregistered component is not an active option. Do not register it merely to claim complete validation. Either remove it until the component exists or make its owning registration method bind and validate it when that component becomes active.

Document dormant types so a later implementation cannot accidentally consume them without validation.

## Define an options type

Use defaults only when the default is genuinely valid and intentional:

```csharp
using System.ComponentModel.DataAnnotations;

public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; init; } = "";
}
```

The C# `required` keyword is a compile-time object-initializer rule. It is not a substitute for runtime configuration validation.

Data annotations handle local property rules. They do not automatically express every nested, conditional, or cross-property rule.

## Register the options in their owning module

Keep the section lookup and registration together:

```csharp
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
```

This deliberately uses `GetSection()` plus an options validator. Using `GetRequiredSection()` is also valid, but it throws during service registration rather than host startup. Pick one timing model intentionally and ensure the bootstrap logger covers it.

Feature-specific options belong in the feature's `Add...` method, not in `Program.cs`. The composition root should call the feature registration and should not know the feature's internal settings.

## Add semantic validation

Use `IValidateOptions<T>` for rules that need parsing, conditional requirements, nested objects, collections, or relationships between properties.

```csharp
using Microsoft.Extensions.Options;

public sealed class DeliveryOptionsValidator : IValidateOptions<DeliveryOptions>
{
    public ValidateOptionsResult Validate(string? name, DeliveryOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            failures.Add("Delivery:Host is required when delivery is enabled.");
        }

        if (options.Port is < 1 or > 65535)
        {
            failures.Add("Delivery:Port must be between 1 and 65535.");
        }

        if (options.Timeout <= TimeSpan.Zero)
        {
            failures.Add("Delivery:Timeout must be positive.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
```

Register the validator explicitly:

```csharp
services.AddSingleton<IValidateOptions<DeliveryOptions>, DeliveryOptionsValidator>();
```

Collect all useful failures for the type. Do not stop after the first property. Failure messages may identify section and property names but must not contain configured secret values.

## Understand `ValidateOnStart()` timing

`ValidateOnStart()` runs when the host starts, not when `builder.Build()` returns.

This is incorrect:

```csharp
var app = builder.Build();
app.Services.GetRequiredService<IWorkerManager>().Start();
await app.RunAsync();
```

The custom work starts before options startup validation.

Represent application work as `IHostedService`, `BackgroundService`, or `IHostedLifecycleService`:

```csharp
public sealed class WorkerLifecycle(
    IOptions<WorkerOptions> options,
    IWorkerManager manager) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return manager.StartAsync(options.Value, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return manager.StopAsync(cancellationToken);
    }
}
```

Register it normally:

```csharp
services.AddHostedService<WorkerLifecycle>();
```

The host runs startup validators before calling hosted-service `StartAsync()`. Avoid accessing `options.Value` in the hosted service constructor; access it inside `StartAsync()` after validation.

## Keep container validation enabled

Options validation and dependency-injection validation solve different problems:

- options validation checks configuration values;
- `ValidateOnBuild` checks whether registered services can be constructed;
- `ValidateScopes` checks scoped-lifetime misuse.

A strict template can enable both in every environment:

```csharp
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});
```

Do not disable validation globally to accommodate a broken registration. Fix or isolate the registration. If a third-party package truly requires an exception, document the exact failure and add a focused composition test covering the replacement check.

## Use two-stage logging with one physical log

The final host logger does not exist during the earliest configuration and composition failures. Configure a bootstrap logger before creating the builder.

The bootstrap logger must:

- depend on no application configuration section;
- write to console;
- write to the normal application log when console capture is not guaranteed;
- use an absolute directory or primitive launch inputs only;
- use a rolling pattern compatible with the final configured file sink;
- fail startup if the file cannot be opened and console output is not preserved.

Two logger phases do not require two files. Use the same file path and sink settings in both phases. The bootstrap logger writes the earliest events, then the final logger replaces its configuration and continues in the same rolling file. A separate startup file is only useful when early startup cannot use the normal log destination.

Example:

```csharp
var logDirectory = Environment.GetEnvironmentVariable("LOG_DIRECTORY");
logDirectory = string.IsNullOrWhiteSpace(logDirectory)
    ? Path.Combine(AppContext.BaseDirectory, "logs")
    : Path.GetFullPath(logDirectory, AppContext.BaseDirectory);
var logFilePath = Path.Combine(logDirectory, "application-.log");

var bootstrapLogger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();
Log.Logger = bootstrapLogger;

Directory.CreateDirectory(logDirectory);
bootstrapLogger.Reload(configuration => configuration
    .WriteTo.Console()
    .WriteTo.File(
        logFilePath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: null,
        shared: true));
```

Keep this inside the process-boundary `try` block. Do not catch and suppress directory or file-sink failures: the top-level boundary logs to the already available console logger, rethrows, and returns a hard failure signal. Continuing would make later failures non-durable.

Configure the final logger through the host. Keep normal sinks and their operational settings in `appsettings.json`; inject only the already resolved absolute file path as the highest-priority configuration value.

```csharp
builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));
```

The bootstrap sink exists only during the early phase; the final configured sink replaces it. This does not duplicate events. Keep the rolling interval and `shared` behavior compatible so both phases target the same physical file family.

The deployment must provision the directory and grant the process identity write access. Do not assume that a process supervisor preserves console output: it must explicitly redirect and consume the child process's standard output and standard error streams. File logging remains the durable source when it does not.

## Handle the process boundary once

Place builder creation, service registration, host construction, and host execution inside one top-level `try` block:

```csharp
try
{
    // Create, configure, build, and run the host.
    await app.RunAsync();
    return 0;
}
catch (OptionsValidationException exception)
{
    Log.Fatal(
        exception,
        "Application configuration is invalid: {ValidationFailures}",
        exception.Failures);
    return 1;
}
catch (Exception exception)
{
    Log.Fatal(exception, "Application failed during startup or execution.");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
```

Returning a nonzero code gives the process supervisor an explicit failure signal. If the deployment relies on unhandled exceptions for crash dumps, rethrow after logging instead and document that policy.

Do not attempt to report a component's invalid configuration through that same component. Startup diagnostics and alerting must be independent.

## Log only a safe startup summary

Do not do this:

```csharp
logger.LogInformation("Loaded {@Options}.", options);
```

Options types often gain passwords, tokens, connection strings, certificate data, or private endpoints later.

Log an allow-listed summary from a hosted service after validation:

```csharp
logger.LogInformation(
    "Starting {ApplicationName} in {EnvironmentName}.",
    options.Value.Name,
    environment.EnvironmentName);
```

## Tests required for every template

### Options tests

For each active options type, test:

- the required section is missing;
- a required value is blank;
- an integer, enum, URI, or duration cannot be bound;
- each semantic rule fails;
- multiple semantic failures are returned together;
- valid configuration succeeds;
- an explicitly disabled optional component does not require its dependent values.

### Lifecycle test

Register a probe hosted service after the application registrations. Start a generic host with invalid configuration and assert:

- `StartAsync()` throws `OptionsValidationException`;
- the probe's `StartAsync()` was never called.

This proves that invalid configuration prevents application work, rather than merely proving that a validator exists.

### Dependency-injection composition test

Build the host in the development environment, or explicitly enable `ValidateOnBuild` and `ValidateScopes`, and assert construction succeeds for valid configuration.

### Logging test or deployment check

Verify that an early startup failure and a post-build event appear in the same rolling application log and that the failing process exits nonzero. Also test the behavior when the application log directory is not writable; console diagnostics must remain available.

## Migration procedure

1. Inventory all configuration types and access paths.
2. Mark each type as normal runtime, composition-driving, optional, reloadable, or dormant.
3. Move each active type's binding and validation into its owning registration method.
4. Remove duplicate `Get<T>()`, `Bind()`, and concrete options instances.
5. Add custom validators for nontrivial rules.
6. Add `ValidateOnStart()` to static runtime options.
7. Replace manual pre-`RunAsync()` work with hosted lifecycle services.
8. Restore strict container validation.
9. Add the independent bootstrap logger and final logger.
10. Replace whole-object configuration logs with a safe allow-list.
11. Add the options, lifecycle, composition, and logging tests.
12. Run the complete test suite in Release configuration.

## Review checklist

- [ ] Configuration providers are added once.
- [ ] Every active options type has one binding path.
- [ ] Every required section has an explicit missing-section failure.
- [ ] Data annotations cover simple property rules.
- [ ] Custom validators cover conditional, nested, and cross-property rules.
- [ ] Validation failures do not expose secrets.
- [ ] Optional behavior is disabled only explicitly.
- [ ] Invalid enum values cannot silently become defaults.
- [ ] No application work starts before startup validation.
- [ ] No hosted-service constructor reads `options.Value`.
- [ ] `ValidateOnBuild` and `ValidateScopes` are not broadly disabled.
- [ ] Bootstrap logging survives failure before the final logger.
- [ ] The final logger repeats all required sinks.
- [ ] Bootstrap and final events use the same rolling application log.
- [ ] Startup events contain only allow-listed configuration metadata.
- [ ] Invalid configuration produces a nonzero process exit.
- [ ] Tests prove both failure behavior and startup ordering.
