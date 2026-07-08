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
   (bad subjects, throwing getters, hostile enumerables, dead SMTP, null args). The single
   exception: `PublishAsync` honors the *caller's own* `CancellationToken`. A reporting
   failure must never fail the pipeline it reports on.
6. **Composition time fails fast.** `AddRunReporting(IConfiguration)` requires an
   `EmailReport` config section and validates email settings (host, port 1–65535, parseable
   From and recipients, at least one recipient, positive SendTimeout) — all problems
   aggregated in one `InvalidOperationException`. Validation is skipped when
   `Enabled = false`. Rules live on `RunReportingOptions.GetValidationErrors()`.
7. **Recipients are arrays** (`To`/`Cc`/`Bcc`), matching the owner's config conventions.
   Layered .NET config merges arrays index-by-index, so an override layer blanks an
   inherited entry with `""` — whitespace entries are always ignored (`Clean`). Do NOT
   reintroduce delimited-string recipients or provider-walking "last layer wins" logic;
   both were tried and explicitly rejected as too complex / wrong shape.
8. **Publishers are a list.** `IRunReportPublisher` is the delivery seam;
   `EmailRunReportPublisher` (SMTP via `System.Net.Mail`, no third-party deps) is the only
   built-in. Extra publishers (a Teams webhook one is anticipated) are plain
   `AddSingleton<IRunReportPublisher, ...>` registrations; every publisher receives every
   report; one failing publisher is logged and never blocks the others.
   `Options.Enabled = false` silences only the email publisher.
9. **Formatting seam.** `IRunReportFormatter` (default `RazorRunReportFormatter`) turns a
   `RunReport` into subject + HTML. Services override the look by supplying their own Razor
   component via `UseRunReportTemplate<T>()` (receives `Report` and `Options` parameters).
   Subject is overridable per service via `Options.SubjectBuilder`
   (`Func<RunReport, string?>`); null/empty/throwing falls back to the default subject, and
   CR/LF are always stripped (raw newlines make `MailMessage.Subject` throw).
10. **Email template must render in Outlook** (Word engine): nested tables, inline styles,
    `bgcolor` attributes, `mso-` hints only. No flexbox/grid/border-radius/web fonts/margins.
    Layout: hidden preheader, outcome-colored top accent (green/amber/red), "RUN REPORT"
    eyebrow, title + outcome pill, two-column summary panel (uppercase humanized attribute
    labels — `runId` → "RUN ID" — plus "STARTED (ET)"), Errors/Warnings panels with counts,
    an "All clear" panel for clean runs, result tables, muted footer. No plain-text body
    (removed on request).
11. **Time zone is fixed to US Eastern** ("STARTED (ET)"), resolving `America/New_York`
    then `Eastern Standard Time`, degrading to UTC (with a "UTC" label) if neither exists.
    Not configurable — this was made configurable once and the owner removed it.
12. **Result tables from plain or anonymous objects; columns derive from the rows.**
    `AddTable(title, rows, alignments?)`: no field list — columns come from the first
    non-null row's public properties in declaration order, headers humanized to spaced
    uppercase (`InternalId` → "INTERNAL ID", same rule as attribute labels). To choose,
    reorder, or rename columns, project rows into anonymous objects
    (`records.Select(r => new { r.Name, r.Email })`). Rows of a different type than the
    first still fill matching cells (property-name match ignoring case/spacing).
    Earlier iterations had an explicit fields array and dictionary/dynamic-row support —
    both were deliberately removed for minimalism; do not reintroduce without the owner.
    Alignment is explicit per column (`ColumnAlignment`, positional, default Left) —
    no type-based auto-alignment.
13. **Issue subjects are flexible:** scalar (→ `id=5`), list (→ `ids=a, b`), dictionary
    (as-is), or object/anonymous (`new { ccy, id }` → one entry per property). Conversion
    lives in `IssueData`; formatting is culture-invariant (`ValueFormatter`).
14. **Outcome** (`Succeeded` / `CompletedWithWarnings` / `Failed`) is derived from collected
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
│                 ColumnAlignment, IssueData, ValueFormatter
├── Formatting/   IRunReportFormatter, RazorRunReportFormatter (HtmlRenderer),
│   └── Templates/  RunReportEmailTemplate.razor, IssuesTable.razor, ResultTableView.razor
└── Publishing/   IRunReportPublisher, EmailRunReportPublisher
```

- `RunReporter` is the singleton. Ambient run = `AsyncLocal<RunScope?>`; `CurrentRun`
  falls back to `_defaultRun`. `RunScope` (private) holds lock-guarded attribute/issue/table
  state; `Dispose` is idempotent and only pops the ambient chain if still current.
- Attribute copying (`CopyAttributes`) is entry-by-entry: empty names logged+skipped,
  case-colliding keys last-wins, a throwing source keeps what was read — one bad attribute
  never costs the set.
- DI (`AddRunReporting`): options instance singleton, `AddLogging()` (so bare hosts work),
  `TryAddSingleton` formatter + reporter, `TryAddEnumerable` email publisher (idempotent).
- SMTP send is bounded by `Options.SendTimeout` via a linked CTS
  (`SmtpClient.Timeout` does not apply to `SendMailAsync`).

## Consumer quick start

```csharp
services.AddRunReporting(configuration);            // requires "EmailReport" section
// appsettings: { "EmailReport": { "Enabled": true, "Host": "...", "Port": 25,
//                "From": "svc@x", "To": [ "team@x" ], "ApplicationName": "My Service" } }

using var run = reporter.BeginRun(("runId", id), ("environment", env));
reporter.AddIssue(new { ccy, id }, "Rate missing", "Step3");        // Warning by default
reporter.AddIssue("fatal", severity: IssueSeverity.Error);
reporter.AddTable("Persisted Records", ["INTERNAL ID", "AMOUNT"], rows,
    [ColumnAlignment.Left, ColumnAlignment.Right]);
await reporter.PublishAsync(failed ? RunOutcome.Failed : null);
```

## Reference integration (DataRetriever, in this repo)

- `DataRetrievalOrchestrator`: `BeginRun(("runId", ...))` at the top, one
  `PublishAsync(status == Failed ? RunOutcome.Failed : null)` at the bottom; API returns
  only `{ runId, status }` (`DataRetrievalRunResult`) — the email *is* the report.
- `StepRunner` bridges fatal errors carried in `StepExecutionResult.Issues` into the
  reporter (ordinary warnings are origin-reported by validators/mappers directly).
- `Step4Persister` adds the "Persisted Records" table at origin.
- Simulator/dev mode: `EmailReport.Enabled` drives whether email actually sends
  (smtp4dev on localhost:2525 locally).

## Known accepted limitations (deliberate, revisit only with the owner)

- Ambient context flows only *downward* from `BeginRun`; work scheduled before/outside the
  run's async flow lands in the default run (logged at origin, never published in apps that
  always use scopes — accepted; a warning/cap was considered and deferred).
- Publishers run sequentially; concurrency to be decided when a second publisher exists.
- Table cell values that fail to read/format render `-`/type-name; never throw.
- Package version/metadata in `RunReporting.csproj` is `0.2.0` — bump before publishing.

## Testing

All package tests live in `tests/DataRetriever.Tests/RunReporting/RunReporterTests.cs`
(52 total in the suite; run `dotnet test`, use `-c Release` if a debugger holds Debug
outputs). Coverage includes: ambient nesting/isolation across async, default-run mode,
attribute salvage on collisions, `""` recipient blanking, never-throw guarantees (throwing
getters, null args, publisher failures, internal-timeout OCE containment), subject builder
(custom/fallback/CRLF), anonymous+typed table rows, alignment flow, Razor rendering + HTML
encoding, and composition-time validation failures.
