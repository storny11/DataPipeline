# Prompt: Create a Plan to Adopt the DataRetriever Template Design

Use this prompt with an agent that needs to create an adoption plan for an existing service. The agent should not implement the changes yet. Its job is to inspect the existing service and write a concrete, low-risk plan for moving that service toward the DataRetriever template design.

## Agent Role

You are planning the adoption of the `DataRetriever` service template into an existing .NET service.

Your output must be a practical implementation plan, not a generic architecture essay. The plan will later be followed by a simpler implementation model, so be explicit about projects, folders, files, dependency direction, and migration order.

The existing service may use internal company packages, existing generated clients, existing database helpers, existing logging, existing scheduler/request-bus infrastructure, and existing deployment conventions. Preserve those where appropriate. The goal is not to rewrite working technology integrations; the goal is to organize the service around the DataRetriever template boundaries.

## Template Source to Study First

Before writing the plan, read these files in the DataRetriever template:

- `src/DataRetriever.Api/Program.cs`
- `src/DataRetriever.Api/ServiceCollectionExtensions.cs`
- `src/DataRetriever.Api/Composition/RealAdapterRegistration.cs`
- `src/DataRetriever.Api/Composition/SimulatorAdapterRegistration.cs`
- `src/DataRetriever.Application/ServiceCollectionExtensions.cs`
- `src/DataRetriever.Application/Runs/DataRetrievalOrchestrator.cs`
- `src/DataRetriever.Application/Runs/StepRunner.cs`
- `src/DataRetriever.Application/Runs/RunInstrumentationWriter.cs`
- `src/DataRetriever.Application/Runs/RunReportFinalizer.cs`
- `src/DataRetriever.Application/Step1Load/`
- `src/DataRetriever.Application/Step2Load/`
- `src/DataRetriever.Application/Step3Load/`
- `src/DataRetriever.Application/Step4Persist/`
- `src/DataRetriever.Execution/`
- `src/DataRetriever.Reporting/`
- `src/DataRetriever.Monitoring/`
- `src/DataRetriever.Infrastructure/`
- `src/DataRetriever.Simulators/`
- `tests/DataRetriever.Tests/`

Also read the existing service's main agent/readme/spec files, startup/composition files, current request handler or scheduled job, existing infrastructure clients, existing persistence code, and current tests.

## Required Design Direction

The adoption plan should keep the existing app as close as practical to this structure:

```text
src/<ServiceName>.Api/
  Program.cs
  ServiceCollectionExtensions.cs
  Composition/
    RealAdapterRegistration.cs
    SimulatorAdapterRegistration.cs

src/<ServiceName>.Application/
  ServiceCollectionExtensions.cs
  Runs/
    <ServiceName>Orchestrator.cs
    StepRunner.cs
    RunInstrumentationWriter.cs
    RunReportFinalizer.cs
    SingleRunGuard.cs
    <RunOptions>.cs
  Step1Load/
    Models/
    IStep1SourceClient.cs
    Step1Loader.cs
    Step1Mapper.cs
    Step1Validator.cs
  Step2Load/
    Models/
    IStep2SourceClient.cs
    Step2Loader.cs
    Step2ResponseMapper.cs
    Step2Selector.cs
  Step3Load/
    Models/
    IStep3SourceClient.cs
    Step3Loader.cs
    Step3RequestMapper.cs
    Step3ResponseMapper.cs
    Step3ResponseValidator.cs
  Step4Persist/
    Models/
    IStep4SinkClient.cs
    Step4Persister.cs
    Step4RequestMapper.cs

src/<ServiceName>.Execution/
src/<ServiceName>.Reporting/
src/<ServiceName>.Monitoring/
src/<ServiceName>.Infrastructure/
src/<ServiceName>.Simulators/
tests/<ServiceName>.Tests/
```

Use the same boundaries even if the existing service has different names or more/fewer steps. If the real service naturally has a different number of steps, keep the same pattern and adapt the step folders in order.

## Dependency Rules

Plan around these dependency rules:

- `Api` is the host and composition root. It wires endpoints, health checks, adapter mode, service bus/scheduler hooks, and app lifetime concerns.
- `Application` owns the business flow, orchestrator, vertical slices, step interfaces, source/sink contracts, validators, mappers, and step models.
- `Infrastructure` owns real technology integrations: internal package clients, generated HTTP clients, SQL connections, retry/timeout/auth setup, health checks, and persistence adapters.
- `Simulators` owns local fake implementations of Application source/sink interfaces.
- `Execution` owns reusable step result primitives such as `IStep`, `StepExecutionResult`, `StepIssue`, `DiagnosticContext`, counters, and status.
- `Reporting` owns `RunReport`, report building, email formatting templates, and publisher contracts.
- `Monitoring` owns operational progress/instrumentation abstractions and the in-memory/local implementation.

Do not make `Application` depend on `Infrastructure`. If the current service already does this, the plan must include moving that dependency behind Application-owned interfaces.

## Internal Package Guidance

The existing service may already use internal packages. Do not plan to replace them unless there is a clear reason.

Use this mapping:

- Internal package generated client: keep in `Infrastructure`.
- Internal retry/timeout/client setup: keep in `Infrastructure`, preferably near the client setup.
- Application-specific adapter over an internal package: keep in `Infrastructure/<StepName>/`.
- Application interface that the step depends on: keep in `Application/<StepName>/`.
- DTOs received from or sent to external/internal package APIs: keep in the owning step's `Models/` folder if they are step-specific.
- Internal package domain objects that must not leak into the flow: map them to step output records inside the step mapper.

If an existing loader currently calls an internal package directly from service/business code, plan to split it like this:

```text
Application/<StepName>/
  I<StepName>SourceClient.cs
  <StepName>Loader.cs
  <StepName>Mapper.cs

Infrastructure/<StepName>/
  <StepName>SourceClient.cs
  <StepName>SourceClientOptions.cs
  <StepName>SourceHealthCheck.cs
```

The step loader should call the Application interface. The Infrastructure implementation should call the internal package or generated client.

## Step Model Rules

Keep models simple and step-owned:

- Use `StepNInput` only when a step genuinely needs an explicit input object.
- If a first step has no input, use the template's `NoInput` pattern or document that the step loads run options/configuration internally.
- Use `StepNOutput` as the step result object.
- Use `StepNOutputRecord` for records emitted by a step.
- Use `StepNResponseDto` and `StepNResponseRecordDto` for wire/external response shapes when needed.
- Use `StepNRequestDto` for wire/external request shapes when needed.
- Use at most two mapper classes per step by default:
  - one request mapper if the step sends a request;
  - one response/output mapper if the step maps external data to internal step output.
- Do not create mapper classes that have no real job.

Each output record should carry the identifiers needed by later steps, logging, issue tracking, and reporting. Internal ids should remain available through the flow when they are needed for diagnostics, even if matching to a source response uses external ids.

## Diagnostics, Warnings, Errors, and Logging

Use the template's simple diagnostic model:

- A step issue has step name, severity, message, and diagnostic context.
- Diagnostic context is a small key/value set with identifiers relevant to the row or operation.
- Do not invent issue-code enums unless the existing service already has a strong need for them.
- Do not create a complex identifier object hierarchy.
- Logs and report issues should carry the same useful identifiers where possible.

Plan for warnings and errors to be collected into one issue list, separated by severity. Examples:

- missing optional amount -> warning;
- missing row for a requested external id -> warning or error depending on business rules;
- unrecoverable source failure for the whole step -> error;
- persistence failure -> error.

The plan should clearly say which existing conditions are warnings and which are errors.

## Reporting and Email

If the existing service sends operational emails or needs final reporting, align it with the template:

- Build one `RunReport` from all step results.
- Include all warnings and all errors in the report.
- Include all final persisted rows in the report when the service needs an audit-style email.
- Keep formatting in `Reporting`.
- Keep sending in `Infrastructure`.
- Use Razor email templates only if the email has real layout/table needs; otherwise a simple formatter is acceptable.
- Email sending failure should be logged and should not normally change the business run result after data has already been processed.

If the existing service has an internal email package, plan to use it in `Infrastructure` behind the reporting publisher contract instead of introducing a new SMTP package.

## Monitoring and Progress

Keep monitoring separate from reporting.

Plan for a run-level instrumentation object that can accept values such as:

```csharp
info.AddValue("Status", "Running");
info.AddValue("RowsPersisted", 42);
```

Progress calls are optional per step. If a step has nothing useful to publish, it does not need to call progress directly. The shared step runner can still publish basic step status/counters.

If the existing app uses an internal monitoring package, put the adapter in `Infrastructure` or host composition and keep the Application-facing monitoring interface close to the template.

## Simulators

Simulators are optional for production but useful for local development and tests.

The plan should say:

- which source/sink interfaces need simulator implementations;
- what simulator seed data is required;
- how simulator registration is isolated from real adapter registration;
- how to remove simulators later without changing Application code.

Register either real adapters or simulator adapters for the same source/sink interfaces. Do not register both for the same interface in the same composition path unless there is an explicit named/keyed strategy.

## Orchestrator Style

Keep the orchestrator hand-written and concrete.

The orchestrator should show the business sequence clearly:

```text
start run
execute Step 1
if cannot continue, finish failed report
execute Step 2
if cannot continue, finish failed report
execute Step 3
if cannot continue, finish failed report
execute Step 4
finish final report
```

Do not introduce a generic workflow engine. Do not introduce step descriptors or runtime ordering metadata unless the existing service already has a real dynamic workflow requirement.

Bookkeeping such as issue logging, progress updates, and report finalization can be in small helpers like the template's `StepRunner`, `RunInstrumentationWriter`, and `RunReportFinalizer`.

## Health Checks

Plan health checks by adapter/source/sink, preferably near the matching Infrastructure slice:

```text
Infrastructure/<StepName>/
  <StepName>SourceHealthCheck.cs
```

If the existing app already has health-check conventions from internal packages, preserve those conventions but keep the files aligned with the vertical slice where practical.

## Tests

Keep the initial test layout simple.

Unless the existing service is already large, prefer one test project:

```text
tests/<ServiceName>.Tests/
```

The adoption plan should include tests for:

- orchestrator happy path;
- warning collection;
- error/failed-step behavior;
- mapping behavior for important source responses;
- persistence/report rows included in final report;
- simulator composition if simulators are added;
- email formatter if email reporting is added.

## Output Required From You

Write a markdown adoption plan in the existing service repo, preferably:

```text
spec/template-adoption-plan.md
```

The plan must include these sections:

1. `Current State`
   - Current projects and important files.
   - Current flow from trigger to persistence/reporting.
   - Current internal packages and generated clients used.

2. `Target Structure`
   - Exact project list.
   - Exact folder tree to create or move toward.
   - Mapping from current files to target folders.

3. `Step Mapping`
   - Table mapping current business operations to Step 1, Step 2, Step 3, Step 4, etc.
   - For each step: input, output, source/sink interface, mapper(s), validator(s), diagnostics.

4. `Dependency Boundary Changes`
   - What must move out of Application/business code.
   - Which interfaces Application will own.
   - Which Infrastructure classes will implement them.
   - How internal packages will be kept.

5. `Reporting and Monitoring Plan`
   - How warnings/errors are collected.
   - What final rows are included in reports.
   - Whether email formatting/sending is needed.
   - How progress/instrumentation maps to existing monitoring.

6. `Simulator Plan`
   - Which simulators to create.
   - How simulator registration is isolated.
   - What seed data is needed.

7. `Implementation Order`
   - Small, sequential phases.
   - Each phase should leave the solution buildable when possible.
   - Include expected tests after each phase.

8. `Risks and Open Questions`
   - Only real blockers or uncertainties.
   - Do not fill this with generic risks.

9. `What Not To Change`
   - Existing internal packages to keep.
   - Existing deployment/host conventions to preserve.
   - Existing business behavior that must remain unchanged.

## Style Requirements

- Be concrete and file-oriented.
- Prefer moving toward the template over inventing alternative architecture.
- Keep the design lightweight.
- Do not propose a generic workflow engine.
- Do not split into many test projects unless the existing service size justifies it.
- Do not introduce abstractions without a clear owner and concrete use.
- Clearly mark any optional items.
- When uncertain, propose the smallest change that preserves the DataRetriever dependency direction.

