# Run Reporting Reliability Handoff

## Goal

Collect one report per service run and hand it to the consuming service's existing delivery
path without allowing reporting failures to replace or fail the business outcome. Keep the
design small: reuse existing delivery behavior and do not add a reporting actor, another
retry worker, or a new outbox solely for run reports.

Idempotency, payload shape, HTML, recipients, attachments, message-size handling, and new
collection features are out of scope.

## RunReporting design

`IRunReporter` is scoped. One DI scope owns one reporter and therefore one run. Every
validator, mapper, persister, and orchestrator resolved from that scope receives the same
collector. Parallel run scopes receive different collectors; no `AsyncLocal`, ambient run
chain, run handle, or process-wide default run is involved.

The orchestration boundary:

1. resolves the reporter and workflow from the run's DI scope;
2. adds `runId`, environment, and other metadata with `AddAttribute`;
3. runs the business workflow with the request cancellation token;
4. records unexpected non-cancellation failures as report issues;
5. calls `CompleteAsync(outcome)` exactly once from the normal or failure path.

`CompleteAsync` has no caller cancellation token. It atomically closes collection,
publishes one snapshot, and returns that same snapshot. Concurrent or repeated calls return
the same report without publishing again. Writes after completion are logged and ignored.

Publishers receive `CancellationToken.None` and own their finite transport timeout and
retry behavior. Publisher exceptions escape the publisher itself; `RunReporter` catches and
logs them so they never affect the business outcome.

## Registration

Core registration is delivery-agnostic:

```csharp
services.AddRunReporting();
services.AddSingleton<IRunReportPublisher, ExternalRunReportPublisher>();
```

The built-in SMTP publisher is an explicit opt-in through
`AddSmtpRunReportPublisher(...)`. A service that already has its own publisher should not
register it.

`IRunReporter` must not be injected into a singleton handler. A singleton handler that
creates a nested service scope resolves both the reporter and orchestrator from that scope:

```csharp
await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
var reporter = scope.ServiceProvider.GetRequiredService<IRunReporter>();
var orchestrator = scope.ServiceProvider.GetRequiredService<WorkflowOrchestrator>();

reporter.AddAttribute("runId", runId.ToString());
reporter.AddAttribute("environment", serviceOptions.Value.EnvironmentName);
```

## External publisher behavior

If the external delivery client's `SendAsync` has no `CancellationToken`, do not wrap it in
`WaitAsync(cancellationToken)`: that only abandons the wait while the underlying submission
continues. Await it directly and retain a finite configured `HttpClient.Timeout`:

```csharp
await emailClient.SendAsync(message);
```

Do not add a broad catch inside the external publisher. A send or persistence exception
must reach `RunReporter`, which logs and contains it.

## Retry decision

Using a direct delivery client alone does not enable an external package's persistence/retry
path. In the reviewed service, the client is built directly and no message persister is
attached. A failed submission is therefore logged and then lost.

Enable an existing persistence path only if it is a small reuse of infrastructure that
already exists. All of these should be true:

- the service already has the compatible database manager/connection;
- the failure-message schema and supporting database objects already exist or have an
  established deployment owner;
- persisted rows are written to the database scanned by the existing retry service;
- no new database, hosted worker, or cross-service delivery subsystem is required;
- the existing finite HTTP timeout can be retained.

If these conditions hold, use the package-supported persistence-enabled registration and add
focused tests. If enabling it requires meaningful new infrastructure, keep direct sending
plus structured `RunReporter` logging. That is the deliberate simple fallback.

If persistence is enabled, reuse the existing retry loop. Do not add another retry policy,
custom backoff, or dead-letter implementation to RunReporting. Reuse existing monitoring,
or add only a minimal alert for failed rows that exhausted the configured retry count.

## Minimal verification

1. Different DI scopes resolve different reporters; repeated resolution within one scope
   returns the same reporter.
2. Concurrent/repeated `CompleteAsync` calls publish once and return the same report.
3. Writes after completion do not modify the snapshot.
4. The published report contains `runId`.
5. An unexpected orchestration failure produces a failed report and preserves the original
   business failure.
6. An external publisher exception does not escape `RunReporter.CompleteAsync`.
7. The external publisher awaits `SendAsync` directly and retains the configured HTTP timeout.
8. If persistence is enabled, a failed submission invokes the configured persister; persistence
   failure is logged by `RunReporter` and does not affect the business outcome.

## Prompt for the agent working in a consuming service

```text
Update the existing RunReporting/external-delivery integration to the scoped one-run design
described below. Keep the implementation small and make one commit per numbered change. Do
not add SMTP, an actor, a new outbox, another retry worker, collection limits, idempotency
features, or payload/HTML/recipient changes.

Target RunReporting contract:
- IRunReporter is scoped: one DI scope equals one run.
- Remove BeginRun, Take, ambient AsyncLocal state, the process-wide default run, and the
  public PublishAsync method.
- Keep AddAttribute, AddIssue, and AddTable.
- Add Task<RunReport> CompleteAsync(RunOutcome? outcome = null).
- CompleteAsync closes collection and publishes once. Concurrent/repeated calls return the
  same report; later writes are logged and ignored.
- CompleteAsync exposes no caller cancellation token. IRunReportPublisher still receives a
  token, but RunReporter passes CancellationToken.None.
- Publisher exceptions are logged and contained by RunReporter.

Known external integration facts:
- The publisher already uses an external delivery client; keep it.
- The reviewed SendAsync has no CancellationToken.
- The current publisher uses SendAsync(...).WaitAsync(cancellationToken), which cancels only
  the wait while the underlying send continues.
- HttpClient.Timeout is configured from RunReportingOptions.SendTimeout; preserve a finite
  configurable timeout.
- The current direct client construction does not attach a message persister.
- External persistence, when configured, stores failed sends for an existing retry service.
- Idempotency, payload shape, HTML, recipients, attachments, and size limits are out of
  scope.

1. Migrate the reporter lifecycle.
   - Implement the scoped IRunReporter/CompleteAsync contract above.
   - Register IRunReporter as scoped.
   - Add focused tests for scope isolation, one-time completion, and ignored late writes.
   - Commit the API, implementation, and required test migration together so the commit
     builds independently.

2. Migrate the singleton handler correctly.
   - Remove IRunReporter from the singleton handler constructor.
   - Create/use the per-run AsyncServiceScope before resolving reporting contributors.
   - Resolve both IRunReporter and WorkflowOrchestrator from that scope.
   - Add runId and environment through AddAttribute.
   - Run the orchestrator with the request cancellation token.
   - Call reporter.CompleteAsync on normal and unexpected non-cancellation failure paths.
   - Preserve the original business exception/status.
   - Add focused handler tests and commit separately.

3. Make delivery registration explicit.
   - Make AddRunReporting register only the core reporter/formatter services.
   - Register the existing external IRunReportPublisher explicitly in the consuming service.
   - Do not register the built-in SMTP publisher.
   - Add a registration test and commit separately.

4. Correct external delivery cancellation behavior.
   - Remove WaitAsync(cancellationToken) around the external SendAsync call.
   - Await SendAsync directly and preserve the finite HttpClient timeout.
   - Let external delivery exceptions propagate to RunReporter for logging/containment.
   - Add a focused test and commit separately.

5. Assess and conditionally enable external persistence/retry.
   - Inspect the installed helper plus the service's database registrations, schema
     deployment, configuration, permissions, and retry-service database target.
   - If a message persister can be enabled through a small DI/configuration change using
     existing infrastructure, implement the supported persistence-enabled registration,
     preserve the timeout, test failed-send persistence and persistence failure, and commit
     separately.
   - If it requires a new database, schema ownership/deployment, hosted worker, or substantial
     cross-service work, do not implement it. Keep direct sending and structured logging.
     Document the exact prerequisite that made persistence disproportionate.

Run the focused tests and the full repository test suite. Report the files and commits,
whether external persistence was enabled, and the remaining accepted report-loss window.
```

## Accepted reliability boundary

Without external persistence, a failed submission is logged and the report can be lost. With
external failure persistence, a process crash before the failed send is persisted can still
lose the report. Eliminating that window requires persist-before-send or a transactional
outbox and is intentionally outside this lightweight design.
