# Run Reporting Reliability Changes

## Goal

Publish run reports as reliably as the existing delivery facilities reasonably allow,
without allowing reporting failures to fail or replace the service's business outcome.
Keep the solution small: reuse existing delivery behavior where it is already available,
and do not add a reporting actor, a second retry worker, or a new outbox solely for run
reports.

Idempotency, message shape, HTML rendering, recipients, and message-size handling are out
of scope. They are already handled by the consuming service.

## Decisions

### Keep the current collection architecture

Keep the singleton `RunReporter` and its per-async-flow `RunScope`. The reporting state
model does not need a central actor or another stateful service. Reliability belongs at
the publisher/delivery boundary.

### Final publication is independent of request cancellation

Once business processing has completed, cancellation of the original request must not
abandon final report publication. Business steps continue to receive the request token,
but final publication uses `CancellationToken.None`. The publisher must have its own
finite transport timeout.

This change is implemented in this repository by commit `8c22c76`.

### Let `RunReporter` contain publisher failures

A publisher should allow a send or persistence exception to escape from the publisher
implementation. `RunReporter` catches publisher exceptions, logs them, and continues
without failing the business service. Avoid adding another broad catch inside the
publisher that would hide the failure from `RunReporter`.

### Do not simulate cancellation around the external delivery client

If the reviewed delivery client's `SendAsync` has no `CancellationToken`, wrapping the task
in `WaitAsync(cancellationToken)` only stops the caller from waiting; it does not cancel
the underlying submission or persistence fallback. Await `SendAsync` directly and rely on
the configured `HttpClient.Timeout` to bound the attempt.

### Retry is conditional on persistence being inexpensive to enable

Using a direct delivery client alone does not enable a persistence/retry path. In the
reviewed consuming service, the client is built directly and no message persister is
attached. A failed submission is therefore logged by `RunReporter` and then lost.

Enable an existing persistence path only when all of these prerequisites already exist or
are a small configuration change:

- the consuming service already has a compatible database manager/connection;
- the failure-message schema and supporting database objects are already deployed or owned
  by an existing deployment;
- the persistence-enabled registration can target the database scanned by the existing
  retry service;
- no new database, hosted retry worker, cross-service deployment, or operational platform
  is required;
- the existing configurable HTTP timeout can be retained.

If those prerequisites are present, wire the message persister through the package's
supported registration helper and add focused tests. This is a small reuse of existing
infrastructure, not a new reporting subsystem.

If any prerequisite requires substantial new infrastructure, keep direct sending and
structured failure logging. Occasional report loss is preferable to introducing a second
delivery system for this use case.

### Keep retry operations simple

If persistence is enabled, use the retry loop already supplied by the existing delivery
path. Do not add another retry policy to `RunReporting`. A simple metric or alert for
messages that have exhausted the configured retry count is sufficient; do not add a new
dead-letter queue or custom backoff implementation unless operations later demonstrate a
need.

## Changes already satisfied by the reference project

The DataRetriever reference integration already:

- puts `runId` in report attributes;
- turns unexpected non-cancellation exceptions into a failed report issue;
- attempts a failed report before returning the run result;
- contains publisher failures inside `RunReporter`.

The external service handler should also include `runId` in `BeginRun` if that change has
not yet been applied:

```csharp
using IDisposable reportScope = runReporter.BeginRun(
    ("runId", runId.ToString()),
    ("environment", serviceOptions.Value.EnvironmentName));
```

## Minimal verification

Keep tests focused on the integration contract:

1. Final publication receives a non-cancelable token even when business steps received a
   cancelable request token.
2. An unexpected orchestration exception produces a failed report and preserves the
   original business failure.
3. A delivery client exception does not escape `RunReporter.PublishAsync`.
4. The publisher awaits `SendAsync` directly rather than abandoning it with `WaitAsync`.
5. If persistence is enabled, a failed initial submission invokes the configured persister
   and returns according to the supported contract.
6. If persistence itself fails, the failure is logged by `RunReporter` and does not affect
   the business result.

## Prompt for the agent working in the consuming service

```text
Review and make the smallest justified reliability changes to the existing RunReporting
integration in this repository. The publisher has already been switched to an external
delivery client; do not replace it and do not introduce SMTP, a reporting actor, a new
outbox, or another retry worker.

Known facts from the prior review:
- RunReporting currently constructs the external delivery client directly and does not
  attach a message persister.
- The reviewed SendAsync has no CancellationToken.
- EmailRunReportPublisher currently uses SendAsync(...).WaitAsync(cancellationToken), which
  cancels only the wait while the underlying send continues.
- HttpClient.Timeout is configured from RunReportingOptions.SendTimeout (30 seconds by
  default); preserve a finite configurable timeout.
- RunReporter catches and logs publisher exceptions, so reporting failure must not replace
  or fail the business operation.
- External persistence, when configured, stores failed sends for its existing retry
  service.
- Idempotency, payload shape, HTML, recipients, attachments, and size limits are out of
  scope.

Make and verify these changes, using one commit per numbered suggestion:

1. Correct cancellation semantics.
   - Remove WaitAsync(cancellationToken) around SendAsync and await SendAsync directly.
   - Keep the finite HttpClient timeout.
   - Change final report publication in both normal and unexpected-failure handler paths to
     use CancellationToken.None after business processing has completed.
   - Do not change cancellation passed into the business orchestration itself.
   - Add focused tests proving the behavior.

2. Ensure report correlation.
   - Include ("runId", runId.ToString()) in RunReporter.BeginRun attributes if it is not
     already present.
   - Add or update a test proving the published report contains the run ID.

3. Assess and conditionally enable persistence/retry.
   - Inspect the installed package registration helper and the consuming service's existing
     database registrations, schema deployment, configuration, and permissions.
   - Determine whether a message persister can be enabled by a small DI/configuration
     change using an already available database and the existing retry service.
   - If yes, wire the package-supported persistence-enabled client, preserve the configured
     HTTP timeout, and add tests for failed-send persistence and persistence failure.
   - Commit that change separately.
   - If enabling it requires a new database, new schema ownership/deployment, new hosted
     worker, or substantial cross-service work, do not implement it. Keep direct sending
     and RunReporter logging, and document the exact missing prerequisites and why
     persistence would be disproportionate.

4. Add only minimal operational visibility when persistence is enabled.
   - Reuse existing logs/metrics where possible.
   - If no alert exists, add the smallest available signal for FAILED messages whose retry
     count has reached the configured maximum.
   - Do not build a new dead-letter queue or retry algorithm.

Allow delivery exceptions to propagate out of the publisher so RunReporter can log and
contain them. Do not add a catch in the publisher that silently converts failures into
success.

Run the focused tests and the repository's full test suite. Report:
- files changed;
- commits created;
- whether persistence was enabled;
- if not enabled, the concrete prerequisite that made it too large;
- the remaining report-loss window and why it is accepted.
```

## Accepted limitation

Without persistence, a failed submission is logged and the report can be lost. With
persistence enabled, a process crash before the failed send is persisted can still lose
the report. Eliminating that final window requires persist-before-send or a transactional
outbox and is intentionally outside this lightweight design.
