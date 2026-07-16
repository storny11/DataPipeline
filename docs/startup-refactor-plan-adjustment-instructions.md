# Instructions for Revising a .NET Service Startup Refactor Plan

## Purpose

Revise the existing startup and configuration refactor plan before implementation. Preserve the useful architectural direction, but remove unnecessary changes, resolve contradictory decisions, and make every failure mode testable.

This task is plan editing only. Do not change production code while revising the plan.

Write the revised plan using generic component names. Do not include organization, platform, service, namespace, infrastructure product, or environment-specific identifiers.

## Desired outcome

The revised plan must describe a .NET service that:

- has a small composition root;
- builds configuration once;
- distinguishes composition-driving values from runtime options;
- binds and validates each active configuration value exactly once;
- fails before application work starts when active configuration is invalid;
- does not validate dormant configuration for disabled components;
- preserves the process supervisor's required failure signal;
- records early and normal failures in one durable rolling application log;
- starts and stops externally managed components through the host lifecycle;
- does not expose secrets through startup logging;
- has explicit acceptance tests for every important startup branch.

## Decisions to preserve

Keep these decisions from the existing proposal:

1. Prefer a clean refactor over compatibility shims when no deployed compatibility requirement exists.
2. Replace an implicit boolean that changes dependency composition with an explicit operating-mode value.
3. Read and validate composition-driving configuration before selecting registrations.
4. Let feature modules own their runtime options, validators, and registrations.
5. Move pre-run manual startup work into hosted lifecycle services.
6. Replace whole-object configuration logging with a safe, allow-listed startup summary.
7. Restore strict dependency-injection validation after invalid registrations have been corrected.
8. Preserve the required top-level process failure behaviour.

## Required corrections

### 1. Replace the bootstrap-logging decision

Do not propose console-only bootstrap logging unless the process supervisor is proven to redirect, consume, and persist both standard output and standard error. Recording a process exit code is not equivalent to preserving the child process's console output.

Use Serilog's two-stage initialization with one logical rolling application log:

1. Configure a bootstrap logger before builder creation.
2. Write bootstrap events to console and the normal rolling application log.
3. Configure the final logger through the host after configuration and dependency injection are available.
4. Make the final logger use the same log path and file-sink settings.
5. Centralize the shared destination and rolling-file settings in one small logging helper.
6. Do not introduce a separate `startup-.log` when the normal destination can be resolved safely during bootstrap.
7. Do not configure an additional file sink in configuration files when code already owns the file sink; doing both can duplicate events.
8. Bound file size and retention.
9. If the file cannot be opened, create a console-only bootstrap logger and emit a warning. Treat this as degraded fallback behaviour, not the primary deployment strategy.

The revised plan must clearly state:

> There are two logger configuration phases but one durable rolling application-log stream.

The log path must depend only on stable primitive startup inputs, such as one deployment-provided log directory plus a deterministic filename. It must not depend on application options that may themselves be invalid.

If the normal application-log destination genuinely cannot be resolved before host configuration exists, the plan may retain a separate bootstrap file only as a documented exception. It must explain why one shared destination is impossible.

### 2. Keep the top-level process boundary explicit

The plan must show one top-level boundary around builder creation, service registration, host construction, and host execution.

For a service whose supervisor requires an unhandled failure signal, retain this shape:

```csharp
try
{
    // Create, configure, build, and run the host.
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

Do not swallow the exception or convert it into a successful exit. Do not log the same failure in multiple application-owned catch blocks. Framework lifecycle messages may still exist, but the application should emit one intentional top-level fatal event.

If the service contract requires an ordinary nonzero return code rather than an unhandled exception, the plan must state that explicitly. Do not silently change the established process contract.

### 3. Classify configuration before assigning validation

The revised plan must classify every configuration type into one of the following categories.

#### Composition-driving values

These values decide which services are registered. Examples include an operating mode or transport selection.

Rules:

- read once before the affected registrations;
- reject missing, blank, malformed, and unsupported values immediately;
- do not bind the same value again through a second options path;
- register the validated value for runtime consumers only if they need it;
- do not pretend a value is an ordinary runtime option when service composition already depends on it.

Using `Options.Create(validatedValue)` is acceptable for exposing an already validated, immutable composition input through `IOptions<T>`. Do not also call `Bind(...).ValidateOnStart()` for the same type.

#### Normal static runtime options

These options are consumed after the container has been composed and do not select registrations.

Rules:

- bind in the feature module that owns the options;
- verify required-section existence explicitly;
- use data annotations for simple property rules;
- use `IValidateOptions<T>` for conditional, nested, or cross-property rules;
- aggregate independent validation failures where practical;
- call `ValidateOnStart()`;
- inject `IOptions<T>` into consumers;
- never manually call `Get<T>()` in the composition root merely to log or inspect the values.

#### Optional component options

These options configure a component controlled by an explicit `Enabled` switch.

Rules:

- when disabled, skip requirements that only matter to the inactive component;
- when enabled, validate every required dependent value;
- do not silently turn an enabled component off because its configuration is invalid;
- document whether a missing section means disabled or invalid;
- prefer an explicit `Enabled=false` when accidental omission would hide an expected capability.

#### Dormant mode-specific options

These options are needed only in one operating mode.

Rules:

- register and validate them only when their mode is active;
- an inactive mode must not require placeholder values for unused infrastructure;
- the composition root chooses the active module, while the module owns its configuration binding and validation.

#### Reloadable options

Use `IOptionsMonitor<T>` only when runtime reload is an actual requirement. Do not choose it merely because it is available. The plan must describe how invalid reloaded values are handled before claiming reload support.

### 4. Make the operating mode strict

Replace any composition-changing boolean with an explicit named mode, for example:

```csharp
public enum OperatingMode
{
    Emulated,
    Live
}
```

The revised plan must require:

- a missing value fails;
- a blank value fails;
- an unknown name fails;
- unsupported numeric enum values fail;
- preferably, all numeric representations are rejected so configuration remains readable;
- valid named values select registrations explicitly;
- configuration never silently selects a mode through a default boolean value.

### 5. Remove duplicate binding of composition options

If a host-level options object is required to compose the service, the plan must use one path:

1. read it once;
2. validate it once;
3. register the validated instance for consumers.

Remove any later `AddOptions<T>().Bind(...).ValidateOnStart()` path for the same type. The plan must explicitly prohibit two independently materialized instances of the same active configuration contract.

### 6. Move runtime option ownership into feature modules

The composition root must not directly bind, materialize, validate, or log infrastructure-specific settings.

Each feature registration method should own:

- the configuration-section name;
- binding;
- required-section checks;
- validators;
- `ValidateOnStart()`;
- registrations that consume the options.

The revised plan should use host-facing calls shaped like:

```csharp
services.AddStorage(configuration);
services.AddExternalClient(configuration);
services.AddOptionalEmailPublishing(configuration);
```

The names are illustrative. The important rule is ownership: the module that understands the configuration also validates it.

### 7. Preserve existing optional-email behaviour unless a package redesign is in scope

Before proposing a reporting or email-options refactor, inspect the existing publisher and registration code.

If the implementation already:

- registers the email publisher;
- returns immediately when `Enabled=false`;
- performs no formatting or network work while disabled;
- skips email-specific validation while disabled;
- validates all active email settings while enabled;
- leaves other publishers unaffected;

then the desired feature-switch behaviour already exists. Do not redesign the package solely to make its internals resemble application-owned options.

Registration-time validation is acceptable for a reusable package, especially when the package can be consumed without the .NET Generic Host. Moving validation to `ValidateOnStart()` changes failure timing and can be a package-level behavioural change.

The revised plan should therefore use the following default decision:

```markdown
Keep the existing optional-email feature-switch semantics. Disabled email publishing
returns from the email publisher without affecting the application or other publishers.
Enabled invalid configuration fails through the package's existing validation path.
The bootstrap logger makes an early registration failure durable.
```

Only propose converting the reusable package to `IOptions<T>` when that package redesign is explicitly in scope. If it is in scope, the plan must include:

- API compatibility analysis;
- changed failure timing;
- constructor changes to use `IOptions<T>`;
- a conditional `IValidateOptions<T>` implementation;
- `ValidateOnStart()` integration for hosted consumers;
- support for programmatic configuration delegates;
- tests for non-host consumers;
- versioning and migration notes.

Do not mix a package redesign into a host-startup cleanup without identifying it as a separate workstream.

### 8. Remove unsupported compatibility fallbacks

The revised plan must not simultaneously say "prefer a clean refactor" and preserve undocumented fallback keys.

When no deployed compatibility requirement exists:

- choose one canonical configuration section;
- update all configuration files and documentation;
- remove fallback reads and `PostConfigure` compatibility shims;
- fail clearly when required canonical configuration is missing.

If a fallback is genuinely required, document:

- the existing consumer that requires it;
- precedence when both keys are present;
- how an empty value behaves;
- the removal timeline;
- tests for the migration behaviour.

### 9. Move manual startup into hosted lifecycle services

Replace manual pre-`RunAsync()` component startup with a focused `IHostedService` or `BackgroundService` adapter.

The plan must require:

- constructors perform no external work;
- constructors do not access `options.Value` merely to trigger validation;
- `StartAsync()` runs only after startup option validation succeeds;
- `StopAsync()` handles graceful shutdown;
- shutdown is idempotent;
- partial-start failure is cleaned up when possible;
- cancellation tokens are honoured where supported;
- registration order is intentional when multiple hosted services have dependencies.

Add a lifecycle test with a probe hosted service. Invalid options must throw before the probe starts and before any externally managed work begins.

### 10. Make dependency-injection validation precise

Do not use vague wording such as "enable strict validation where possible" without an acceptance rule.

The target is:

```csharp
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});
```

`ValidateScopes` should remain enabled in every environment.

`ValidateOnBuild` applies to the service provider as a whole; there is no general per-registration exclusion switch. If an external library prevents global build validation:

1. capture the exact failing registration and exception;
2. determine whether the registration can be corrected or deferred safely;
3. document the remaining limitation;
4. add focused resolution and composition tests around that external boundary;
5. do not disable all validation merely to hide an application-owned registration error.

### 11. Keep startup logging safe

Remove logs that destructure complete configuration or options objects.

After validation succeeds, a startup-summary hosted service may log only allow-listed operational metadata, such as:

- application version;
- environment name;
- selected operating mode;
- non-secret service identity;
- enabled or disabled component states.

Never log:

- passwords;
- credentials;
- tokens;
- connection strings;
- complete endpoint configuration containing secrets;
- whole options objects.

Access validated options inside `StartAsync()`, not in the hosted-service constructor.

## Required phase order

Rewrite the implementation phases in this order:

1. Establish the top-level startup skeleton and one-file two-stage logging.
2. Introduce and strictly parse the explicit operating mode.
3. Eliminate duplicate binding of composition-driving host options.
4. Move runtime options and validators into their owning feature modules.
5. Verify and preserve optional-component feature-switch behaviour; separate any reusable-package redesign.
6. Move manual component startup and shutdown into host lifecycle services.
7. Restore strict DI validation or document an exact external blocker with focused tests.
8. Add safe startup-summary logging.
9. Complete the startup, validation, lifecycle, and logging test matrix.

Do not defer architectural contradictions to implementation. Resolve them in the revised plan first.

## Required acceptance-test matrix

The revised plan must include tests for at least the following cases.

### Composition

- missing operating mode fails before affected registrations complete;
- invalid operating mode fails;
- valid emulated mode selects only emulated registrations;
- valid live mode selects only live registrations;
- each composition-driving options object is materialized once.

### Runtime options

- a required active section is missing;
- a required value is blank;
- a malformed URI, duration, enum, or numeric value fails;
- multiple independent semantic errors are reported together;
- valid configuration starts successfully;
- inactive mode-specific settings are not validated.

### Optional email publishing

- disabled publishing needs no email-specific values;
- disabled publishing performs no formatting or network call;
- enabled invalid configuration fails through the documented validation phase;
- enabled valid configuration sends;
- disabling email does not disable other publishers.

### Lifecycle

- invalid startup options prevent the first hosted component from starting;
- valid options start components through the host lifecycle;
- graceful shutdown stops them once;
- partial startup failure does not leave avoidable background work running.

### Logging and process behaviour

- an exception before host construction appears in the rolling application log;
- an options-validation failure during host startup appears in the same log stream;
- no separate startup log is created when the shared destination is available;
- a file-sink failure falls back to console without causing a recursive logging failure;
- fatal events are flushed;
- the process preserves its required non-success failure signal.

### Dependency injection

- valid application-owned registrations pass `ValidateOnBuild` and `ValidateScopes`;
- any documented external validation limitation has a focused replacement test;
- no singleton captures a scoped service.

## Plan-writing rules

The revised plan must:

- describe ownership and observable behaviour, not just filenames;
- identify whether each validation happens during composition or host startup;
- distinguish required behaviour from optional cleanup;
- avoid speculative abstractions and compatibility layers;
- avoid service-specific names in examples;
- avoid changing reusable-package contracts accidentally;
- include acceptance criteria under every phase;
- state all intentional exceptions and their rationale;
- remain implementable in small, verifiable steps.

## Final review checklist

Before returning the revised plan, verify:

- [ ] There is one logical rolling application log for bootstrap and final events.
- [ ] Console persistence is not assumed without evidence.
- [ ] The top-level failure contract is explicit.
- [ ] Composition-driving values are validated once and are not rebound.
- [ ] Runtime options use module-owned binding and `ValidateOnStart()`.
- [ ] Dormant configuration is not validated.
- [ ] Optional components have explicit disabled semantics.
- [ ] Existing optional-email behaviour is not needlessly redesigned.
- [ ] Compatibility fallbacks have evidence or are removed.
- [ ] External work begins only through the host lifecycle.
- [ ] Strict DI validation has a concrete pass condition or an exact documented blocker.
- [ ] Startup summaries contain no secrets or whole options objects.
- [ ] Every important failure branch has an acceptance test.
- [ ] The plan contains no organization- or service-specific terminology.

## Expected deliverable

Return one revised implementation plan that incorporates these corrections. Include:

1. the final architectural decisions;
2. the configuration classification table;
3. ordered implementation phases;
4. acceptance criteria for each phase;
5. the complete test matrix;
6. explicit non-goals and deferred package-level changes.

Do not implement code until the revised plan has been reviewed and approved.
