# Authoritative Application Startup and Configuration Design

## Status and scope

This document is the authoritative design for startup, configuration composition, logging, option validation, and process-failure behavior in the template application.

It is written as a clean implementation specification, not as a description of historical code. An implementation agent should reproduce the invariants and ordering described here rather than preserve an older helper structure.

The design is intended for a service with these deployment constraints:

- the launcher can pass command-line arguments reliably;
- the launcher cannot reliably set the .NET host-environment variables;
- part of the application configuration may come from an external configuration store;
- checked-in environment files must be able to override external defaults;
- command-line values must remain the final operator-controlled overrides;
- the process supervisor may not preserve standard output;
- the normal application log directory is known from primitive launch inputs before full configuration exists;
- some third-party dependency registrations are not safe for eager whole-container validation.

## Complete requirement list

### Environment and process startup

1. The logical application environment comes from the required `--environment` command-line argument.
2. `DOTNET_ENVIRONMENT` and `ASPNETCORE_ENVIRONMENT` do not select the logical environment for this application.
3. The environment is validated before `WebApplicationBuilder` is created.
4. The selected value is passed through `WebApplicationOptions.EnvironmentName` so the correct `appsettings.{environment}.json` file is selected.
5. `ContentRootPath` is always `AppContext.BaseDirectory`; the launcher working directory is not trusted.
6. Missing, blank, whitespace-padded, path-like, or otherwise unsafe environment values fail startup.
7. Primitive launch arguments are parsed once and then passed as an immutable object to later startup decisions.

### Configuration composition

1. The effective configuration order is:
   1. standard base application settings;
   2. the initially selected environment settings;
   3. external configuration-store settings;
   4. canonical mappings derived from external settings;
   5. the same environment settings intentionally reapplied;
   6. final command-line overrides;
   7. generated safety-critical values, such as the resolved absolute log path.
2. Later providers win.
3. The environment file is reapplied deliberately so checked-in environment values can override external defaults.
4. Command-line mappings convert operator-friendly flat switches into canonical application keys.
5. External configuration selection uses a primitive launch selector such as `--externalProfile`.
6. An external selector and an external-provider callback are a pair:
   - callback supplied but selector missing is invalid;
   - selector supplied but callback missing is invalid;
   - neither supplied is valid for an application without an external provider.
7. The external provider receives the already parsed selector and must not reread it from mutable application configuration.
8. External-provider failures are fatal. Startup never continues with a partially loaded configuration.
9. Compatibility mappings are added in one focused in-memory provider and produce canonical keys.
10. Full configuration objects, credentials, connection strings, and secrets are never logged.
11. Static startup configuration does not use reload-on-change unless the complete consumer path explicitly supports safe reload.

### Local environment files

1. Local variants are ordinary environment files, not a second special override mechanism.
2. Examples are:
   - `appsettings.local.json` selected by `--environment=local`;
   - `appsettings.local-uk.json` selected by `--environment=local-uk`.
3. Checked-in local environment files contain only safe development defaults and no secrets.
4. The same environment-file replay rule applies to local and deployed environment names.

### Durable logging

1. Startup failures must be written to a durable file before full application configuration exists.
2. Bootstrap and final Serilog phases use the exact same resolved file path.
3. On Windows, the fixed log root is:
   - `D:\Logs` when the `D:` drive exists;
   - otherwise `C:\Logs`.
4. A non-Windows template run uses `<application-base>/logs` so the reference project remains testable across operating systems.
5. An optional `--instance` argument adds one safe subdirectory under the fixed root.
6. Missing `--instance` is valid and writes directly under the root.
7. An explicitly supplied invalid instance value selects the root fallback path first, enables durable logging there, and then fails startup.
8. The instance value is one directory segment. It cannot be blank, whitespace-padded, rooted, `.` or `..`, or contain directory separators or invalid filename characters.
9. The filename is `<application>_<yyyyMMdd>_<processId>.log`.
10. Because date and process ID are already part of the filename, Serilog uses `RollingInterval.Infinite`; size-based rolling remains enabled.
11. Bootstrap retention is bounded so a repeatedly failing process cannot create unbounded size-roll files within one file family.
12. Sink selection, normal levels, enrichers, output templates, size limits, retention, and shared-file behavior remain visible in `appsettings.json`.
13. Code owns only the early bootstrap sink, safe path resolution, the final resolved path injection, and logger lifecycle.
14. The named final File sink is required and its path is overwritten by the generated absolute path at the highest precedence.
15. A configured or command-line path cannot redirect the application log outside the validated destination.
16. Failure to create or open the durable bootstrap log is fatal; the process does not continue in a console-only state.
17. The top-level boundary logs a fatal event once, preserves the original exception, and produces the deployment-required hard failure signal.
18. Buffered events are flushed during both normal shutdown and failure.

### Composition and option validation

1. Composition-driving configuration remains configuration-driven and preserves its established parsing semantics.
2. Composition values are read and validated once after the final provider pipeline exists and before service registration depends on them.
3. Normal runtime options are owned, bound, and validated by the module that consumes them.
4. Static runtime options use `ValidateOnStart()`.
5. Optional behavior uses an explicit `Enabled` value:
   - disabled behavior does not require its inactive dependent settings;
   - enabled behavior validates every required dependent setting;
   - invalid enabled configuration fails startup rather than silently disabling the feature.
6. Runtime work starts through the host lifecycle after startup validation, never through a manual pre-`RunAsync()` start call.
7. `ValidateScopes` remains enabled.
8. `ValidateOnBuild` remains disabled because some external registrations are intentionally completed lazily and are not safe for eager construction.
9. Application-owned registrations compensate with focused composition and host-start tests.
10. If an external settings document exposes a value under a noncanonical path, one adapter maps it to the canonical application path before final environment and CLI overrides are applied.

## Design principles

### Bootstrap configuration is not application configuration

Only values required to construct the host or its configuration pipeline belong in `ApplicationLaunchArguments`:

- logical environment;
- optional log instance;
- optional external configuration selector.

Ports, feature flags, reporting settings, adapter settings, timeouts, and endpoints are normal application configuration. They must not be manually materialized during bootstrap.

### Provider construction, composition decisions, and runtime options are separate phases

The startup path has three configuration responsibilities:

1. **Provider construction** builds the final `IConfiguration` view.
2. **Composition decisions** read the small number of values needed to choose registrations.
3. **Runtime options** are bound and validated by their owning modules.

Combining these responsibilities produces duplicate binding, unclear precedence, and failures that occur before durable logging is ready.

### Ordering remains visible

`Program.cs` is intentionally short, but it still shows the process-ordering decisions. Do not hide the entire startup path behind a single opaque `StartApplication()` method.

## Component responsibilities

### `ApplicationLaunchArguments`

Responsibilities:

- parse command-line input once with the standard .NET command-line provider;
- expose only primitive bootstrap selectors;
- preserve the provider's supported argument forms and last-value-wins behavior.

It does not:

- bind runtime option objects;
- inspect environment variables;
- load external configuration;
- decide service composition.

### `ApplicationEnvironment`

Responsibilities:

- require the parsed command-line environment;
- validate that it is one safe environment name;
- return the exact value used by `WebApplicationOptions.EnvironmentName`.

It does not fall back to .NET environment variables or to `Production`.

### `ApplicationLogPath`

Responsibilities:

- compute the fixed root without application configuration;
- validate the optional instance as one safe path segment;
- generate the required filename;
- return both the safe path and any validation error.

Returning both values is intentional. An unsafe supplied instance must use the root fallback long enough to establish durable logging, then surface its validation error.

### `ApplicationLogging`

Responsibilities:

- create a console bootstrap logger immediately;
- reload it with the resolved bootstrap File sink;
- inject the resolved path into the named configured File sink;
- verify that the required sink exists;
- configure the final logger from configuration and DI services;
- leave final operational sink settings in JSON.

### `AddApplicationConfiguration`

Responsibilities:

- add the external provider when supplied;
- enforce selector/provider symmetry;
- reapply the selected environment file once;
- add final mapped command-line values;
- inject the protected generated log path last.

It does not bind broad application options or choose adapters.

### Application modules

Each module owns:

- its configuration section name;
- its `AddOptions<T>()` registration;
- data-annotation and semantic validators;
- `ValidateOnStart()`;
- its services and hosted lifecycle components.

## Exact startup sequence

The required process order is:

1. Create a minimal reloadable console logger and assign it to `Log.Logger`.
2. Parse `ApplicationLaunchArguments` once.
3. Resolve a safe log path from primitive launch inputs.
4. Enable the bootstrap File sink at that path.
5. Throw any deferred unsafe-instance validation error; the error is now durable.
6. Validate the required CLI environment.
7. Create `WebApplicationBuilder` with the validated environment and fixed content root.
8. Add external configuration when configured.
9. Add any canonical external-value mappings owned by that external adapter.
10. Reapply the selected environment JSON file.
11. Add final mapped command-line configuration.
12. Inject the protected resolved File sink path.
13. Verify the named File sink and configure host-wide logging and DI policy.
14. Read and validate composition-driving values once.
15. Register application modules and their option pipelines.
16. Build the application.
17. Map endpoints.
18. Run the host.
19. Log fatal failures once, preserve the hard-failure behavior, and flush Serilog.

The composition root should retain this recognizable shape:

```csharp
var bootstrapLogger = ApplicationLogging.CreateBootstrapLogger();
Log.Logger = bootstrapLogger;

try
{
    var launchArguments = ApplicationLaunchArguments.Parse(args);
    var logPath = ApplicationLogPath.Resolve(launchArguments.Instance);

    ApplicationLogging.EnableBootstrapFile(bootstrapLogger, logPath.FilePath);
    logPath.ThrowIfInvalid();

    var environmentName = ApplicationEnvironment.ReadRequired(launchArguments);
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory,
        EnvironmentName = environmentName
    });

    builder.AddApplicationConfiguration(args, logPath.FilePath, launchArguments);
    builder.ConfigureApplicationHost();

    var mode = ApplicationModeConfiguration.ReadRequired(builder.Configuration);
    builder.Services.AddApplication(builder.Configuration, mode);

    var app = builder.Build();
    app.MapApplicationEndpoints();

    await app.RunAsync();
    return 0;
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
```

An application may retain a dedicated `OptionsValidationException` catch solely to provide a clearer configuration-failure message, provided it still rethrows and the general catch does not log the same exception again.

## Provider precedence in detail

`WebApplication.CreateBuilder(...)` first installs the normal framework providers. The important initial sources include base JSON, selected environment JSON, user secrets where framework rules enable them, environment variables, and command-line arguments.

The application then appends its required providers. The final effective precedence, from lower to higher, is:

| Order | Provider | Purpose |
|---:|---|---|
| 1 | `appsettings.json` | Safe application defaults |
| 2 | Initial `appsettings.{environment}.json` | Initial environment values and standard builder behavior |
| 3 | Other initial framework providers | Framework-compatible bootstrap inputs |
| 4 | External configuration | Environment or instance settings from the external store |
| 5 | Canonical external mappings | Adapt external key shapes to application-owned keys |
| 6 | Reapplied `appsettings.{environment}.json` | Intentional checked-in environment overrides |
| 7 | Final mapped command line | Explicit operator overrides and flat-to-canonical mappings |
| 8 | Generated log-path override | Nonredirectable safety value |

The duplicate environment JSON source is deliberate and limited to one replay. Do not repeatedly append it around individual mapping operations.

Do not clear all framework sources merely to create a theoretically pure provider list. Reconstructing every framework provider is more fragile than one documented environment-file replay.

Environment variables may remain among the standard initial application providers, but they do not choose the logical host environment and they are not replayed as final overrides in this design.

## Command-line contract and mappings

Use standard long switches:

```text
--environment=local
--instance=worker-01
--externalProfile=region-a
--application-name="Local data retriever"
--adapter-mode=Simulator
--log-level=Debug
```

Bootstrap selectors keep their primitive names. Operator-friendly runtime switches map directly to canonical configuration keys in one dictionary:

```csharp
private static readonly Dictionary<string, string> CommandLineMappings =
    new(StringComparer.OrdinalIgnoreCase)
    {
        ["--application-name"] = "Application:Name",
        ["--adapter-mode"] = "AdapterMode",
        ["--log-level"] = "Serilog:MinimumLevel:Default"
    };
```

Prefer `AddCommandLine(args, mappings)` over several handwritten mapping helpers. Add new mappings only for a real launcher compatibility requirement.

## External configuration adapter

The template represents the external provider as a callback because it cannot invent a real external store:

```csharp
builder.AddApplicationConfiguration(
    args,
    logPath.FilePath,
    launchArguments,
    (configuration, profile) =>
    {
        configuration.AddExternalConfiguration(profile);
        configuration.AddCanonicalExternalMappings();
    });
```

The callback is the ownership boundary for external-store concerns. It may:

- load a connection alias from the external selector;
- load an environment settings document;
- load a service settings document;
- normalize a noncanonical external email endpoint into `EmailClient:BaseAddress`;
- add derived transport settings after all required selectors are known.

It must not:

- read the selector again from mutable configuration;
- log credentials or complete documents;
- bind unrelated runtime option objects;
- suppress provider failures;
- append command-line providers itself.

When mapping an external value, add one in-memory provider immediately after the external source. The later environment file and final CLI provider then override it naturally.

## Local environments

Local configuration uses the exact same environment mechanism as deployed configuration.

Example launch profiles:

```json
{
  "profiles": {
    "local-http": {
      "commandName": "Project",
      "commandLineArgs": "--environment=local"
    },
    "local-uk-https": {
      "commandName": "Project",
      "commandLineArgs": "--environment=local-uk"
    }
  }
}
```

Corresponding files:

```text
appsettings.local.json
appsettings.local-uk.json
```

There is no special `AddLocalConfiguration()` phase and no ignored machine-only file with the same name. Machine secrets belong in an approved secret store or explicit environment mechanism, not checked-in JSON.

## Logging design

### Path resolution

For a valid instance `worker-01`, an example path is:

```text
D:\Logs\worker-01\Application.Api_20260717_12345.log
```

Without an instance:

```text
D:\Logs\Application.Api_20260717_12345.log
```

For an unsafe supplied instance, path resolution returns the root path plus a validation error. Startup enables that root file first and then throws the error.

### Why rolling interval is infinite

The required filename already contains the startup date and process ID. Daily Serilog rolling would add another date suffix and create a confusing filename contract. Use:

- `RollingInterval.Infinite`;
- a size limit;
- `rollOnFileSizeLimit: true`;
- bounded retained files within the process file family;
- `shared: true` only where the deployment requires compatible shared access.

Cross-process retention for process-ID-based file families is an operational log-maintenance responsibility. A File sink opened for one process-specific path cannot reliably clean every historical process-ID family.

### Configuration ownership

`appsettings.json` retains the visible normal sink definition:

```json
{
  "Serilog": {
    "Using": [
      "Serilog.Sinks.Console",
      "Serilog.Sinks.File"
    ],
    "MinimumLevel": {
      "Default": "Information"
    },
    "Enrich": [
      "FromLogContext"
    ],
    "WriteTo": {
      "ConsoleSink": {
        "Name": "Console"
      },
      "FileSink": {
        "Name": "File",
        "Args": {
          "path": "logs/application.log",
          "rollingInterval": "Infinite",
          "retainedFileCountLimit": 14,
          "fileSizeLimitBytes": 10485760,
          "rollOnFileSizeLimit": true,
          "shared": true
        }
      }
    }
  }
}
```

The configured path is an intentionally visible placeholder. Code injects the validated absolute path under this stable key:

```text
Serilog:WriteTo:FileSink:Args:path
```

Never scan a sink array, mutate JSON, or replace arbitrary path tokens at runtime.

### Failure policy

The durable application file is mandatory under the stated supervisor behavior. Therefore:

- bootstrap directory creation failure is fatal;
- bootstrap File sink creation failure is fatal;
- missing or renamed final File sink is fatal;
- external configuration failure is fatal;
- invalid required configuration is fatal;
- no failure path silently continues with console-only logging.

The initial console logger still provides the best available diagnostic if the mandatory file itself cannot be opened, but it does not convert that failure into successful startup.

## Host and options policy

### Dependency-injection validation

The constrained host policy is:

```csharp
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = false;
    options.ValidateScopes = true;
});
```

This is an explicit compatibility exception, not a recommendation to ignore composition quality. Every application-owned registration path must have focused tests that build or start a host with valid configuration.

If the external packages later become safe for eager validation, turn `ValidateOnBuild` back on and remove this exception.

### Runtime options

Normal static options use one owner and one binding path:

```csharp
var section = configuration.GetSection(ApplicationOptions.SectionName);

services
    .AddOptions<ApplicationOptions>()
    .Bind(section)
    .Validate(
        _ => section.Exists(),
        $"Required section '{ApplicationOptions.SectionName}' is missing.")
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

Use `IValidateOptions<T>` for conditional, nested, parsing, or cross-property rules. Validation messages identify keys but never echo secret values.

### Optional reporting or notification

Register the component consistently and let `Enabled` control runtime behavior. The component may be a no-op when disabled. Its validator follows this rule:

- `Enabled=false`: succeed without requiring delivery-specific fields;
- `Enabled=true`: validate address, recipients, port, timeouts, and every active dependency;
- malformed `Enabled` or invalid active fields: fail startup.

Do not use conditional service registration spread across `Program.cs` merely to avoid validating disabled settings.

## Testing specification

### Launch parsing

Test:

- supported command-line forms;
- separated and equals syntax;
- duplicate values use last-value-wins;
- missing environment fails;
- the legacy environment alias is not silently accepted;
- unsafe environment names fail;
- external selector is retained exactly once.

### Log path

Test:

- missing instance resolves to the root and succeeds;
- valid instance resolves below the root;
- blank, traversal, rooted, and separator-containing instances resolve to root then fail;
- filename contains application name, current date, and process ID;
- bootstrap and final events appear in the same physical file;
- configured and CLI path values cannot override the generated path;
- an unavailable bootstrap path fails startup.

### Provider precedence

Use unique values at each layer and prove:

- external overrides base and the initial environment view;
- the reapplied environment file overrides external values;
- final CLI overrides the reapplied environment file;
- canonical switch mappings populate the expected nested keys;
- generated log path overrides every configured path;
- a missing optional environment file is allowed;
- external selector/provider mismatch fails in both directions.

### Options and lifecycle

For each active options type, test:

- missing section;
- blank required value;
- invalid scalar binding;
- semantic validation rules;
- disabled optional behavior;
- enabled valid behavior;
- enabled invalid behavior;
- invalid options prevent hosted application work from starting.

### Composition

Test every configuration-driven registration choice. Because `ValidateOnBuild` is disabled globally, add explicit tests that build or start the application-owned host for each supported composition.

## Agent implementation procedure

An implementation agent should follow this order:

1. Capture the current build and test baseline.
2. Write the complete provider-precedence test before changing provider order.
3. Introduce or update the immutable launch-argument record.
4. Remove .NET environment-variable fallback from logical environment selection.
5. Set `EnvironmentName` and `ContentRootPath` in `WebApplicationOptions`.
6. Implement safe log resolution returning path plus deferred validation error.
7. Enable the required bootstrap file before throwing the deferred error or loading external configuration.
8. Configure one stable named File sink in JSON and inject only its path from code.
9. Implement the external-provider callback with symmetric selector validation.
10. Put external canonical mappings inside the external adapter boundary.
11. Reapply the selected environment file once.
12. Replace scattered flat-key mappings with one command-line switch map.
13. Add the generated log path last.
14. Keep composition-value parsing separate from runtime options.
15. Keep runtime option registration and validation with each owning module.
16. Keep runtime startup in hosted lifecycle services.
17. Apply the documented DI validation exception and add focused composition tests.
18. Remove obsolete special-local-file handling and obsolete configuration helpers.
19. Run formatting, focused tests, full tests, and startup smoke tests.
20. Review the final provider source list and confirm there is exactly one intentional environment-file replay and one final CLI provider.

## Anti-patterns to reject

- Choosing the logical environment from Debug/Release compilation.
- Allowing the host to silently fall back to `Production`.
- Reading the current working directory as the content root.
- Parsing command-line selectors independently in several helpers.
- Rebuilding broad runtime option objects during provider composition.
- Adding command-line configuration after every mapping step.
- Creating a second special local-settings mechanism.
- Allowing external configuration to override explicit final CLI values.
- Letting a configured log path bypass safe path resolution.
- Using array indexes to find the File sink.
- Continuing without the mandatory durable file.
- Logging complete options or configuration documents.
- Starting application work manually before `RunAsync()`.
- Disabling all DI validation without focused replacement tests.

## Definition of done

- [ ] `--environment` is required, safe, and selected before builder creation.
- [ ] .NET environment variables do not choose the logical environment.
- [ ] Content root is the executable directory.
- [ ] Bootstrap selectors are parsed once.
- [ ] Provider precedence matches the documented table.
- [ ] The selected environment file is replayed exactly once after external configuration.
- [ ] Local variants use ordinary environment files.
- [ ] Flat CLI mappings exist in one dictionary.
- [ ] External selector/provider symmetry is enforced.
- [ ] External failures are fatal.
- [ ] Canonical external mappings precede final environment and CLI overrides.
- [ ] Missing instance uses the root log directory.
- [ ] Unsafe instance logs to the root fallback and then fails.
- [ ] Bootstrap and final Serilog use the same process-specific file.
- [ ] The final named File sink is configuration-owned and path-protected.
- [ ] Bootstrap size rolling and retention are bounded.
- [ ] Composition-driving values are read once.
- [ ] Runtime option types have one owner and `ValidateOnStart()`.
- [ ] Disabled optional components skip inactive requirements.
- [ ] Enabled optional components validate completely.
- [ ] Runtime work starts through the host lifecycle.
- [ ] `ValidateScopes` is enabled.
- [ ] The `ValidateOnBuild` compatibility exception is documented and covered by focused tests.
- [ ] Fatal failures are logged once and preserve the hard-failure signal.
- [ ] Full configuration objects and secrets are never logged.
- [ ] Build, formatting, focused tests, full tests, and startup checks pass.

