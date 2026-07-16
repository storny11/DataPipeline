# Response to Startup Refactor Plan Feedback

## Purpose

Use this response to revise the startup-refactor plan before implementation. The feedback identifies legitimate open questions. Resolve them in the plan using the decisions below rather than carrying them into coding.

This document uses generic names intentionally. Keep the revised plan free of organization, product, service, environment, and infrastructure-specific terminology.

## Decision summary

Settle the open decisions as follows:

1. Use one bootstrap-safe rolling application-log path based on `LOG_DIRECTORY`, with `AppContext.BaseDirectory/logs` as the local fallback.
2. Let code own both console and rolling-file Serilog sinks. Configuration files own levels and category overrides only.
3. Use the canonical configuration key `OperatingMode` with named values `Emulated` and `Live`.
4. Do not support the previous boolean composition key unless a real deployed compatibility requirement is demonstrated.
5. Split the current broad host configuration into a minimal composition contract and module-owned runtime options.
6. Read and validate the composition contract once, then expose the validated instance to consumers without rebinding it.
7. Keep the reusable reporting package's current registration-time validation and disabled-publisher behaviour unchanged.
8. Use one hosted lifecycle adapter when instrumentation setup and external runtime start/stop require deterministic ordering.
9. Enable strict DI validation after fixing application-owned registrations. Document an exact external blocker only if one is reproduced.
10. Replace staged configuration mutation with one explicit final provider pipeline and a documented precedence order.

## 1. Shared application-log path

### Decision

Use one logical rolling application log for both bootstrap and final Serilog phases.

Resolve the directory from one stable primitive input:

```text
LOG_DIRECTORY
```

When it is absent, use:

```text
AppContext.BaseDirectory/logs
```

Use a deterministic filename such as:

```text
application-.log
```

The two phases therefore target the same path:

```csharp
var logDirectory = Environment.GetEnvironmentVariable("LOG_DIRECTORY");
logDirectory = string.IsNullOrWhiteSpace(logDirectory)
    ? Path.Combine(AppContext.BaseDirectory, "logs")
    : Path.GetFullPath(logDirectory, AppContext.BaseDirectory);

var logFilePath = Path.Combine(logDirectory, "application-.log");
```

### Rationale

The log destination must be available before application configuration and options validation. Do not parse a broad host-options object merely to discover where early failures should be written.

If deployment requires a per-application or per-instance directory, deployment should supply an already resolved directory through `LOG_DIRECTORY`. This keeps path selection out of the application-options graph and avoids circular startup dependencies.

### Sink ownership

Code owns:

- console sink;
- rolling-file sink;
- rolling interval;
- size limit;
- retention count;
- shared-file setting;
- the common destination helper used by both logger phases.

Configuration files own:

- default minimum level;
- category overrides;
- optional level switches that do not add destinations.

Remove file-sink declarations from JSON configuration. Do not configure the same file sink in both code and JSON.

### Bootstrap enrichment

Bootstrap events do not need every property available to final events. Use only values that are safe and stable before the builder exists, for example:

- entry-assembly name;
- process id;
- machine name when operationally appropriate;
- `LogContext` support.

Do not read or validate application options solely to enrich bootstrap events. The final logger may add validated environment, profile, mode, or service properties later. Both phases can still write to the same file even when early events contain fewer properties.

### Failure behaviour

If the configured file cannot be opened:

1. create a console-only bootstrap logger;
2. emit a warning describing the file-sink failure;
3. continue or fail according to the application's established logging policy;
4. do not recursively retry arbitrary paths.

Console-only logging is degraded fallback behaviour. Do not rely on it as the normal deployment strategy unless the process supervisor is proven to capture and persist child standard output and standard error.

### Acceptance criteria

- A failure before host construction appears in the rolling application log.
- A failure during host startup appears in the same rolling log stream.
- No separate startup log is created when the shared path is available.
- JSON configuration does not declare a second file sink.
- Early events may have fewer properties but remain readable and attributable.
- A file-open failure creates a usable console logger without recursive failure.

## 2. Operating mode name and migration

### Decision

Use this canonical configuration shape:

```json
{
  "OperatingMode": "Emulated"
}
```

Supported names are:

```csharp
public enum OperatingMode
{
    Emulated,
    Live
}
```

The composition root reads this value before selecting registrations.

### Parsing rules

- Missing fails.
- Blank fails.
- Unknown text fails.
- Numeric representations fail, even if they map to a defined enum member.
- Named values may be parsed case-insensitively.
- The error message lists the supported names.
- No mode is selected by an implicit default.

Do not rely on `Enum.TryParse` alone because it accepts numeric representations. Add an explicit named-value check.

### Migration decision

Do not add a fallback from the old boolean key by default. The stated refactor preference is a clean canonical contract, and no deployed compatibility requirement has been established.

Update:

- base application settings;
- environment-specific settings;
- launch profiles;
- deployment examples;
- operator documentation;
- tests.

If a deployed compatibility requirement is later demonstrated, treat fallback support as a bounded migration feature with explicit precedence, warnings, tests, and a removal date. Do not add it speculatively.

### Acceptance criteria

- Missing mode fails before affected registrations complete.
- Invalid text fails with a clear supported-values message.
- Numeric input fails.
- `Emulated` selects only emulated registrations.
- `Live` selects only live registrations.
- The old boolean key is absent from active configuration and code.

## 3. Host composition contract

### Decision

Split the current broad host-options object when it mixes values required for composition with values used only at runtime.

Create a minimal composition contract containing only values required before registrations can be completed. Illustrative fields might include:

- non-secret application identity;
- selected operating mode;
- a required listener port when registration depends on it;
- another primitive selector that directly changes the dependency graph.

Do not place feature-specific endpoints, credentials, retry policies, or transport details in this contract. Those belong to the modules that consume them.

### Binding and validation

The composition contract follows exactly one path:

1. read from the final configuration;
2. validate immediately;
3. select registrations;
4. expose the already validated value to runtime consumers if required.

For consumers that expect `IOptions<T>`, register the validated value with `Options.Create(validatedValue)`.

Do not also register:

```csharp
services.AddOptions<HostCompositionOptions>()
    .Bind(...)
    .ValidateOnStart();
```

That would create a second binding and validation path for a value already used during composition.

### Runtime options

Every non-composition setting moves to its owning feature module. Each module owns:

- section-name constants;
- binding;
- required-section checks;
- data-annotation rules;
- custom `IValidateOptions<T>` validators;
- `ValidateOnStart()`;
- registrations that consume the options.

### Acceptance criteria

- The composition root reads only composition-driving configuration directly.
- The composition contract is materialized and validated once.
- Runtime consumers receive the same validated value.
- Feature-specific runtime options are not members of the composition contract.
- No active options type has both immediate validation and later rebinding.

## 4. Reporting and optional email scope

### Decision

Keep the reusable reporting package's current behaviour unchanged unless a separate package-redesign workstream is explicitly approved.

The expected behaviour is:

```csharp
if (!options.Enabled)
{
    return;
}
```

The email publisher remains registered when email integration is selected. At publish time:

- `Enabled=false` returns from the email publisher;
- no report formatting is performed for email;
- no SMTP client is created;
- no network connection is attempted;
- other publishers remain active.

Validation remains conditional:

- disabled email skips email-specific requirements;
- enabled email validates address, recipients, port, timeout, and other active requirements;
- enabled invalid configuration fails through the existing registration-time validation path;
- the bootstrap logger makes that early failure durable.

### Why validation timing remains unchanged

Registration-time validation is a valid design for a reusable package that may be consumed outside the .NET Generic Host. Moving it to `ValidateOnStart()` would change failure timing, constructor dependencies, consumer tests, and potentially the package API without improving the already-correct enabled/disabled behaviour.

Do not perform that redesign merely to make package internals resemble application-owned runtime options.

### Configuration policy

Prefer an explicit section with an explicit `Enabled` value. This distinguishes an intentionally disabled capability from accidentally missing configuration.

Do not add fallback keys from unrelated configuration sections unless a deployed compatibility requirement is documented.

### Acceptance criteria

- Disabled email requires no email-specific configuration.
- Disabled email performs no formatting or network work.
- Enabled invalid email configuration returns all expected validation failures.
- Enabled valid configuration sends successfully.
- Other publishers are unaffected by the email switch.
- Host code does not manually materialize or validate reporting-package options.
- Reporting-package validation timing remains unchanged.

## 5. Hosted lifecycle shape

### Decision

Use one hosted lifecycle adapter when instrumentation setup and external runtime start/stop belong to the same subsystem and require strict ordering.

Its `StartAsync()` should perform the ordered sequence:

1. access already validated options;
2. register post-build instrumentation that requires the built service provider;
3. start the externally managed runtime;
4. return only after startup has either succeeded or failed clearly.

Its `StopAsync()` should stop the runtime safely and idempotently.

### Why one adapter

Two hosted services rely on registration order for start order and reverse registration order for stop order. That is acceptable for independent services but unnecessarily fragile when one operation must precede another within the same subsystem.

Keep one lifecycle boundary and use private methods or small internal collaborators to separate implementation details.

Use two hosted services only if the operations are genuinely independent. If two are used, the plan must name the ordering contract and test it explicitly rather than relying on incidental registration order.

### Lifecycle requirements

- Constructors perform no external work.
- Constructors do not access `options.Value` to force validation.
- Startup validation completes before `StartAsync()` begins.
- Partial startup failure is cleaned up where possible.
- `StopAsync()` is idempotent.
- Cancellation is honoured where the underlying API supports it.
- The top-level host controls start and stop; no manual pre-`RunAsync()` call remains.

### Acceptance criteria

- Invalid startup options prevent the lifecycle adapter from starting.
- Instrumentation setup occurs before runtime start.
- Runtime start happens once.
- Graceful shutdown stops it once.
- Partial failure does not leave avoidable background work running.

## 6. Strict DI validation blockers

### Decision

Attempt strict validation rather than assuming an external blocker exists.

Use:

```csharp
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});
```

Fix every application-owned lifetime or construction failure exposed by this change.

### External blocker procedure

If strict build validation fails inside an external registration:

1. reproduce the failure with the smallest host-composition test;
2. capture the exact service type and exception;
3. determine whether the registration can be corrected, deferred, or wrapped safely;
4. keep `ValidateScopes=true` regardless;
5. disable `ValidateOnBuild` only if the blocker cannot be resolved within scope;
6. document the exact blocker rather than referring vaguely to external wiring;
7. add focused tests that resolve the application-owned roots surrounding that boundary;
8. create a follow-up item to restore global build validation.

There is no general per-registration exclusion switch for `ValidateOnBuild`. Do not write the plan as though individual registrations can simply be exempted.

### Acceptance criteria

- All application-owned registrations pass strict validation.
- Scoped services are not captured by singletons.
- Any external blocker is reproduced and documented precisely.
- A focused replacement test exists when global build validation must remain disabled.

## 7. Configuration provider order

### Decision

Treat configuration-pipeline cleanup as a dedicated refactor phase, not a collection of small edits.

The final application configuration should have one documented provider sequence. A typical precedence order is:

1. base JSON settings;
2. environment-specific JSON settings;
3. instance-specific JSON settings, if retained;
4. remote or document-backed configuration;
5. environment variables;
6. command-line arguments.

Later providers override earlier providers. Add each provider once. In particular, do not add command-line configuration twice.

### Minimal bootstrap configuration

If a remote provider requires connection information before it can be added, a small bootstrap configuration is permitted solely to locate and configure that provider.

The bootstrap configuration must:

- contain only the primitives needed to locate the remote provider;
- not become a second runtime configuration source;
- not bind application options;
- not be mutated after use;
- feed the remote provider into the one final application configuration pipeline.

“Build once” means the application has one final `IConfiguration` used for composition and runtime. It does not prohibit a deliberately small locator configuration when an external provider cannot otherwise be constructed.

### Alias and derived-value handling

Avoid reading values, writing aliases back into configuration, and rebuilding. Prefer one of:

- canonical keys in every provider;
- a dedicated configuration provider that exposes canonical keys;
- an explicit adaptation object created after final configuration is available.

Derived runtime objects belong in service registration or options configuration, not in repeated configuration mutation.

### Precedence tests

Add tests that prove:

- base values are overridden by environment-specific values;
- instance values override the intended lower-precedence sources;
- remote values participate at the documented precedence point;
- environment variables override file and remote values;
- command-line arguments win last;
- an empty scalar override behaves intentionally;
- the same provider is not added twice.

### Acceptance criteria

- One final configuration graph is used by the application.
- Provider order is documented in code and tests.
- Command-line arguments are added once.
- No helper repeatedly builds and mutates configuration.
- Remote-provider bootstrap needs are isolated from application options.

## 8. Dormant mode-specific settings

### Decision

Do not register live-only options, validators, or consumers in emulated mode.

Merely avoiding direct access in the composition root is insufficient. Search for hidden consumers, including:

- singleton constructors;
- hosted-service constructors;
- factories;
- validators depending on other options;
- startup-summary services;
- health checks;
- instrumentation registration;
- eager service-provider resolution.

### Required proof

Create an emulated-mode host with every live-only section absent. The host must build and start successfully.

Create a live-mode host with the same sections absent. Startup must fail with clear, aggregated validation messages before external work begins.

### Acceptance criteria

- Emulated mode starts without placeholder live-only configuration.
- Live mode validates every active live-only section.
- No hidden consumer forces dormant options to materialize.

## 9. Pre-existing build failures

### Decision

Record the baseline before implementation.

Run the normal build and test commands before editing. If unrelated failures already exist:

1. capture the failing projects and messages;
2. do not attribute them to the startup refactor;
3. avoid expanding scope unless a baseline failure blocks every meaningful verification path;
4. use focused project builds and tests for changed areas where possible;
5. report both the baseline state and the post-change state in the handoff.

Do not hide new failures among known noise. The refactor must introduce no additional build or test failures.

### Acceptance criteria

- Baseline failures are documented before edits.
- Changed projects compile independently where possible.
- Focused startup tests pass.
- The final report distinguishes pre-existing failures from regressions.

## Required changes to the implementation plan

Revise the plan so its phases appear in this order:

1. Capture baseline build and test state.
2. Replace staged configuration mutation with the explicit provider pipeline.
3. Establish one-file two-stage logging and the top-level failure boundary.
4. Introduce strict `OperatingMode` parsing and remove the old boolean key.
5. Split and validate the minimal composition contract once.
6. Move runtime options into feature-owned modules and keep inactive settings dormant.
7. Preserve the existing reporting-package enabled/disabled and validation semantics.
8. Move post-build setup and runtime start/stop into one ordered hosted lifecycle adapter.
9. Enable strict DI validation and handle only reproduced blockers.
10. Replace whole-object startup logs with an allow-listed startup summary.
11. Run the full configuration, lifecycle, DI, logging, and process-failure test matrix.

Each phase must include:

- purpose;
- exact ownership changes;
- files or components likely to change;
- behaviour that must remain unchanged;
- acceptance criteria;
- focused verification commands or tests;
- rollback or deferral notes for any external blocker.

## Non-goals

The revised plan must explicitly exclude:

- redesigning a reusable reporting package without separate approval;
- adding speculative compatibility fallbacks;
- introducing a general-purpose logging-location framework;
- enriching bootstrap logs by binding application options early;
- supporting runtime option reload without a real requirement;
- fixing unrelated baseline build failures unless they block verification;
- weakening validation merely to make startup appear successful.

## Final instruction to the planning agent

Update the plan using the decisions in this response. Do not return the same unresolved questions. If repository evidence disproves one of these assumptions, cite the exact code path or failing test and propose the smallest necessary deviation.

Do not begin implementation until the revised plan:

- names one shared bootstrap-safe log path;
- names one canonical operating-mode key and value set;
- defines the minimal composition contract;
- explicitly preserves reporting-package validation timing;
- chooses the hosted lifecycle shape;
- defines the strict-DI blocker procedure;
- documents final configuration-provider precedence;
- includes acceptance tests for dormant settings and both logging phases.
