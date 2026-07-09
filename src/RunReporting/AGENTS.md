# RunReporting

A single-project NuGet package for pipeline-style services: collect issues, result tables,
and run attributes from any layer while a run executes, then publish one Razor-templated
HTML email report when the run finishes. Built to be dropped into many company services
(the reference consumer is the DataRetriever solution this repo hosts).

## Requirements (as specified by the owner — do not regress these)

1. **One package, one interface.** Consumers learn `IRunReporter` only — modeled on
   `ILogger`. No scoped handles, no companion interfaces on the consuming side.
2. **Inject anywhere, no run id passing.** `BeginRun(attributes)` sets the ambient run for
   the current async flow via `AsyncLocal` (exactly like `ILogger.BeginScope`); everything
   downstream calls `AddIssue`/`AddTable`/`AddAttribute` with no context arguments.
   `BeginRun` returns an opaque `IDisposable`.
3. **The run concept is optional.** Code that never calls `BeginRun` collects into a
   process-wide default run and can still `PublishAsync()`. The library must not force a
   run id; "run id" is just an attribute (`BeginRun(("runId", id))`).
4. **Report at origin.** Issues/tables/attributes are reported where they happen
   (validators, mappers, persisters), not accumulated in lists and merged at the end.
5. **Runtime never throws.** Every `IRunReporter` member logs-and-degrades on any failure
   (invalid explicit data, throwing table selectors, hostile enumerables, dead SMTP, null
   args). The single
   exception: `PublishAsync` honors the *caller's own* `CancellationToken`. A reporting
   failure must never fail the pipeline it reports on. Cancellation is checked before
   `Take()` (so a pre-cancelled publish does not drain the run), at the report-overload
   boundary, between publishers, and before a successfully published call returns.
6. **Composition time fails fast.** `AddRunReporting(IConfiguration)` requires an
   `EmailReport` config section and validates email settings (host, port 1–65535, parseable
   From and recipients, at least one recipient, positive SendTimeout) — all problems
   aggregated in one `InvalidOperationException`. Validation is skipped when
   `Enabled = false`. Rules live on `RunReportingOptions.GetValidationErrors()`.
7. **Recipients are `';'`-separated strings** (`To`/`Cc`/`Bcc`), e.g.
   `"To": "team@x; ops@x"`. Chosen (after trying arrays) because layered .NET config merges
   arrays index-by-index — an override can never shorten or clear one — while a scalar is
   replaced wholesale by the last layer, so `"To": ""` genuinely clears. Entries are
   trimmed and empties ignored. Do NOT reintroduce array recipients or provider-walking
   "last layer wins" logic; both were tried and rejected by the owner.
8. **Compact variant for Teams.** The org has no Teams webhooks; reports reach Teams via a
   channel's email address, and Teams mangles rich table-HTML. `Options.CompactTo` recipients
   receive a slim rendering (`CompactRunReportEmailTemplate`: outcome, counts, top 10 issues,
   table row counts, no nested layout tables) as a second email with the same subject, while
   To/Cc/Bcc get the full report. `IRunReportFormatter.FormatCompactAsync` is the seam.
   `CompactTo` alone satisfies the recipient requirement. Full and compact formatting/send
   failures are isolated: both variants are attempted, failures are aggregated afterward,
   and only caller-requested cancellation stops before the other variant.
9. **Publishers are a list.** `IRunReportPublisher` is the delivery seam;
   `EmailRunReportPublisher` (SMTP via `System.Net.Mail`, no third-party deps) is the only
   built-in. Extra publishers (a Teams webhook one is anticipated) are plain
   `AddSingleton<IRunReportPublisher, ...>` registrations; every publisher receives every
   report; one failing publisher is logged and never blocks the others.
   `Options.Enabled = false` silences only the email publisher.
10. **Formatting seam.** `IRunReportFormatter` (default `RazorRunReportFormatter`) turns a
   `RunReport` into subject + HTML. Services override the look by supplying their own Razor
   component via `UseRunReportTemplate<T>()` (receives `Report` and `Options` parameters).
   Default subject is `[{ServiceName}] Run {failed (N errors, M warnings) | completed
   with M warnings | succeeded}`; the service prefix is omitted when `ServiceName` is empty.
   `Options.SubjectBuilder` (`Func<RunReport, string?>`) is the full-subject override;
   null/empty/throwing falls back to the generated subject, and CR/LF are always stripped
   (raw newlines make `MailMessage.Subject` throw).
11. **Email template must render in Outlook** (Word engine): nested tables, inline styles,
    `bgcolor` attributes, `mso-` hints only. No flexbox/grid/border-radius/web fonts/margins.
    Layout: hidden preheader, compact navy header band (outcome-colored top accent, "RUN
    REPORT" eyebrow, title, outcome badge — green/amber/red, soft fills with darker text),
    a metadata panel of equal 25%-width columns containing only caller-supplied attributes
    (humanized labels — `runId` → "RUN ID" — four per row), Errors/Warnings panels (white
    background, colored left border, very pale table header), an "All clear" panel for
    clean runs, result tables (navy header), muted footer. No plain-text body (removed on
    request).
12. **Metadata is caller-owned.** Do not append internal timing fields to the template.
    If a consumer wants Started, Completed, Duration, Environment, etc., they pass those
    as run attributes from the host/application code. The DataRetriever host supplies
    `started (ET)` at run start and `completed (ET)` immediately before publish.
13. **Result tables use explicit typed column definitions.**
    `AddTable(title, rows, columns)`: every `TableColumn<TRow>` owns its displayed header,
    typed value selector, alignment, and optional .NET format string. Headers are shown
    **verbatim**, so callers can reword them without touching row types. The selector is the
    complete data binding; there is no reflection, property-name normalization, inferred
    header mapping, dictionary/dynamic-row support, or positional header/format list.
    `TableColumn<TRow>.Left`/`Right`/`Center` select alignment explicitly, while
    `TableColumn<TRow>.Number(..., "N4")` is right-aligned and formats with invariant
    culture. A throwing selector renders `-`; a bad format degrades to the unformatted
    value. Do not reintroduce inferred property binding without the owner.
    Attribute labels in the summary panel are still auto-humanized
    (`ValueFormatter.ToHeader`: `runId` → "RUN ID").
14. **Issue identity is explicit.** A keyed issue is reported with
    `AddIssue(stepName, key, message, data?, severity?)`; its identity is the exact ordinal
    `(StepName, Key)` pair. `RemoveIssues(stepName, key)` removes every matching issue from
    the current run and returns the count. General message-only issues have empty identity
    and are not targetable. `Data` is display-only diagnostic context with one explicit
    shape: `IReadOnlyDictionary<string, string?>`. `IssueData.From(("name", value), ...)`
    is only a named-string-pair convenience; it performs no scalar/list classification,
    reflection, property discovery, or value formatting. Names are case-insensitive,
    blank names are ignored, and the last duplicate wins. `RunReporter` snapshots data on
    receipt, and data never participates in removal.
15. **Outcome** (`Succeeded` / `CompletedWithWarnings` / `Failed`) is derived from collected
    issues; callers may override at publish (`PublishAsync(RunOutcome.Failed)`).
    `PublishAsync` returns the published `RunReport` so callers can shape API responses
    from exactly what was sent. `Take()` drains issues/tables (attributes survive; the
    collection window's start time resets) without sending.

## Architecture

```
src/RunReporting/                     net8.0, Sdk=Microsoft.NET.Sdk.Razor,
├── IRunReporter.cs                   FrameworkReference Microsoft.AspNetCore.App,
├── RunReporter.cs                    zero third-party dependencies.
├── RunReportingOptions.cs            Flat namespace `RunReporting` (folders are physical only).
├── ServiceCollectionExtensions.cs
├── Models/       RunReport, RunIssue, ResultTable, RunOutcome, IssueSeverity,
│                 TableColumn, ColumnAlignment, IssueData, ValueFormatter
├── Formatting/   IRunReportFormatter, RazorRunReportFormatter (HtmlRenderer),
│   └── Templates/  RunReportEmailTemplate.razor, CompactRunReportEmailTemplate.razor,
│                   IssuesTable.razor, ResultTableView.razor
└── Publishing/   IRunReportPublisher, EmailRunReportPublisher
```

- `RunReporter` is the singleton. Ambient run = `AsyncLocal<RunScope?>`; `CurrentRun`
  falls back to `_defaultRun`. `RunScope` (private) holds lock-guarded attribute/issue/table
  state; `Dispose` is idempotent, only pops the ambient chain if still current, and skips
  any ancestors already disposed out of order rather than restoring them.
- Attribute copying (`CopyAttributes`) is entry-by-entry: empty names logged+skipped,
  case-colliding keys last-wins, a throwing source keeps what was read — one bad attribute
  never costs the set.
- DI (`AddRunReporting`): configured options replace any pre-registered `RunReportingOptions`,
  `AddLogging()` makes bare hosts work, `TryAddSingleton` registers formatter + reporter, and
  `TryAddEnumerable` registers the email publisher idempotently.
- SMTP send is bounded by `Options.SendTimeout` via a linked CTS
  (`SmtpClient.Timeout` does not apply to `SendMailAsync`). Full and compact delivery are
  independent best-effort attempts; any failures are reported together after both.

## Consumer quick start

```csharp
services.AddRunReporting(configuration);            // requires "EmailReport" section
// appsettings: { "EmailReport": { "Enabled": true, "Host": "...", "Port": 25,
//                "From": "svc@x", "To": "team@x; ops@x", "ServiceName": "My Service" } }

using var run = reporter.BeginRun(("runId", id), ("environment", env));
reporter.AddIssue("Step3", id, "Rate missing",
    IssueData.From(("currency", ccy), ("id", id)));                // Warning by default
reporter.RemoveIssues("Step3", id);                                // Removes every exact match
reporter.AddIssue("fatal", severity: IssueSeverity.Error);
reporter.AddTable("Persisted Records", rows,
    [
        TableColumn<PersistedRow>.Left("INTERNAL ID", row => row.InternalId),
        TableColumn<PersistedRow>.Number("AMOUNT", row => row.Amount, "N2")
    ]);
await reporter.PublishAsync(failed ? RunOutcome.Failed : null);
```

## Reference integration (DataRetriever, in this repo)

- `DataRetrievalOrchestrator`: `BeginRun(("runId", ...))` at the top, one
  `PublishAsync(status == Failed ? RunOutcome.Failed : null)` at the bottom; API returns
  only `{ runId, status }` (`DataRetrievalRunResult`) — the email *is* the report.
- `StepRunner` bridges keyed fatal errors carried in `StepExecutionResult.Issues` into the
  reporter (ordinary warnings are origin-reported by validators/mappers directly).
- `Step4Persister` adds the "Persisted Records" table at origin.
- Simulator/dev mode: `EmailReport.Enabled` drives whether email actually sends
  (smtp4dev on localhost:2525 locally).

## Known accepted limitations (deliberate, revisit only with the owner)

- Ambient context flows only *downward* from `BeginRun`; work scheduled before/outside the
  run's async flow lands in the default run (logged at origin, never published in apps that
  always use scopes — accepted; a warning/cap was considered and deferred).
- Publishers run sequentially; concurrency to be decided when a second publisher exists.
- Table selectors or cell values that fail to evaluate/format render `-`/type-name; never throw.
- Package version/metadata in `RunReporting.csproj` is `0.2.0` — bump before publishing.

## Testing

Package-focused tests live in `tests/DataRetriever.Tests/RunReporting/RunReporterTests.cs`
(run `dotnet test`, use `-c Release` if a debugger holds Debug outputs). Coverage includes:
ambient nesting/isolation across async, out-of-order scope disposal, default-run mode,
attribute salvage on collisions, `""` recipient blanking, never-throw guarantees (throwing
table selectors, null args, publisher failures, internal-timeout OCE containment), explicit
named issue data and snapshotting, caller
cancellation without pre-draining and between-publisher cancellation, exact step+key issue
removal, subject builder (custom/fallback/CRLF), explicit typed table selectors, alignment
flow, isolated full/compact email failures, Razor rendering + HTML
encoding, and composition-time validation failures.
