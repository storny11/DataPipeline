# Plan Update: Bootstrap-Safe Shared Application Log

## Purpose

Update the implementation plan to use the existing fixed log root and an optional namespace command-line argument without reading application options or mutating Serilog configuration.

This document uses generic names intentionally. Keep the revised plan free of organization, product, service, and infrastructure-specific terminology.

## Final launch rules

Use these path rules:

| Launch shape | Namespace argument | Log directory | Startup result |
|---|---|---|---|
| Development/local | Absent | Existing log root | Continue |
| Development/local | Valid | Existing log root plus namespace | Continue |
| Deployed | Valid | Existing log root plus namespace | Continue |
| Deployed | Absent | Existing log root fallback | Log fatally and fail |
| Any | Blank, malformed, or unsafe | Existing log root fallback | Log fatally and fail |

The namespace is a primitive launcher input in deployed environments. It must not be obtained by binding a broad application-options object.

## Required implementation shape

### Resolve the path before builder creation

Create a pure path resolver that receives:

- command-line arguments;
- the environment name available from primitive process inputs;
- the existing fixed log root;
- the deterministic application-log filename.

The resolver must not receive `IConfiguration`.

It returns:

- the path bootstrap and final logging should share;
- an optional launch-validation error.

When the namespace is valid, append it as one directory segment beneath the existing log root. When a local launch omits it, use the root-level application-log path.

When a deployed launch omits it or any launch supplies an unsafe value, return the root fallback path plus a validation error. This lets the application create a durable bootstrap logger before failing.

### Configure logging before validating the launch

Use this order:

1. Resolve the normal or fallback log path.
2. Configure the bootstrap logger with that path.
3. Throw any launch-validation error inside the top-level `try` block.
4. Log fatally and rethrow from the top-level boundary.
5. Flush Serilog in `finally`.

Do not throw for a missing deployed namespace before the fallback logger exists.

### Use one resolved path

Resolve the path exactly once. Pass the same value to:

- the bootstrap logger;
- the final logger.

Do not change from a root-level path to a namespaced path after builder creation. A supported local launch that starts with the root path must keep using it for the lifetime of that process.

### Keep sink ownership in code

Code owns:

- console sink;
- rolling-file sink;
- retention;
- size limits;
- shared-file behaviour;
- resolved file path.

JSON configuration owns:

- minimum levels;
- category overrides;
- level switches that do not add destinations.

Remove logic that scans configured sinks, replaces path tokens, or writes resolved paths back into configuration.

## Path validation

Treat the namespace as one directory name, not a relative path.

Reject:

- empty values;
- whitespace-only values;
- surrounding whitespace;
- `.` and `..`;
- rooted paths;
- directory separators;
- invalid filename characters.

Use the root fallback path when validation fails, configure bootstrap logging, and then fail startup.

## Filename and rolling behaviour

Keep the application's established deterministic filename convention where operational compatibility requires it. Compute date or process tokens once if they are retained, then give the same completed path to both logger phases.

If the filename uses Serilog's rolling marker, let Serilog own date and size rolling. Describe the result as one shared rolling log stream; rolling may produce multiple physical files.

## Optional boolean composition switch

Preserve these independent mode-switch semantics:

- missing or `false` selects live dependencies;
- `true` selects emulated dependencies;
- blank or malformed values fail startup.

Read that raw scalar once before selecting registrations. Do not rebind it through a later runtime-options path merely for consistency.

## Top-level failure boundary

Keep one boundary around builder creation, registration, host construction, and execution:

```csharp
try
{
    startupLogging.EnsureLaunchIsValid();

    // Create, configure, build, and run the host.
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

## Required tests

Add tests proving:

- local missing namespace uses the root log path;
- deployed missing namespace selects the root fallback and fails;
- valid deployed namespace selects the namespaced path;
- common command-line argument forms are accepted;
- blank, whitespace, rooted, traversing, and separator-containing values fail;
- duplicate arguments follow the documented last-value-wins rule;
- bootstrap and final events use the same resolved path;
- a pre-builder launch error reaches the fallback file;
- an options-validation error reaches the shared application log;
- no configuration sink is scanned or mutated;
- no separate startup-log family is created.

## Instruction to the implementation agent

Revise the plan with these fixed launch and logging rules, then implement the path resolver before the broader startup refactor. Do not introduce a new log-directory configuration feature. Do not obtain the namespace from late-bound options. Resolve once, configure bootstrap logging, validate the launch, and reuse the same path for final logging.
