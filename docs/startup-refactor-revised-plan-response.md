# Response to the Revised Startup Refactor Plan

## Verdict

The revised plan is coherent with one clarification: the optional boolean mode switch intentionally defaults to live behaviour when the key is absent.

That is acceptable because the value drives service composition and has an explicit domain default. It does not need to be made required merely to resemble ordinary runtime options.

The remaining open item is implementation proof: verify that the existing application-log path convention can be resolved before builder creation using primitive startup inputs only.

## 1. Preserve the optional boolean mode switch

### Decision

Keep the existing boolean mode switch when the supported behaviour is intentionally:

| Configuration state | Selected mode |
|---|---|
| Key absent | Live |
| `false` | Live |
| `true` | Emulated |
| Blank or malformed | Startup failure |

Missing and invalid are different states. Missing selects the documented default; an explicitly supplied value that cannot be parsed must never silently select live mode.

### Why this does not need the ordinary options pattern

This value is composition-driving configuration. The host needs it before it can choose which services to register.

Therefore it should:

1. be read once before affected registrations;
2. be parsed explicitly;
3. select the dependency graph;
4. be exposed as the already resolved value only if runtime consumers need it.

It should not also be rebound through `AddOptions<T>().Bind(...).ValidateOnStart()`. Startup validation occurs too late to make the original composition decision and would create a duplicate configuration path.

If runtime consumers require `IOptions<T>`, expose an already validated composition object with `Options.Create(...)` rather than binding it again.

### Parsing requirement

Do not rely on a non-nullable boolean property's default value to hide malformed input. Read the raw scalar so absence can be distinguished from an invalid supplied value.

Use behaviour equivalent to:

```csharp
internal static bool ReadUseEmulation(IConfiguration configuration)
{
    ArgumentNullException.ThrowIfNull(configuration);

    string? configuredValue = configuration["UseEmulation"];
    if (configuredValue is null)
    {
        return false; // The documented default is live mode.
    }

    if (!bool.TryParse(configuredValue, out bool useEmulation))
    {
        throw new InvalidOperationException(
            "Configuration value 'UseEmulation' must be 'true' or 'false' when supplied.");
    }

    return useEmulation;
}
```

The example uses a generic key. The implementation should retain the application's established key.

This parser intentionally treats:

- `null` as absent and therefore live;
- an empty string as invalid;
- whitespace as invalid;
- arbitrary text as invalid;
- `true` and `false` case-insensitively.

Do not introduce a named enum solely for stylistic consistency when the two-state boolean and its default are deliberate requirements.

### Documentation requirement

Document the default next to:

- the parser;
- the base configuration example;
- deployment instructions;
- the composition test cases.

Use explicit wording:

> When the mode-switch key is absent or set to `false`, the application uses live dependencies. Set it to `true` to use emulated dependencies. A supplied blank or malformed value fails startup.

### Acceptance criteria

- Missing key selects live registrations.
- Explicit `false` selects live registrations.
- Explicit `true` selects emulated registrations.
- Empty input fails.
- Whitespace input fails.
- Non-boolean input fails.
- The value is read once.
- No second options binding exists for the same composition decision.
- Tests assert the exact registrations selected in every valid case.

## 2. Reuse the existing application-log path convention

### Decision

Reuse the existing directory and filename convention instead of introducing a new log-location convention, provided it can be resolved safely before builder creation.

Do not reuse a resolver unchanged when it:

- requires the complete `IConfiguration`;
- scans configured Serilog sinks;
- binds a broad host-options object;
- mutates file-sink paths in configuration;
- runs only after failures that bootstrap logging must capture.

Extract a pure bootstrap-safe path resolver.

### Required resolver shape

The resolver should accept only primitive values available before the builder and normal option validation, for example:

- application name derived from the entry assembly;
- instance identity supplied by the launcher;
- the existing deterministic root-selection rule;
- process id only if the existing operational convention genuinely requires per-process files.

Conceptually:

```csharp
string logFilePath = ApplicationLogPath.Resolve(
    instanceName,
    applicationName);
```

Resolve the value once and give the exact same path to:

- the bootstrap logger;
- the final logger.

The resolver must not receive or mutate `IConfiguration`.

### Rolling-file ownership

Code owns:

- console sink;
- file sink;
- rolling interval;
- file-size limit;
- retention;
- shared-file behaviour;
- the common resolved path.

JSON configuration owns:

- minimum levels;
- category overrides;
- level switches that do not add destinations.

Remove configuration-driven file-sink path mutation and duplicate sink ownership.

Prefer Serilog rolling controls over manually inserting the current date into the path. Include a process identifier only when concurrent processes intentionally need separate streams; otherwise it creates a new file family after every restart.

### Proof step before the full refactor

Implement or test the path resolver as the first focused spike. Prove that it can resolve the expected production path using only primitive startup inputs.

The proof must establish:

1. every required path token has a pre-builder source;
2. missing required identity fails with a clear bootstrap-safe message;
3. the runtime identity can create and append to the target directory;
4. bootstrap and final events appear in the same rolling stream;
5. no configuration sink is scanned or rewritten;
6. no separate startup-log family is produced.

If a required path component is available only from later-bound application options, stop and revise the path decision. Do not bind broad options early merely to make logging work.

## 3. Preserve the remaining plan decisions

Keep the following parts of the revised plan.

### Baseline capture

Record existing build and test failures before editing so unrelated failures cannot conceal regressions.

### Configuration-provider pipeline

Use one final provider pipeline with explicit precedence:

1. base JSON;
2. environment-specific JSON;
3. remote configuration;
4. local or instance-specific overrides;
5. environment variables;
6. command-line arguments.

Later providers override earlier providers. Add each provider once. Local or instance-specific values intentionally override remote values.

### Composition contract

Keep only composition-driving primitives in the minimal host contract. Read and validate the contract once. Move normal runtime options to their owning modules.

### Dormant settings

In emulated mode, do not register live-only:

- options;
- validators;
- consumers;
- health checks;
- hosted services that force those options to materialize.

The host must start with live-only configuration sections completely absent.

### Optional email publishing

Preserve the existing reporting-package behaviour:

- the email publisher remains registered when the integration is selected;
- disabled email returns immediately;
- disabled email performs no formatting or network activity;
- email-specific validation is skipped while disabled;
- enabled invalid configuration fails through the existing validation path;
- other publishers remain active.

Do not redesign the reusable package merely to make its internal configuration resemble application-owned runtime options.

### Hosted lifecycle

Move manual post-build setup and external runtime start/stop into one hosted lifecycle adapter when strict ordering is required.

Require:

- no external work in constructors;
- startup only after option validation succeeds;
- deterministic setup-before-start ordering;
- partial-start cleanup where possible;
- idempotent shutdown;
- no manual pre-`RunAsync()` startup call.

### Process failure contract

Keep one top-level fatal boundary that logs and rethrows. Flush Serilog in `finally` before the exception leaves the process.

Do not replace rethrow with a controlled exit code unless the process-supervisor contract is deliberately changed and verified first.

### Dependency-injection validation

Keep `ValidateScopes=true`.

Enable `ValidateOnBuild=true` after application-owned registrations are corrected. If an external registration blocks it, reproduce and document the exact service and exception, then add a focused replacement test. Do not claim a hypothetical blocker.

### Safe startup summary

Log only allow-listed operational metadata after validation succeeds. Do not destructure whole options objects or log credentials, tokens, connection strings, or secret-bearing endpoints.

## 4. Required final verification

The implementation plan must retain tests for:

- all four mode-switch states: absent, false, true, and invalid;
- exact provider precedence;
- one-time composition binding;
- dormant live-only configuration;
- disabled and enabled email publishing;
- bootstrap and final events in one rolling stream;
- file-sink fallback;
- lifecycle start, partial failure, and stop ordering;
- strict DI validation;
- safe startup-summary content;
- fatal flush and hard-failure propagation;
- baseline failures versus new regressions.

## Final instruction to the agent

Do not reopen the boolean-versus-enum decision. The optional boolean and its live default are intentional requirements.

Revise the detailed plan to make the missing-versus-invalid distinction explicit, then perform the bootstrap-path proof before beginning the broader startup refactor. If that proof succeeds, the remaining plan is ready for implementation.
