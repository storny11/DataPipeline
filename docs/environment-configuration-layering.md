# Explicit Environment and Configuration Layering

## Purpose

This pattern is for a service whose deployment launcher can pass command-line arguments but cannot reliably set `ASPNETCORE_ENVIRONMENT`.

It solves two separate problems:

1. select the ASP.NET Core host environment before the builder is created;
2. apply configuration providers in one documented, testable order.

Keep the logical environment separate from an external settings selector. For example, `env=Staging` may select the host environment and `externalProfile=region-a` may select a specific external configuration source.

## Resolve the environment before builder creation

The host environment is fixed while `WebApplicationBuilder` is created. Calling `UseEnvironment()` later is too late for reliable selection of `appsettings.{Environment}.json` and other environment-sensitive behavior.

Resolve the value from these sources, in order:

1. command-line `env`;
2. `DOTNET_ENVIRONMENT` as a local/developer fallback;
3. `ASPNETCORE_ENVIRONMENT` as a local/developer fallback;
4. fail startup if none is present.

Failing is intentional. It prevents a deployed service from silently reporting `Production` merely because its launcher forgot the environment argument.

Enable the bootstrap-safe application file before resolving the environment or invoking an external provider. That keeps these earliest failures durable while still using the same file family as normal logging.

```csharp
var environmentName = ApplicationEnvironment.ReadRequired(args);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
    EnvironmentName = environmentName
});
```

Use `Development` when framework development semantics such as `IsDevelopment()` are required. A custom value such as `DEV` is valid, but ASP.NET Core treats it as a distinct environment and looks for `appsettings.DEV.json`.

The value must be one safe environment name: no blank value, surrounding whitespace, rooted path, directory separator, or traversal segment.

## Provider precedence

The effective application order is below. Later providers win.

1. `appsettings.json`;
2. `appsettings.{Environment}.json` and other standard builder sources;
3. the external settings provider;
4. optional `appsettings.local.json`;
5. environment variables;
6. command-line arguments;
7. generated safety-critical values, such as a validated absolute log path.

`WebApplication.CreateBuilder(...)` installs the standard sources and makes bootstrap selectors from environment variables and command-line arguments available. The application then adds the external and local layers and deliberately appends environment variables and command-line arguments once more so they retain their expected final precedence.

```csharp
addExternalConfiguration?.Invoke(builder.Configuration);

builder.Configuration
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

ApplicationLogging.ApplyResolvedLogFilePath(configuration, resolvedLogPath);
```

This deliberate final replay is different from repeatedly adding command-line providers between configuration transformations. It happens once, at the end of the normal provider pipeline.

Do not clear `builder.Configuration.Sources` merely to reorder application providers. Clearing it also removes framework sources such as user secrets and host/application fallback providers.

## External settings provider

The template exposes an insertion callback instead of inventing a fake remote provider:

```csharp
var launch = builder.AddApplicationConfiguration(
    args,
    configuration => configuration.AddExternalSettings(
        configuration["externalProfile"]));
```

The external selector is visible because the builder initially loaded command-line arguments. Command-line arguments are replayed after all custom providers, so explicit final overrides still win.

If the external provider fails, fail startup. Do not silently continue with incomplete settings. Never log provider credentials or complete configuration objects.

## Local settings file

`appsettings.local.json` is a machine-local override:

- it is optional;
- it is loaded from the fixed content root;
- reload is disabled because this template treats startup configuration as static;
- the real file is ignored by Git;
- `appsettings.local.example.json` documents the shape without containing secrets;
- environment variables and command-line arguments can still override it.

Do not deploy `appsettings.local.json` unless a machine-specific deployment overlay is explicitly intended.

## Launch examples

Deployed launch:

```text
dotnet Application.Api.dll --env=Staging --externalProfile=region-a --instance=worker-01
```

Local `launchSettings.json`:

```json
{
  "commandLineArgs": "--env=Development"
}
```

Local file copied from the example:

```text
appsettings.local.example.json -> appsettings.local.json
```

## Agent implementation checklist

- [ ] Resolve the host environment before creating the builder.
- [ ] Enable the bootstrap-safe application file before resolving fallible startup inputs.
- [ ] Pass it with `WebApplicationOptions.EnvironmentName`.
- [ ] Keep `ContentRootPath` independent of the launcher working directory.
- [ ] Never infer environment from Debug/Release builds.
- [ ] Keep the external selector separate from the logical environment.
- [ ] Add the external provider before the local file.
- [ ] Add `appsettings.local.json` once, optional and with reload disabled.
- [ ] Replay environment variables and CLI once after custom providers.
- [ ] Keep generated safety-critical values last.
- [ ] Do not clear all framework configuration sources.
- [ ] Test every precedence boundary and missing/invalid environment input.
- [ ] Verify `builder.Environment.EnvironmentName` equals the selected value.
