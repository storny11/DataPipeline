# DataRetriever Implementation Plan

## Current state

The solution is a new ASP.NET Core minimal API project:

- Solution: `DataRetriever.sln`
- Application project: `DataRetriever/DataRetriever.csproj`
- Current app entry point: `DataRetriever/Program.cs`
- Specification: `spec/spec.txt`

The application currently exposes only a root `Hello World` endpoint, so the implementation can establish the architecture without needing to unwind existing code.

## Goals from the specification

Build a simple prototype service that can later become a real work service. The prototype must demonstrate:

- a serial four-step data retrieval and persistence flow;
- clear vertical-slice organization by step;
- simulator-backed external dependencies kept in one removable `Simulators` directory;
- real application loaders, validators, orchestration, monitoring, and reporting outside the simulator directory;
- one-run-at-a-time execution guard;
- request options for all records, currency filter, or internal id filter;
- reusable operational instrumentation/tracking for sequential multi-step services;
- reusable issue collection and final user-facing reporting;
- detailed diagnostic context in all issues, logs, and reports.

The design should stay lightweight. The aim is a reusable project template for serial step-based services, not a generic workflow engine.

## Core rules versus prototype choices

Use this plan as a template, but keep a clear line between reusable rules and this prototype's example choices.

Core template rules:

- Application owns the step flow, step interfaces, diagnostics, and source/sink contracts.
- Infrastructure owns real technology clients/helpers, transport details, retry/timeout, authentication, and health checks.
- Simulators are removable local replacements for external dependencies.
- Reporting, monitoring, and execution primitives stay separate.
- Issues carry step name, severity, message, and diagnostic context.
- `RunId` is generated internally and is the primary id for one execution.

Prototype/example choices:

- ASP.NET Core minimal API host.
- Synchronous run endpoint that returns the final report.
- One-run-at-a-time guard.
- Four concrete steps named `Step1Load`, `Step2Load`, `Step3Load`, and `Step4Persist`.
- Serial per-input loading in Step 2, one batch request in Step 3, and one persistence request in Step 4.
- Email report publisher and simulator-backed local development.

Future services can change the host surface, execution trigger, number of steps, or delivery channel without changing the core dependency direction.

## Core architectural decisions

### 1. Create the reusable template structure immediately

Do not wait to move reusable pieces into shared libraries later. Create the correct solution structure at the start so this service can be used as a template by another LLM or developer filling in different business logic.

The solution should have separate projects for:

- API hosting and endpoint composition;
- application/business flow and vertical slices;
- real infrastructure adapters;
- reusable sequential-step execution primitives;
- reporting;
- monitoring/operational instrumentation;
- simulators.

This makes the intended boundaries explicit from day one. A future service can copy the structure and replace only the business slices, infrastructure adapters, and simulator data.

### 2. Use vertical slices for the four steps

Each step should own its processor, request/response models, validation, and loader/source wrapper. Shared code should be reserved for concerns that are genuinely reusable across slices:

- sequential execution primitives;
- reporting contracts/builders;
- monitoring/operational instrumentation contracts;
- feature-owned external dependency interfaces.

Real technology adapter implementations belong in `Infrastructure`. Local fake implementations belong in `Simulators`.

Do not move step-specific value objects into shared code just because more than one class in that step uses them. For example, an amount type used only by Step 3 mapping belongs in the Step 3 slice, not in a shared library.

Avoid creating broad generic layers like `Repositories`, `Services`, and `Managers` unless a specific feature needs them.

### 3. Keep infrastructure and simulators behind application-owned interfaces

The application should own interfaces for external dependencies, but it should not reference concrete infrastructure or simulator implementations.

Concrete implementations are split by purpose:

- real external-system adapters live in `Infrastructure`;
- fake/local/in-memory adapters live in `Simulators`;
- the API/Host project is the composition root and chooses which implementations to register.

For example:

- `StepNLoader` is application code only when it contains application decisions such as batching, selecting, merging, or translating source results into step issues.
- `IStepNSourceClient` is the application-owned abstraction for the external source.
- `StepNSourceSimulator` implements `IStepNSourceClient` in `Simulators` for local development.
- `StepNSourceClient` implements `IStepNSourceClient` in `Infrastructure` for the real external system.
- Switching between simulator-backed and real implementations is a dependency-injection/composition choice, not an application-code change.

This is the intended dependency direction for the template: a feature/service project should not import infrastructure. Infrastructure should import the application contracts it implements.

If loading data only requires opening a `SqlConnection`, calling a simple `HttpClient`, or calling a generated Swagger client, treat that concrete caller as a real technology adapter and put it in `Infrastructure` behind a source client interface such as `IStep2SourceClient`. The Application loader remains the `IStep<..., ...>` implementation and owns step orchestration, issue collection, and output shaping.

The real infrastructure adapter may be intentionally thin. For a tiny source, the adapter and concrete client/helper can initially be the same class. If retry, timeout, authentication, generated-client setup, or connection behavior becomes non-trivial, split Infrastructure into:

- an application-facing adapter that implements the Application-owned interface and maps to/from step-local DTOs;
- a concrete client/helper that owns transport, retry/timeout, authentication, generated DTOs, SQL commands, and low-level exceptions.

The concrete client/helper can use:

- `SqlConnection`, Dapper, or another lightweight SQL helper;
- `HttpClient` or a typed HTTP client;
- a generated Swagger/OpenAPI client;
- connection strings and options through `IOptions<TOptions>`;
- a concrete client or helper already configured with retry/timeout policy.

Prefer putting retry, timeout, authentication, and low-level connection behavior in the concrete client/helper setup itself, such as typed `HttpClient` registration, generated-client configuration, SQL/data-access helper configuration, or a tiny Infrastructure client wrapper. Do not put retry in Application loaders, mappers, or validators.

Those details should not leak into Application. Application should see step-local request/response DTOs and issues, not SQL rows, generated-client models, connection strings, or HTTP-specific exceptions.

Simulator implementations should still live in `Simulators`, not in `Infrastructure`, even when the real adapter is very small.

### 4. Keep reporting and monitoring separate

Reporting and monitoring are related, but they should not be the same component.

- Reporting is user-facing. It collects issues and builds the final run report.
- Monitoring is operational. It records live status, counters, timestamps, and progress.
- Both can consume the same step execution results.
- A service should be able to use reporting without monitoring, monitoring without reporting, or both.

The common dependency should be the execution/diagnostics primitives, not a combined "monitoring and reporting" package.

### 5. Treat issues as first-class data

Each step returns output data plus structured diagnostics:

- counters;
- issues with severity;

Warnings are recoverable data-quality or partial-result issues. Errors are flow-stopping step/request failures and make the run fail. A run can be successful with warnings, but not successful with errors.

Issue severity is the source of truth for run outcome and report grouping. Logging should align with it:

- warnings are logged as warnings when operationally useful, but they do not fail the run;
- errors are logged as errors and make the run fail;
- ordinary progress messages can be logs only and should not create issues.

### 6. Use a simple sequential orchestrator

The orchestrator runs Step 1 through Step 4 in order. It collects each step result, updates operational progress, and builds the final report.

It should stop before downstream steps when a required upstream output is unavailable because of an error. Warnings alone should not stop the flow.

Keep the first implementation hand-written and sequential. Do not introduce TPL Dataflow, dynamic step registration, graph execution, or a generic workflow engine for this prototype.

Future services may use a different execution style, including TPL Dataflow, if they need parallelism, bounded capacity, fan-out/fan-in, or richer cancellation behavior. That should not require redesigning monitoring or reporting. The stable contract is the execution facts emitted by each unit of work:

- status;
- counters;
- issues with diagnostic context;
- optional instrumentation values;
- optional output and persisted-record summaries.

Execution style is replaceable; execution facts are stable. A future dataflow block can emit the same `StepExecutionResult`, `StepIssue`, counters, and instrumentation updates that the sequential orchestrator emits today.

## Proposed folder structure

```text
DataRetriever.sln

src/
  DataRetriever.Api/
    DataRetriever.Api.csproj
    Program.cs
    ServiceCollectionExtensions.cs
    Composition/
      AdapterMode.cs
      RealAdapterRegistration.cs
      SimulatorAdapterRegistration.cs
    Endpoints/
      RunDataRetrievalEndpoint.cs
      GetDataRetrievalStatusEndpoint.cs
    Contracts/
      RunDataRetrievalRequest.cs
      RunDataRetrievalResponse.cs

  DataRetriever.Application/
    DataRetriever.Application.csproj
    ServiceCollectionExtensions.cs
    Runs/
      DataRetrievalOrchestrator.cs
      DataRetrievalRunOptions.cs
      SingleRunGuard.cs
    Step1Load/
      Step1Loader.cs
      Step1Mapper.cs
      Step1Validator.cs
      IStep1SourceClient.cs
      Models/
        Step1Input.cs
        Step1Output.cs
        Step1Dto.cs
        Step1OutputRecord.cs
    Step2Load/
      Step2Loader.cs
      Step2ResponseMapper.cs
      Step2Selector.cs
      IStep2SourceClient.cs
      Models/
        Step2Output.cs
        Step2OutputRecord.cs
        Step2ResponseDto.cs
    Step3Load/
      Step3Loader.cs
      Step3RequestMapper.cs
      Step3ResponseMapper.cs
      Step3ResponseValidator.cs
      ExternalId2Normalizer.cs
      IStep3SourceClient.cs
      Models/
        Step3Output.cs
        Step3OutputRecord.cs
        Step3RequestDto.cs
        Step3ResponseDto.cs
        Step3ResponseItemDto.cs
    Step4Persist/
      Step4Persister.cs
      Step4RequestMapper.cs
      IStep4SinkClient.cs
      Models/
        Step4Output.cs
        Step4RequestDto.cs
  DataRetriever.Infrastructure/
    DataRetriever.Infrastructure.csproj
    ServiceCollectionExtensions.cs
    Step1Load/
      Step1SourceClient.cs
      Step1SourceHealthCheck.cs
    Step2Load/
      Step2SourceClient.cs
      Step2SourceClientOptions.cs
      Step2SourceHealthCheck.cs
    Step3Load/
      ServiceCollectionExtensions.cs
      Step3SourceClient.cs
      Step3ExternalClient.cs
      Step3SourceClientOptions.cs
      Step3SourceHealthCheck.cs
    Step4Persist/
      Step4SinkClient.cs
      Step4SinkHealthCheck.cs
    Reporting/
      EmailRunReportPublisher.cs
      EmailRunReportOptions.cs

  DataRetriever.Execution/
    DataRetriever.Execution.csproj
    ServiceCollectionExtensions.cs
    RunContext.cs
    IStep.cs
    StepExecutionResult.cs
    StepExecutionStatus.cs
    StepIssue.cs
    StepIssueSeverity.cs
    StepCounter.cs
    DiagnosticContext.cs
    NoInput.cs
    NoOutput.cs

  DataRetriever.Reporting/
    DataRetriever.Reporting.csproj
    ServiceCollectionExtensions.cs
    RunReport.cs
    RunReportBuilder.cs
    IRunReportPublisher.cs

  DataRetriever.Monitoring/
    DataRetriever.Monitoring.csproj
    ServiceCollectionExtensions.cs
    IInstrumentationInfo.cs
    IRunInstrumentation.cs
    ProcessingRunStatus.cs
    ProcessingRunSnapshot.cs
    IProcessingTracker.cs
    InMemoryProcessingTracker.cs

  DataRetriever.Simulators/
    DataRetriever.Simulators.csproj
    ServiceCollectionExtensions.cs
    Monitoring/
      SimulatedInstrumentationInfo.cs
      SimulatedRunInstrumentation.cs
    Reporting/
      SimulatedEmailRunReportPublisher.cs
    Step1Load/
      Step1SourceSimulator.cs
    Step2Load/
      Step2SourceSimulator.cs
    Step3Load/
      Step3SourceSimulator.cs
    Step4Persist/
      Step4SinkSimulator.cs
    SimulatorSeedData.cs

tests/
  DataRetriever.Tests/
    Api/
    Application/
    Infrastructure/
    Simulators/
    Execution/
    Reporting/
    Monitoring/
```

Because this solution is intended as an LLM-facing template, keep explicit placeholder files for the expected concerns even when the prototype implementation is small. The placeholders teach where future business logic belongs. Treat them as scaffolding guidance: delete or collapse a placeholder when a real service genuinely does not have that concern, not merely to reduce file count during the first implementation pass.

Within each loader/persister slice, keep behavior files at the slice root and put DTOs, inputs, outputs, and internal records under `Models/`. Use one `Models/` folder per slice for consistency; do not split into separate `Dtos/`, `Inputs/`, or `Outputs/` folders unless a real service grows enough to need that.

Use the standard .NET naming pattern `ServiceCollectionExtensions.cs` for service registration extension methods, for example `AddDataRetrieverApplication(...)`, `AddDataRetrieverInfrastructure(...)`, `AddDataRetrieverReporting(...)`, and `AddDataRetrieverMonitoring(...)`. Avoid generic names like `DependencyInjection.cs` in the template.

The Application tree shows `Step1Loader.cs`, `Step2Loader.cs`, and `Step3Loader.cs` as the step implementations. Keep them in Application because they own `IStep<..., ...>` execution, application decisions, issue collection, and output shaping. If a load only needs SQL/HTTP/generated-client access, move that concrete caller to the matching `Infrastructure/<SliceName>/` folder behind the source client interface.

Health checks for external dependencies should live beside the adapter they check, for example `Infrastructure/Step3Load/Step3SourceHealthCheck.cs`. Avoid a single catch-all infrastructure health-check file in the template.

Do not create interfaces for every helper class. Use interfaces for real boundaries and extension points: step contracts, source/sink clients, report publishing, and instrumentation. Keep mappers, validators, selectors, and `RunReportBuilder` concrete unless a real second implementation appears.

### Slice replacement rule

There is no generic `Steps` directory. Slice folders should sit directly under the project that owns that part of the slice.

For this template the slice names are intentionally generic and step-shaped because the specification describes Step 1 through Step 4. They are placeholders, not domain names. In a real service, replace these folders with business names:

- replace `Step1Load` with the real configured-data loading slice name;
- replace `Step2Load` with the real related-data loading slice name;
- replace `Step3Load` with the real enrichment/data loading slice name;
- replace `Step4Persist` with the real persistence/publication slice name.

Use the same slice folder name across `Application`, `Infrastructure`, `Simulators`, and tests when that slice has code in those projects. For example, if a real slice is named `ConfiguredDataLoad`, the folders should be:

```text
DataRetriever.Application/ConfiguredDataLoad/
DataRetriever.Infrastructure/ConfiguredDataLoad/
DataRetriever.Simulators/ConfiguredDataLoad/
tests/DataRetriever.Tests/Application/ConfiguredDataLoad/
tests/DataRetriever.Tests/Infrastructure/ConfiguredDataLoad/
tests/DataRetriever.Tests/Simulators/ConfiguredDataLoad/
```

This gives the template a vertical-slice feel without teaching future services to keep artificial names like `Steps`.

The existing `DataRetriever` web project can either be renamed/moved to `src/DataRetriever.Api` or kept as the API project with the same internal folder shape. Because the solution is new, moving to `src`/`tests` early is preferred.

## Public API shape

### Run endpoint

Use a single controller/minimal API endpoint to trigger the flow:

```http
POST /api/data-retrieval/runs
```

Request:

```json
{
  "currency": "GBP",
  "internalIds": null
}
```

Internal-id filter example:

```json
{
  "currency": null,
  "internalIds": "BARC:L, DKE:DD,D:KKKX"
}
```

Rules:

- `currency` omitted/null and `internalIds` omitted/null means run all configured records.
- `currency` provided means filter by currency.
- `internalIds` provided means filter by those internal ids. The API contract accepts this as a comma-separated string and splits it at the API boundary before creating application run options.
- Reject requests that provide both `currency` and `internalIds` to match the spec's "currency only" and "internal ids only" modes.
- Normalize currency by trimming and uppercasing.
- Normalize internal ids by trimming, removing duplicates, and preserving a stable order for reports.

Responses:

- `200 OK` when the run executes and returns a report.
- `400 Bad Request` for invalid filter options.
- `409 Conflict` when another run is already in progress.
- `500 Internal Server Error` only for unexpected unhandled application failures. Expected step failures should be captured in the report and reflected in run status.

The prototype can run synchronously inside the request and return the final report. If the real service later needs background execution, the same orchestrator can move behind a queue or hosted service.

The HTTP endpoint is the prototype host surface, not a core template rule. The same Application orchestration can be triggered by an API endpoint, hosted service, scheduler, message consumer, or test harness.

### Status endpoint

Add a simple endpoint for operational progress:

```http
GET /api/data-retrieval/status
```

Returns the current or last `ProcessingRunSnapshot`.

The reusable monitoring store should support lookup by `runId`. The prototype endpoint can return latest/current because `SingleRunGuard` allows only one active run.

## Run guard

Implement `SingleRunGuard` with `SemaphoreSlim(1, 1)`:

- `TryEnterAsync` attempts immediate acquisition.
- If acquisition fails, return `409 Conflict`.
- Release in a `finally` block.

The app prototype allows one run at a time. The reusable framework pieces should not require that assumption.

Keep the one-run guard in the API/Application flow, but keep reusable primitives run-scoped:

- monitoring/instrumentation is keyed by `RunId`;
- `latest` status is a convenience query, not the only possible status lookup;
- in-memory reusable monitoring stores should be thread-safe;
- simulators should be stateless, per-run, or internally synchronized if a future service enables parallel runs.
- dependency-injection lifetimes should be explicit: reusable in-memory stores may be singleton only because they are thread-safe; real infrastructure adapters should use the lifetime required by their underlying client/connection.

Do not add a scheduler, queue, or parallel-run orchestration now. Business-level idempotency, persistence conflicts, and adapter-specific concurrency are service decisions.

## Reusable execution, reporting, and monitoring design

### Project dependency rules

Use the reusable projects as separate building blocks:

- `DataRetriever.Execution` contains the common step contract, run context, counters, and issue primitives.
- `DataRetriever.Reporting` references `DataRetriever.Execution` and builds user-facing reports from step results and issues.
- `DataRetriever.Monitoring` references `DataRetriever.Execution` and tracks operational status/progress from counters and issue counts.
- `DataRetriever.Application` references the reusable projects it actually uses.
- `DataRetriever.Reporting` and `DataRetriever.Monitoring` must not reference each other.

Ownership summary:

| Concern | Owner |
| --- | --- |
| Step orchestration and business decisions | Application |
| Step inputs, outputs, mappers, validators, and selectors | Application slice |
| Source/sink interfaces used by steps | Application slice |
| Real SQL/HTTP/generated clients, retry/timeout, auth, health checks | Infrastructure |
| Local fake source/sink implementations | Simulators |
| Step contract, run context, counters, issues | Execution |
| Final report model and builder | Reporting |
| Email/report delivery implementation | Infrastructure or Simulators |
| Live run status and instrumentation | Monitoring |
| Host endpoints, environment selection, DI composition | API/Host |

This means a future service can use:

- execution + reporting only;
- execution + monitoring only;
- execution + reporting + monitoring;
- or only copy the execution pattern and skip both optional packages.

### Step interface

Use a small generic interface from `DataRetriever.Execution`:

Name the interface `IStep<TInput, TOutput>`, not `ISequentialStep`, because the step contract should not leak the current orchestrator's execution style. Sequential execution is an application/orchestrator decision.

```csharp
public readonly record struct NoInput
{
    public static readonly NoInput Value = new();
}

public readonly record struct NoOutput
{
    public static readonly NoOutput Value = new();
}

public interface IStep<TInput, TOutput>
{
    string Name { get; }

    Task<StepExecutionResult<TOutput>> ExecuteAsync(
        TInput input,
        RunContext context,
        CancellationToken cancellationToken);
}
```

Keep `RunContext` reusable and small:

```csharp
public sealed record RunContext(
    Guid RunId,
    DateTimeOffset StartedAt);
```

Application-specific request options should remain in `DataRetrievalRunOptions` and the step input models, then be passed separately to the report builder when the report needs a filter summary.

Use `NoInput` for a true source step that has no business input. Use `NoOutput` only for a command step that genuinely has nothing useful to report downstream. Do not use `NoOutput` for persistence when the final report needs to show successfully persisted records.

Examples:

```csharp
public sealed class Step1Loader
    : IStep<Step1Input, Step1Output>
{
    // Step 1 wraps the run/filter options in a step-local input model.
}

public sealed class Step1LoaderWithoutInput
    : IStep<NoInput, InitialFetchOutput>
{
    // Use this shape when a first step genuinely has no input.
}

public sealed class NotifyRunCompletedStep
    : IStep<RunReport, NoOutput>
{
    // Use this shape only when a step genuinely has no output to report downstream.
}
```

This keeps the pattern reusable for another sequential service without creating a full workflow framework, while avoiding fake request/response DTOs.

### Step result

```csharp
public enum StepExecutionStatus
{
    Succeeded,
    SucceededWithIssues,
    Failed
}

public sealed record StepExecutionResult<TOutput>(
    StepExecutionStatus Status,
    TOutput? Output,
    bool HasUsableOutput,
    IReadOnlyList<StepCounter> Counters,
    IReadOnlyList<StepIssue> Issues)
{
    public bool HasIssues => Issues.Count > 0;
    public bool HasErrors => Issues.Any(issue => issue.Severity == StepIssueSeverity.Error);
}
```

Use helper factories for success, completed, and failed results so steps remain readable:

- `StepExecutionResult.Success(output, counters, issues)`
- `StepExecutionResult.Completed(counters, issues)` for `NoOutput`
- `StepExecutionResult.Failed(issues, counters)`

`HasUsableOutput` avoids ambiguous null handling. An empty collection can still be a valid usable output; a failed upstream call can return no usable output.

### Step warning and error aggregation

Each step should return one merged `Issues` collection. Warnings and errors are distinguished by `StepIssueSeverity`, not by separate result lists.

Internal helpers inside a step can produce their own issues. For example, Step 3 may collect issues from:

- request/response validation, such as no Step 3 source row returned for requested `externalId2 = "BB"`;
- response mapping, such as `amount2` missing or unparseable;
- matching mapped Step 3 source rows back to Step 2 output rows.

The step processor should merge those issues into the single `StepExecutionResult.Issues` collection for that step. Do not expose separate final collections such as `ValidationWarnings`, `MappingWarnings`, and `MatchingWarnings`.

The issue message and diagnostic context should preserve what happened, but the report should still present related issues together under the step that emitted them. For example, a missing source response and an amount mapping problem are both Step 3 warnings and should appear in the same Step 3 warning section.

### Step issue

```csharp
public sealed record DiagnosticContext(
    IReadOnlyDictionary<string, string?> Values);

public sealed record StepIssue(
    string StepName,
    StepIssueSeverity Severity,
    string Message,
    DiagnosticContext Context);
```

`StepIssueSeverity.Error` means the issue should stop downstream flow because the step cannot safely produce usable output. Recoverable per-record or partial-data problems should be warnings, even if the message is serious.

Diagnostic context examples:

- Step 1 may use `internalId`, plus available `currency` and `externalId1`.
- Step 2 may use `internalId` and `externalId1`.
- Step 3 may use `internalId`, `externalId1`, `externalId2`, or any domain-specific composite such as `customIdA` and `customIdB`.
- Step 4 may use whatever ids identify the persisted record.

Use PascalCase names such as `InternalId` and `ExternalId1` in C# records. Use camelCase names such as `internalId`, `externalId1`, `customIdA`, and `customIdB` in JSON, logs, instrumentation values, and report details.

Do not hard-code identifier properties into the reusable execution package. Every service can define the ids that make sense for its records. What matters is that the same simple `DiagnosticContext` shape can be used for structured logs, step issues, report rows, and any instrumentation values that need row-level context.

Status rules:

- `Succeeded`: step completed and produced its expected output with no issues.
- `SucceededWithIssues`: step completed and may have produced partial output, but only warnings occurred.
- `Failed`: step encountered at least one error.

Do not add separate issue constants by default. For this template, the useful issue facts are step name, severity, diagnostic context, and a clear human-readable message.

### Monitoring instrumentation

Model monitoring instrumentation after the internal package shape that will eventually replace the simulator:

```csharp
public interface IInstrumentationInfo
{
    void AddValue<T>(string name, T value);
}

public interface IRunInstrumentation
{
    void AppendInstrumentationInfo(
        string level,
        IInstrumentationInfo info);
}

public interface IProcessingTracker
{
    IRunInstrumentation ForRun(Guid runId);
    Task<ProcessingRunSnapshot?> GetSnapshotAsync(
        Guid runId,
        CancellationToken cancellationToken);

    Task<ProcessingRunSnapshot?> GetLatestSnapshotAsync(
        CancellationToken cancellationToken);
}
```

`ForRun(runId)` returns an instrumentation sink bound to that run. `AppendInstrumentationInfo(level, info)` appends values for a logical level such as `run`, `step1`, `step2`, `step3`, or `step4`.

Example usage:

```csharp
var instrumentation = processingTracker.ForRun(runId);
var info = new SimulatedInstrumentationInfo();

info.AddValue("Status", "Running");
info.AddValue("External Ids Requested", 50);
info.AddValue("Valid Rows Returned", 40);
info.AddValue("Warning Count", 2);

instrumentation.AppendInstrumentationInfo("step3", info);
```

Instrumentation is optional. A loader/persister can skip instrumentation entirely; the final report still comes from `StepExecutionResult` values. The simulator can decide whether repeated values update or append because this behavior will later be owned by the internal instrumentation package.

Prototype implementation:

- `InMemoryProcessingTracker`
- `SimulatedInstrumentationInfo` and `SimulatedRunInstrumentation` live in `DataRetriever.Simulators/Monitoring`;
- unknown run-id lookup returns `null`;
- latest status starts as `NeverRun`, represented by the initial snapshot before any progress is recorded;
- status becomes `Running`, `Success`, or `Failed` when the orchestrator appends run-level instrumentation;
- `LastAttemptedRunStartedAt` and `LastAttemptedRunCompletedAt` update for every run attempt;
- `LastSuccessfulRunCompletedAt` updates only after a successful full run;
- snapshots are immutable read models.

Warnings should not make a run fail by themselves.

### Report builder

`RunReportBuilder` lives in `DataRetriever.Reporting`. Keep it as a concrete service unless a real second report-building implementation appears. It should collect all step results and produce:

- run id (`Guid`);
- started/completed timestamps;
- final status;
- request filter summary;
- generic summary metrics;
- issue counts by severity;
- grouped issues by step and severity;
- zero or more service-specific report tables;
- issue messages with diagnostic context.

The report builder should aggregate structured results, not infer meaning from the orchestrator implementation. It may preserve the configured logical step order for readability, but it should not require results to arrive in that order. A future TPL Dataflow orchestrator should be able to pass block/step results into the same report builder.

The report should be structured JSON for the API response. Plain text or HTML formatting can be handled by a publisher later if a real delivery channel needs it.

The report builder should not update operational status. The processing tracker should not format user-facing reports. The orchestrator may use both, but they remain separate dependencies.

The report should group issues first by step and severity. It should not create separate top-level user-facing report sections for validator issues versus mapper issues. A Step 3 missing-response warning and a Step 3 amount-mapping warning both belong in the Step 3 warning group.

Successfully processed data should be represented as normal report output, not as issues. Use generic report tables so each service can add one or more grids without changing the shared reporting model. For example, this prototype can add a `persisted-records` table, while another service could add `expiring-coupons` and `rejected-records` tables.

```csharp
public sealed record RunReportTable(
    string Name,
    string Title,
    IReadOnlyList<RunReportColumn> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows);

public sealed record RunReportColumn(
    string Key,
    string Header,
    RunReportColumnAlignment Alignment = RunReportColumnAlignment.Left);
```

`RunReport` should include `IReadOnlyList<RunReportTable> Tables`. The run endpoint can return the structured report, and the email publisher should render every table as a grid.

For large reports, keep `RunReport` structured but let publishers choose a practical representation:

- include counts and a small sample in the email/body;
- include the full list in the API response only when reasonable;
- otherwise attach, link, or export the full successful-record list through a service-specific mechanism.

Do not make report-size handling part of the step execution contract.

### Report publishing

Report building and report publishing should be separate.

`DataRetriever.Reporting` should define a small publishing extension point:

```csharp
public interface IRunReportPublisher
{
    Task PublishAsync(RunReport report, CancellationToken cancellationToken);
}
```

Implementation:

- `EmailRunReportPublisher` lives in `DataRetriever.Infrastructure/Reporting`.
- `SimulatedEmailRunReportPublisher` lives in `DataRetriever.Simulators/Reporting` for local runs before the real email dependency is wired.

Publishers should receive the structured `RunReport` and decide what to send. They may filter by step name and severity. For example, an email publisher can send only Step 2 warnings and Step 4 warnings without changing the orchestrator or report builder.

The orchestrator may call `IRunReportPublisher.PublishAsync(report, cancellationToken)` after the report is built. Publishing failures should be tracked in operational logs/instrumentation, but they should not modify or hide the original run report.

## Data model plan

### Step-local model naming

Use light, step-local names inside each slice. The folder name carries the business meaning, so model names do not need to repeat it.

Recommended convention:

- step boundary models: `Step1Input`, `Step1Output`, `Step2Output`, `Step3Output`, `Step4Output`;
- external/wire models: `StepNRequestDto`, `StepNResponseDto`, `StepNResponseItemDto`, or simply `StepNDto` for a direct one-row source DTO;
- internal mapped row models inside outputs: `StepNOutputRecord`;
- mappers: create at most the mapper files the slice actually needs.

A slice should have at most two mapper classes:

- `StepNResponseMapper` when the slice maps source response DTOs into output records;
- `StepNRequestMapper` when the slice must build an outbound source/sink request DTO;
- `StepNMapper` only instead of a specific mapper name when there is one obvious mapping boundary and a more specific name would add no clarity.

Do not create `StepNMapper`, `StepNRequestMapper`, and `StepNResponseMapper` together. For a slice like Step 3, separate `Step3RequestMapper` and `Step3ResponseMapper` are useful because they handle different boundaries. Keep one or two mapper files at the slice root. Add a `Mappers/` folder only if a real slice grows enough mapper-related files that the slice root becomes hard to scan.

Use `StepNResponseItemDto` only when the external response has an envelope plus a collection of items. If the external API returns a flat list directly, use the simpler DTO shape needed by that adapter.

Prefer explicit placeholder files under the slice's `Models/` folder, for example `Models/StepNInput.cs`, `Models/StepNOutput.cs`, `Models/StepNOutputRecord.cs`, `Models/StepNRequestDto.cs`, and `Models/StepNResponseDto.cs`. This is more useful for another LLM than a compact catch-all `StepNModels.cs`, because each file names the concern it is meant to replace. Use a grouped `Models/StepNModels.cs` only for truly tiny helper records that do not map to a boundary, DTO, or internal row model.

Application-owned source interfaces should expose step-local DTOs, not generated-client models or SQL-specific row shapes. A real infrastructure adapter can use private/generated DTOs internally, but it should map them to `StepNDto` or `StepNResponseDto` before returning to Application. Then the Application mapper/validator turns those DTOs into `StepNOutputRecord` / `StepNOutput`.

For step handoffs, prefer passing the previous step output directly into the next step when that shape is natural:

```csharp
public sealed class Step3Loader
    : IStep<Step2Output, Step3Output>
{
}
```

Introduce `StepNInput` only when the next step needs a narrower or reshaped input that should not be coupled to the full previous step output.

### Mapping boundaries and mapping issues

Every external or persistence boundary should have an explicit mapper in the relevant vertical slice, including Step 1 and Step 2. Even if the mapper is tiny in the prototype, keep the placeholder because it teaches the source DTO to internal record pattern.

The mapper is responsible for converting from an external/client DTO into the internal domain model used by the next step. The mapper must not silently drop bad data. Any field-level or row-level mapping problem should become a `StepIssue` with the same diagnostic context rules as validation issues.

Default load flow:

```text
source response DTO -> mapper -> StepNOutputRecord / StepNOutput
StepNOutputRecord / StepNOutput -> mapper -> sink request DTO
```

Mapping and validation may be implemented in separate classes or one coordinated pair, but the plan should treat them as separate concerns:

- validation checks whether a request/response is complete and consistent;
- mapping converts valid raw data into the shape required by the next step;
- mapping issues record cases where conversion cannot safely produce the target record.

Examples:

- If a step requests data for an identifier and the source returns no matching row, record an issue with the requested identifier, severity, and a clear message.
- If a source returns a row for `externalId2 = "YY"` but `amount2` is missing, record a warning and discard that row.
- If a source returns `amount2 = "N/A"` or another non-numeric value when the generated client exposes it as a string, record a warning and discard that row.
- If a source returns an identifier that cannot be normalized or matched back to the input/output row being enriched, record a warning with the returned identifier and the reason in the message.
- If persistence requires a request shape that cannot be built from a `Step3OutputRecord`, record a warning before calling persistence with the remaining valid rows.

For this service, Step 3 should produce final valid business records ready for persistence. Step 4 should not need to understand Step 3 source response DTOs or perform Step 3 amount validation.

Do not create overly granular issue taxonomies by default. It is enough for an issue to include step name, severity, diagnostic context, and a clear message.

### Step 1 configured data

Step 1 input:

Use one of these two shapes:

- `IStep<Step1Input, Step1Output>` when the orchestrator/API passes run options into Step 1.
- `IStep<NoInput, Step1Output>` when Step 1 owns loading its run options/configuration from a source.

For the API-driven prototype, `Step1Input` is fine:

```csharp
public sealed record Step1Input(
    DataRetrievalRunOptions RunOptions);
```

```csharp
public sealed record Step1Dto(
    string? InternalId,
    string? ExternalId1,
    string? Currency,
    int Step2RecordsToKeep);
```

Validated configured output row:

```csharp
public sealed record Step1OutputRecord(
    string InternalId,
    string ExternalId1,
    string Currency,
    int Step2RecordsToKeep);
```

Validation rules:

- `InternalId` should be present in the configured source DTO for internal-id filtering and diagnostics. Carry it into downstream output records so every step can log/report against it. If missing, record a warning and exclude from downstream processing for the prototype.
- `ExternalId1` is required for Step 2. Missing means record a data issue and exclude from Step 2.
- `Currency` is required. Missing means record a data issue and exclude from Step 2.
- `Step2RecordsToKeep` must be greater than zero. If missing/invalid in simulator data, warn and either exclude or default. Prefer excluding for the prototype so bad configuration is visible.

Step 1 order must be:

1. Fetch the full configured dataset.
2. Validate the full configured dataset and record configuration issues for all invalid rows.
3. Apply the request filter only to valid rows.
4. Pass the filtered valid rows downstream.

Invalid configured rows should be reported even when they would not have matched the requested currency or internal id filter. The filter controls downstream processing, not whether source configuration defects are visible.

Step 1 output:

```csharp
public sealed record Step1Output(
    IReadOnlyList<Step1OutputRecord> Records);
```

### Step 2 output

```csharp
public sealed record Step2OutputRecord(
    string InternalId,
    string ExternalId1,
    string ExternalId2,
    DateOnly EffectiveDate);

public sealed record Step2Output(
    IReadOnlyList<Step2OutputRecord> Records);
```

The Step 2 source simulator may return more fields, but the selected output should contain only what downstream steps need plus `InternalId` for diagnostics. Use external identifiers for source matching, but carry `InternalId` through every step for logging and reporting.

### Step 3 fetched amount

```csharp
public sealed record Step3OutputRecord(
    string InternalId,
    string ExternalId1,
    string ExternalId2,
    decimal Amount1,
    decimal Amount2,
    decimal Amount3);
```

Step 3 should map the raw Step 3 source response DTOs into validated internal amount data, then match those values back to Step 2 output rows using normalized `externalId2`. The final output rows are `Step3OutputRecord` values and keep `InternalId` for diagnostics.

Use a normalized value object for matching instead of passing raw strings through dictionaries:

```csharp
public readonly record struct NormalizedExternalId2(string Value);
```

`ExternalId2Normalizer` should produce `NormalizedExternalId2` from both requested and returned external ids. Matching dictionaries should use `NormalizedExternalId2` as the key.

Step 3 output:

```csharp
public sealed record Step3Output(
    IReadOnlyList<Step3OutputRecord> Records);
```

Step 2 can produce many output rows for one configured input. That is the expected one-to-many shape of the workflow. Step 3 request/response matching uses `externalId2`; `InternalId` is carried alongside the row for logs, issues, and reports.

Step 4 consumes `Step3Output` when that handoff is natural. Introduce `Step4Input` only if persistence needs a narrower/reshaped input contract.

Step 4 output:

```csharp
public sealed record Step4Output(
    IReadOnlyList<Step3OutputRecord> PersistedRecords);
```

`Step3Output.Records` means the records Step 4 attempted after upstream validation/mapping. `Step4Output.PersistedRecords` means the records confirmed as successfully persisted and available for the final report's persisted-records section.

## Step-by-step implementation plan

### Phase 1: Project foundation

1. Move or rename the existing web project to the API host shape, preferably `src/DataRetriever.Api`.
2. Create class library projects:
   - `DataRetriever.Application`
   - `DataRetriever.Infrastructure`
   - `DataRetriever.Execution`
   - `DataRetriever.Reporting`
   - `DataRetriever.Monitoring`
   - `DataRetriever.Simulators`
3. Add project references:
   - API references Application and Infrastructure by default.
   - API may reference Simulators only for prototype/local simulator mode, and that reference must be isolated to `Composition/SimulatorAdapterRegistration.cs`.
   - Application references Execution, Reporting, and Monitoring for this service because this service uses both.
   - Infrastructure references Application so real adapters can implement application-owned client interfaces.
   - Reporting references Execution.
   - Monitoring references Execution.
   - Simulators references Application so simulator source/sink classes can implement application-owned client interfaces.
   - Simulators references Monitoring and Reporting so local instrumentation/email publisher replacements are isolated in the simulator project.
   - Application does not reference Infrastructure or Simulators.
   - Reporting and Monitoring do not reference each other.
   - API/Host is the composition root and chooses real infrastructure or simulator implementations by environment/configuration.
   - Register either real adapters or simulator adapters for a run mode, never both.
   - Removing the simulator project should require only removing the simulator project reference and the isolated simulator registration branch/file.
4. Add endpoint registration structure to API `Program.cs`.
5. Add dependency-injection extension methods per project.
6. Add a single initial `DataRetriever.Tests` project if test tooling is available in the local SDK, organized internally by folders for API, Application, Infrastructure, Simulators, Execution, Reporting, and Monitoring.
7. Add a lightweight API error response shape for validation and conflict cases.

Expected result:

- App starts.
- Status endpoint returns `NeverRun`.
- Run endpoint exists but can initially return a placeholder until the slices are wired.
- Project boundaries already show what future services should copy and what business logic they should replace.

### Phase 2: Reusable execution, reporting, and monitoring primitives

1. In `DataRetriever.Execution`, implement:
   - `NoInput`
   - `NoOutput`
   - `IStep<TInput, TOutput>`
   - `StepExecutionStatus`
   - `StepIssueSeverity`
   - `StepIssue`
   - `StepCounter`
   - `StepExecutionResult<TOutput>`
   - `DiagnosticContext`
   - `RunContext`
2. In `DataRetriever.Reporting`, implement:
   - `RunReport`
   - `RunReportBuilder`
   - `IRunReportPublisher`
3. In `DataRetriever.Monitoring`, implement:
   - `IInstrumentationInfo`
   - `IRunInstrumentation`
   - `ProcessingRunStatus`
   - `ProcessingRunSnapshot`
   - `IProcessingTracker`
   - `InMemoryProcessingTracker`
4. In `DataRetriever.Simulators`, implement:
   - `SimulatedInstrumentationInfo`
   - `SimulatedRunInstrumentation`
   - `SimulatedEmailRunReportPublisher`
5. Add tests for:
   - no-input and no-output step result helpers;
   - `StepExecutionStatus` selection;
   - warning versus error run-status behavior;
   - `DiagnosticContext` serialization/report/logging context;
   - issue grouping in reports;
   - counter aggregation;
   - instrumentation append behavior in the simulator;
   - monitoring snapshot lookup by run id and latest snapshot lookup;
   - basic concurrent writes to separate run ids in the in-memory processing tracker;
   - status transitions;
   - last attempted run and last successful run tracking;
   - Reporting and Monitoring staying independent from each other.

Expected result:

- Steps can return structured counters/issues.
- The report builder can produce user-facing output without updating operational status.
- The processing tracker can update operational status without formatting user-facing reports.
- The reusable monitoring store is run-scoped even though the prototype allows only one active run.
- Future services can reference only the reusable packages they need.

### Phase 3: Request handling and run guard

1. Implement `RunDataRetrievalRequest`.
2. Implement validation for filter modes.
3. Implement `DataRetrievalRunOptions`.
4. Implement `SingleRunGuard`.
5. Wire `POST /api/data-retrieval/runs` to validate, acquire the guard, and call the orchestrator.
6. Add tests for invalid requests and concurrent run rejection.

Expected result:

- API rejects ambiguous filter options.
- Only one request can execute at a time.
- Reusable monitoring storage remains keyed by run id; the guard is an application policy, not a storage limitation.
- Request options flow into the orchestrator, step input models, and final report summary.

### Phase 4: Step 1 - load and filter configured data

1. Define `IStep1SourceClient`.
2. Implement `Step1SourceSimulator` in `DataRetriever.Simulators` returning about 50 configured records, including intentional scenario rows:
   - valid rows across multiple currencies;
   - missing `externalId1`;
   - missing `currency`;
   - invalid `Step2RecordsToKeep`;
   - rows useful for Step 2 source missing/fewer/more result cases.
3. Add `Step1SourceClient` in `DataRetriever.Infrastructure` when wiring to a real internal source; for the prototype this can be a placeholder or omitted until a real dependency exists.
   - It may directly use `SqlConnection`, `HttpClient`, a generated client, DocumentStore SDK, or another concrete technology.
   - It should map real source rows/models into `Step1Dto` before returning to Application.
4. Implement `Step1Mapper`.
   - It maps `Step1Dto` rows into `Step1OutputRecord` rows.
   - It records mapping issues when a source row cannot be represented as an internal Step 1 record.
5. Implement `Step1Validator`.
6. Implement `Step1Loader` as `IStep<Step1Input, Step1Output>` for the API-driven prototype, or `IStep<NoInput, Step1Output>` if Step 1 owns loading run options/configuration itself.
7. Ensure Step 1 validates and maps the full configured dataset before applying request filters.
8. Record counters:
   - `ConfiguredRowsReturned`
   - `InvalidConfiguredRows`
   - `ValidConfiguredRows`
   - `RowsAfterFiltering`
   - `ValidRowsSelected`
   - `InvalidRowsDiscarded`
9. Record issues with internal id context for every invalid or unmappable configured row, including invalid rows outside the selected filter.
10. Add tests for all-record mode, currency filter, internal id filter, invalid configured rows, unmappable configured rows, and invalid rows outside the selected filter.

Expected result:

- Step 1 always loads the full configured data first.
- Validation happens on the full configured dataset before in-memory filtering.
- Invalid rows are visible in the report and do not break downstream steps.

### Phase 5: Step 2 - serial Step 2 source loading

1. Define `IStep2SourceClient`.
2. Implement `Step2SourceSimulator` in `DataRetriever.Simulators`.
3. Add `Step2SourceClient` in `DataRetriever.Infrastructure` for the real source adapter when a real source exists.
   - If the real source is SQL, this class may directly use `SqlConnection`, Dapper, or the existing lightweight data-access helper.
   - If the real source is HTTP, this class may directly use `HttpClient`, a typed HTTP client, or a generated Swagger/OpenAPI client.
   - Put connection strings, base URLs, credentials mode, and timeout settings in `Step2SourceClientOptions`.
   - Prefer applying retry/timeout policy in the typed/generated client setup or SQL/data-access helper. If there is no separate client/helper yet, keep retry isolated inside Infrastructure and split it out when it grows.
   - Translate transport exceptions into source failures that the step can report.
   - Map SQL rows or generated-client DTOs into step-local DTOs before returning to Application.
4. Implement `Step2ResponseMapper`.
   - It maps `Step2ResponseDto` rows into `Step2OutputRecord` rows.
   - It records mapping issues when a source row cannot be represented as an internal Step 2 record.
5. Implement `Step2Loader` as `IStep<Step1Output, Step2Output>`.
   - Keep it in Application when it coordinates multiple calls, applies per-input continuation rules, or turns partial source results into step issues.
   - If the real loading implementation is only a transport adapter, keep `Step2Loader` as the thin step/orchestration class and move the concrete transport logic behind `IStep2SourceClient` in Infrastructure.
6. Implement `Step2Selector`:
   - sort returned records by date descending;
   - keep the latest `Step2RecordsToKeep`;
   - discard rows missing `externalId2`;
   - warn when fewer rows than requested are available;
   - warn when no rows are returned for a requested `externalId1`.
7. `Step2Loader` processes selected Step 1 rows strictly one after another.
8. Decide failure behavior:
   - per-input Step 2 source call failure where the batch can continue: record a warning with `externalId1`, continue to the next Step 1 row if the shared connection remains usable;
   - catastrophic shared connection failure: record an error and stop Step 2.
9. Record counters:
   - `Step1RowsProcessed`
   - `Step2SourceCallsAttempted`
   - `Step2RowsProduced`
   - `Step2RowsDiscarded`
10. Add tests proving calls are serial, source DTOs are mapped into `Step2OutputRecord`, mapping issues are reported, and selection limits are applied.

Expected result:

- Step 2 output cardinality can differ from Step 1 input cardinality.
- Each missing/failing Step 2 source case is represented in the final report with `externalId1`.

### Phase 6: Step 3 - batch Step 3 source loading

1. Define `IStep3SourceClient`.
2. Implement `Step3SourceSimulator` in `DataRetriever.Simulators`.
3. Add `Step3SourceClient` in `DataRetriever.Infrastructure` for the real source adapter when a real source exists.
   - For the real Step 3 source, assume the first real implementation wraps a generated Swagger/OpenAPI client.
   - The generated client should be registered/configured in Infrastructure and injected into `Step3SourceClient`.
   - Keep generated-client request/response models inside Infrastructure.
   - Map generated-client models or SQL rows into step-local Step 3 DTOs before returning to Application.
   - Put base URLs, connection strings, credentials mode, and timeout settings in `Step3SourceClientOptions`.
4. Implement `ExternalId2Normalizer`.
5. Implement `Step3RequestMapper`.
   - It maps normalized `externalId2` values from `Step2Output` into `Step3RequestDto`.
   - It records mapping issues when an input row cannot be represented in the Step 3 source request.
6. Add reusable retry/timeout handling for the generated Step 3 Swagger client in `DataRetriever.Infrastructure`.
   - Prefer wiring retry into typed `HttpClient`/generated-client registration in `DataRetriever.Infrastructure/Step3Load/ServiceCollectionExtensions.cs`.
   - If the generated client needs a wrapper, create a small `Step3ExternalClient` in Infrastructure that owns retry/timeout behavior and calls the generated client.
   - Keep `Step3SourceClient` as the application-facing adapter that maps between `IStep3SourceClient` and the Infrastructure client.
   - Use `Microsoft.Extensions.Http.Resilience` if the target framework/project already supports it; otherwise use Polly or a small local retry helper in Infrastructure.
   - Retry only transient failures: timeout, network failure, HTTP 408, HTTP 429, and HTTP 5xx.
   - This is appropriate because Step 3 is a read-only/idempotent fetch. Do not copy the same retry policy blindly to non-idempotent write operations without an idempotency key or equivalent protection.
   - Do not retry validation failures, mapping failures, HTTP 400/404-style request/data problems, or cancellation.
   - Make max attempts, delay/backoff, and per-try timeout configurable through `Step3SourceClientOptions`.
   - Keep retry counters/logging in Infrastructure; expose only final success/failure and useful counters to Application if needed.
   - If the generated Swagger client cannot be configured with a resilient `HttpClient`, put retry in the tiny Infrastructure-only `Step3ExternalClient` wrapper.
   - Do not put retry in `Step3Loader` or Application mappers/validators.
   - Keep the generated Swagger client boundary behind `IStep3SourceClient`.
   - Do not hand-code generated Swagger output unless an OpenAPI document is available.
   - Suggested options shape:

```csharp
public sealed class Step3SourceClientOptions
{
    public string BaseUrl { get; init; } = "";
    public TimeSpan PerTryTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public int MaxRetryAttempts { get; init; } = 3;
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);
}
```

7. Implement `Step3ResponseValidator`:
   - match requested and returned external ids after normalization;
   - record a warning with diagnostic context and message when requested data is not returned.
8. Implement `Step3ResponseMapper`:
   - convert Step 3 source response DTOs returned by `IStep3SourceClient` into internal records needed to build `Step3Output`;
   - warn and discard rows missing `amount1`, `amount2`, or `amount3`;
   - warn and discard rows with unparseable or invalid amount values;
   - warn and discard rows whose `externalId2` cannot be normalized;
   - include `internalId`, `externalId1`, and `externalId2` when available in mapping warnings.
9. Implement `Step3Loader` as `IStep<Step2Output, Step3Output>`.
   - Collect issues from `Step3ResponseValidator`.
   - Collect issues from `Step3ResponseMapper`.
   - Collect issues from matching mapped amounts back to Step 2 output rows.
   - Merge all Step 3 issues into one `StepExecutionResult.Issues` collection.
10. Match valid Step 3 amounts back to `Step2Output` rows using `NormalizedExternalId2`.
11. Produce `Step3Output` containing `Step3OutputRecord` rows ready for persistence.
12. Record counters:
   - `ExternalId2ValuesRequested`
   - `Step3RowsReturned`
   - `ValidStep3RowsReturned`
   - `RowsDiscardedDueToMissingAmounts`
   - `RowsDiscardedDueToMappingErrors`
   - `RowsMatchedToStep2Output`
   - `MissingStep3Rows`
13. Add tests for request mapping issues, missing rows for requested external ids, missing amount fields, unparseable amount values, case normalization, failed matches back to Step 2 output rows, retry success, retry exhaustion, no retry for non-transient/request-data failures, and full call failure.
14. Add an aggregation test where one source/validation issue and one mapping issue both appear in the same step result and report section.
15. Add a normalized identifier test proving requested and returned `externalId2` values with casing/format differences match through `NormalizedExternalId2`, not raw strings.

Expected result:

- The Step 3 source is called once per run with the `externalId2` values from Step 2 output.
- Invalid/incomplete/unmappable Step 3 source rows are discarded and reported.
- Step 3 outputs final mapped records matched by external ids while retaining `InternalId` for diagnostics.

### Phase 7: Step 4 - persist final data

1. Define `IStep4SinkClient`.
2. Implement `Step4SinkSimulator` in `DataRetriever.Simulators`.
3. Add `Step4SinkClient` in `DataRetriever.Infrastructure` for the real persistence adapter when a real persistence target exists.
   - It may directly use `SqlConnection`, Dapper, an existing data-access helper, `HttpClient`, or a generated client depending on the real persistence target.
   - Keep connection strings, target names, credentials mode, and timeout settings in infrastructure options.
   - Prefer putting retry/timeout behavior in the concrete persistence client/data-access helper configuration, with the adapter only translating the outcome into step issues.
   - Keep SQL command details, generated-client DTOs, and persistence-specific exceptions inside Infrastructure.
4. Implement `Step4RequestMapper` only if the sink client request shape differs from `Step3OutputRecord`.
   - It maps `Step3OutputRecord` rows into the Step 4 request DTO.
   - It records a warning if a row cannot be represented in the persistence request.
   - It includes internal id, external id 1, and external id 2 in mapping issues.
5. Implement `Step4Persister` as `IStep<Step3Output, Step4Output>` unless a separate `Step4Input` is needed.
6. Persist in a single request.
7. Record counters:
   - `RowsAttemptedForPersistence`
   - `RowsDiscardedDueToPersistenceMappingErrors`
   - `RowsSuccessfullyPersisted`
8. Return `Step4Output` containing the records confirmed as successfully persisted.
9. If Step 4 receives an empty input because upstream steps completed with warnings or legitimate filtering, return a successful result with an empty `PersistedRecords` collection, record zero attempted rows, and do not call persistence.
10. If an upstream error prevented safe output, the orchestrator should stop before calling Step 4.
11. If persistence-request mapping produces issues, include them in the Step 4 result before calling persistence with the remaining valid rows.
12. If persistence fails, record an error with all ids available.
13. If simulator supports partial persistence, record row-level issues while still preserving the single request shape.
14. Add tests for successful persistence, persisted-record output, empty input success, upstream error stops before persistence, persistence-request mapping failure, and persistence failure.

Expected result:

- Final valid records are persisted in one call.
- Step 4 returns the successfully persisted records for the final report.
- Step 4 does not know about Step 3 source response DTOs or Step 3 amount validation.
- Empty Step 4 input is a successful zero-row persistence result unless caused by an upstream error.
- Persistence failure appears in operational status and the final user-facing report.

### Phase 8: Orchestrator integration

1. Implement `DataRetrievalOrchestrator` as a hand-written, concrete orchestrator for this service. Do not introduce dynamic step registration, graph execution, or a generic workflow engine.
2. Create a `RunContext` at the start of each run, including a new internally generated `Guid` run id.
3. Create run-scoped instrumentation with `processingTracker.ForRun(runId)`.
4. Mark processing status as `Running` by appending run-level instrumentation values.
5. Execute steps in order:
   - Step 1 input: `Step1Input` built from run options, or `NoInput.Value` if Step 1 loads run options/configuration itself.
   - Step 2 input: `Step1Output`.
   - Step 3 input: `Step2Output`.
   - Step 4 input: `Step3Output`.
6. After each step:
   - record counters;
   - record step status;
   - record issue counts by severity;
   - log issues with diagnostic context;
   - optionally append step-level instrumentation values for counters and status;
   - retain full step issues for `RunReportBuilder`.
7. Stop downstream execution when a step has errors or no usable output because of an error.
8. Build the final report from all completed step results and Step 4 persisted-record output when Step 4 ran.
9. Publish the final report through `IRunReportPublisher`; use the real email publisher in Infrastructure or the simulated email publisher for local/simulator runs.
10. Mark final status:
   - `Success` if the run completes with no errors;
   - `Failed` if any error occurs;
   - warnings alone still produce `Success` for the run and `SucceededWithIssues` for affected steps.
11. Ensure unhandled exceptions are caught at the orchestrator boundary, tracked as failed, logged, and included in the report as an unexpected error.
12. Add end-to-end tests over simulator data.

Expected result:

- The whole flow executes from one API call.
- Status and report data agree.
- Monitoring and reporting are both driven by the same run, but remain separate dependencies.
- Partial data-quality problems are visible without hiding successful records.

### Phase 9: Logging and report polish

1. Use structured logging for every step start/end.
2. Keep logging, issue collection, reporting, and instrumentation as separate concerns:
   - ordinary operational events may be logs only;
   - every issue should be available to the report builder;
   - important issues should also be logged with the same diagnostic context.
3. Log each issue with:
   - run id;
   - step name;
   - severity;
   - diagnostic context values.
4. Keep report messages human-readable but derive them from structured issue data.
5. Add a summary section to `RunReport`:
   - total configured rows;
   - rows after filtering;
   - Step 2 rows produced;
   - valid Step 3 rows;
   - rows persisted;
   - issue counts by severity;
   - persisted record list or persisted record summary, depending on report size.
6. Consider returning both `summary` and `issuesByStep` in API responses.
7. Configure report publishing:
   - add `EmailRunReportPublisher` in `Infrastructure`;
   - use `SimulatedEmailRunReportPublisher` in `Simulators` for local/simulator runs;
   - keep publisher filtering configurable by step name and severity, for example Step 2 warnings and Step 4 warnings only.

Expected result:

- A support person can understand the run without reconstructing the identifier chain manually.
- Report delivery can be added without changing report building or orchestration logic.

### Phase 10: Final verification

1. Run `dotnet build`.
2. Run all tests.
3. Run the service locally.
4. Exercise:
   - all-record run;
   - currency-filtered run;
   - internal-identifier-filtered run;
   - concurrent request conflict;
   - status endpoint before, during, and after a run;
   - simulator failure scenarios.
5. Confirm the final report includes expected issues and diagnostic context.
6. Confirm simulator removal/replacement would not require moving main application classes.

## Suggested simulator scenario matrix

Seed the simulators with predictable data so manual and automated tests can hit every important branch.

| Scenario | Simulator | Expected behavior |
| --- | --- | --- |
| Valid GBP/EUR/USD records | Internal source | Normal filtering and processing |
| Missing currency | Internal source | Step 1 issue with internal id |
| Missing external id 1 | Internal source | Step 1 issue with internal id |
| Invalid keep count | Internal source | Step 1 issue, row excluded |
| Step 2 source returns no rows | Step 2 source | Step 2 warning for external id 1 |
| Step 2 source returns fewer than requested | Step 2 source | Step 2 warning, keep available rows |
| Step 2 source returns more than requested | Step 2 source | Sort by date, keep configured count |
| Step 2 source row missing external id 2 | Step 2 source | Step 2 warning, discard row |
| Step 2 source call throws | Step 2 source | Step 2 error with external id 1 |
| Step 3 source missing response external id | Step 3 source | Step 3 warning with external id 2 |
| Step 3 source response has different casing | Step 3 source | Normalized match succeeds |
| Step 3 source row missing amount2 | Step 3 source | Step 3 warning, row discarded |
| Step 3 source row has unparseable amount2 | Step 3 source | Step 3 mapping warning, row discarded |
| Step 3 source row cannot be matched to Step 2 output | Step 3 source | Step 3 mapping warning with external ids |
| Step 3 source transient failure then success | Step 3 source | Retry succeeds, report may include retry counter if useful |
| Step 3 source full failure | Step 3 source | Step 3 error, Step 4 is not run |
| Persistence success | Persistence | Step 4 success counters |
| Persistence request mapping fails for one row | Persistence/application mapper | Step 4 mapping issue with ids |
| Persistence failure | Persistence | Step 4 error with ids |

## Testing plan

Create `DataRetriever.Tests` with focused unit tests and a small number of integration tests.

Recommended test groups:

- `ProjectDependencyTests`
- `InfrastructureAdapterTests`
- `SimulatorAdapterTests`
- `StepExecutionResultTests`
- `NoInputNoOutputStepTests`
- `ProcessingTrackerTests`
- `RunInstrumentationTests`
- `RunRequestValidationTests`
- `SingleRunGuardTests`
- `Step1MapperTests`
- `Step1LoaderTests`
- `Step2ResponseMapperTests`
- `Step2LoaderTests`
- `Step3RequestMapperTests`
- `Step3ResponseMapperTests`
- `Step3LoaderTests`
- `Step4RequestMapperTests`
- `Step4PersisterTests`
- `RunReportBuilderTests`
- `ReportTableEmailTests`
- `RunReportPublisherTests`
- `DataRetrievalOrchestratorTests`
- `DataRetrievalApiTests`

Use unit tests for step behavior. Use integration tests only for endpoint wiring, dependency injection, and final response shape.

## Implementation order recommendation

The safest order is:

1. Project split into API, Application, Infrastructure, Execution, Reporting, Monitoring, and Simulators.
2. Execution primitives, including `NoInput` and `NoOutput`.
3. Reporting package.
4. Monitoring package.
5. Request validation and run guard.
6. Step 1 with simulator.
7. Step 2 with simulator.
8. Step 3 with simulator and Infrastructure generated-client retry/client wrapper registration.
9. Step 4 with simulator.
10. Real infrastructure adapter placeholders or implementations where real dependencies exist.
11. Orchestrator and API integration.
12. End-to-end tests and report polish.

This order keeps the diagnostic framework in place before data-flow complexity arrives, which reduces rework.

## Important edge cases to decide explicitly during implementation

1. Whether a row missing `internalId` should be included downstream.
   - Recommendation: record a warning and exclude it because traceability is a core requirement.

2. Whether Step 2 per-input call failures should abort the whole step.
   - Recommendation: continue and record a warning when the failure is isolated to one input; abort and record an error only on shared connection/catastrophic failure.

3. Whether both currency and internal id filters should be allowed together.
   - Recommendation: reject both together for the prototype because the spec lists them as separate modes.

4. Whether a completed run with warnings should be `Success`.
   - Recommendation: yes. Warnings are recoverable; errors drive `Failed`.

5. Whether Step 4 should run when Step 3 returns zero valid rows but no upstream error.
   - Recommendation: return a successful Step 4 result with zero attempted rows, do not call persistence, and return a successful run with warnings if the zero-row outcome came only from recoverable data issues.

## Definition of done

The prototype is complete when:

- The solution has separate API, Application, Infrastructure, Execution, Reporting, Monitoring, and Simulators projects.
- Application does not reference Infrastructure or Simulators.
- Infrastructure and Simulators implement application-owned external dependency interfaces.
- Reporting and Monitoring are separate packages and do not reference each other.
- Reporting and Monitoring consume structured execution facts and do not depend on the orchestrator being sequential.
- Reusable monitoring storage is run-scoped and thread-safe in the in-memory implementation.
- Source-style steps can use `IStep<NoInput, TOutput>`.
- Sink-style steps can use `IStep<TInput, NoOutput>`.
- Step results include explicit `StepExecutionStatus`.
- Step results expose a single `Issues` collection with severity.
- Issues use one consistent `DiagnosticContext` shape for row identifiers.
- `POST /api/data-retrieval/runs` can run all, currency-filtered, and identifier-filtered flows.
- Parallel run attempts receive `409 Conflict`.
- Step 2 processes Step 2 source requests serially.
- Step 3 makes one batch Step 3 source request using normalized `externalId2` values.
- Step 3 real Swagger/generated client retry policy is configured in Infrastructure client registration or a tiny Infrastructure wrapper, not in Application.
- Step 3 matches requested and returned data using normalized identifier value objects rather than raw strings.
- Step 3 maps raw Step 3 source response rows into final valid records and reports mapping issues with ids.
- Step 4 persists valid final records in one request.
- Step 4 returns `Step4Output` with successfully persisted records.
- The final report includes successfully persisted records separately from issues.
- Step 4 treats empty input as a successful zero-row result unless caused by an upstream error.
- Step 4 reports persistence-request mapping failures separately from persistence call failures.
- Simulators live together in `Simulators`.
- Simulators are stateless, per-run, or internally synchronized; they do not rely on a single active run.
- Host composition registers either real adapters or simulator adapters, never both.
- Removing simulator support is limited to removing the simulator project reference and isolated simulator registration.
- Real technology adapters live in `Infrastructure`.
- Feature workflow, processors, validators, mappers, and application-facing ports live in `Application`.
- Operational tracking uses run-scoped instrumentation with `IInstrumentationInfo.AddValue<T>(name, value)` and `IRunInstrumentation.AppendInstrumentationInfo(level, info)`.
- Operational status reports `NeverRun`, `Running`, `Success`, and `Failed` correctly.
- Operational tracking records both last attempted run and last successful run.
- The final report groups issues by step and severity.
- Report publishing is behind `IRunReportPublisher`, with email delivery implemented as an infrastructure adapter and simulated email delivery isolated in `Simulators`.
- Every issue includes relevant diagnostic context.
- Tests cover happy path, filtering, issues, run guard, report generation, and status transitions.
- `dotnet build` and the full test suite pass.
