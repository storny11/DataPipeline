# Review of the Concise Startup Refactor Plan

## Assessment

The concise plan is coherent, internally consistent, and ready to be expanded into the detailed implementation plan.

It correctly covers:

- baseline build and test capture;
- one explicit configuration-provider pipeline;
- documented provider precedence;
- two-stage logging through one shared rolling application-log stream;
- the required hard-failure process contract;
- a strict named operating mode;
- separation of composition-driving configuration from runtime options;
- dormant mode-specific settings;
- preservation of existing optional-email behaviour;
- hosted lifecycle ownership;
- strict dependency-injection validation;
- safe startup-summary logging;
- a complete verification matrix.

## Required clarifications

Add the following details to the implementation plan.

### Reuse the existing log-path convention

Do not introduce `LOG_DIRECTORY` when the existing application-log convention can be resolved safely before builder creation.

Reuse the convention, but do not reuse a resolver unchanged when it:

- requires the complete `IConfiguration`;
- reads identity from a broad options object that may be invalid;
- discovers Serilog file sinks by scanning configuration;
- replaces tokens by mutating configuration after it has been built;
- can run only after failures that the bootstrap logger is intended to capture.

Extract a small, pure, bootstrap-safe path resolver instead. It should accept only stable primitive inputs that are available before normal application configuration and options validation.

Suitable inputs include:

- an application name derived from the entry assembly;
- an instance name supplied as a required launcher argument;
- the existing deterministic log-root selection rule.

Resolve the path once:

```csharp
string logFilePath = ApplicationLogPath.Resolve(
    instanceName,
    applicationName);
```

Then pass the same value to both logger phases:

```csharp
Log.Logger = ApplicationLogging.CreateBootstrapLogger(logFilePath);

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, loggerConfiguration) =>
        ApplicationLogging.ConfigureFinalLogger(
            services,
            loggerConfiguration,
            builder.Configuration,
            logFilePath));

    // Build and run the host.
}
catch (Exception exception)
{
    Log.Fatal(exception, "Application terminated unexpectedly.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
```

The resolver may preserve the existing directory structure while remaining independent of the application options pipeline:

```csharp
internal static class ApplicationLogPath
{
    public static string Resolve(
        string instanceName,
        string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        string logRoot = ResolveExistingLogRoot();

        return Path.Combine(
            logRoot,
            instanceName,
            $"{applicationName}-.log");
    }

    private static string ResolveExistingLogRoot()
    {
        // Preserve the established deployment-specific root-selection rule.
        throw new NotImplementedException();
    }
}
```

The pseudocode intentionally leaves the existing root-selection rule in one replaceable method. The implementation plan must identify and preserve the current rule rather than adding a second competing convention.

Do not materialize and validate the complete host-options object merely to construct the log path. If a required identity value is available only from later-bound configuration, first determine whether the launcher can supply it as a primitive startup argument. If it cannot, record that as a bootstrap-path blocker before implementation; a later-bound path cannot protect the earliest configuration failures.

Code should own the console and rolling-file sinks. JSON configuration should retain levels and category overrides only. Remove file-sink path mutation and duplicate file-sink declarations from configuration.

Deployment must still ensure that:

- the runtime identity can create, append, roll, and delete retained files;
- monitoring or collection reads the resolved location;
- unrelated application instances do not accidentally share the same logical stream.

### Rolling-log terminology

Describe the target as one shared rolling application-log stream, not necessarily one physical file.

`application-.log` is a rolling filename pattern. Daily or size-based rolling can create multiple physical files, but bootstrap and final logging must not create separate startup and application log families.

Prefer Serilog's rolling interval and size controls over manually inserting the current date into the filename. Include a process identifier only when concurrent processes intentionally require separate streams; otherwise it fragments a single application's operational history across restart-specific files.

### Fatal flush and exception propagation

The top-level boundary must flush Serilog before the exception leaves the process:

```csharp
try
{
    // Build and run the host.
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
```

Log-and-rethrow is intentional when the process supervisor requires an unhandled hard-failure signal. Do not replace it with a controlled return code without first changing and testing that process contract.

### Dormant mode-specific settings

Inactive settings must be truly dormant.

In emulated mode, do not register:

- live-only options;
- live-only validators;
- live-only consumers;
- hosted services, health checks, or startup-summary dependencies that force those options to materialize.

It is not sufficient merely to allow invalid live-only values. The host must start successfully with those sections completely absent.

### Disabled email publishing

Preserve the existing feature-switch behaviour:

- keep the email publisher registered when the integration is selected;
- return immediately from the publisher when `Enabled=false`;
- skip email-specific validation while disabled;
- perform no email formatting;
- create no SMTP client;
- perform no network activity;
- leave other publishers active.

When `Enabled=true`, validate all active email settings through the existing validation path.

### Hosted lifecycle cleanup

The hosted lifecycle adapter must:

- start only after startup validation succeeds;
- perform required post-build setup before starting the external runtime;
- avoid external work in constructors;
- clean up partial startup where possible;
- stop idempotently;
- honour cancellation where supported.

### Dependency-injection validation

Keep `ValidateScopes=true` in every environment.

Enable `ValidateOnBuild=true` after application-owned registrations have been corrected. Relax only `ValidateOnBuild` if an exact external blocker is reproduced and cannot be resolved within scope.

If that happens, document:

- the exact service and exception;
- why the registration cannot be corrected immediately;
- the focused composition or resolution test that replaces the missing global check;
- the follow-up work needed to restore global build validation.

There is no general per-registration exemption from `ValidateOnBuild`.

## Additional logging tests

Add explicit verification that:

- bootstrap and final events appear in the same rolling log stream;
- the existing directory convention is preserved by the pure path resolver;
- the path is resolved before builder creation without binding broad application options;
- the resolver does not scan or mutate `IConfiguration`;
- no separate startup-log family is created;
- code and JSON do not configure duplicate file sinks;
- an invalid composition value is logged before host construction completes;
- an options-validation failure is logged during host startup;
- a file-open failure falls back to a usable console logger without recursive failure;
- fatal events are flushed before the exception propagates;
- the process supervisor observes the required hard-failure signal.

## Final conclusion

No architectural redesign is required. Incorporate these clarifications into the detailed plan, then proceed phase by phase with the stated acceptance tests.
