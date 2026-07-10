# RunReporting

A single-project NuGet package for pipeline-style services: collect issues, result tables,
and run attributes from any layer while a run executes, then publish one Razor-templated
HTML email report when the run finishes. Built to be dropped into many company services
(the reference consumer is the DataRetriever solution this repo hosts).

## Requirements (as specified by the owner — do not regress these)

1. **One package, one interface.** Consumers learn `IRunReporter` only — modeled on
   `ILogger`. No scoped handles, no companion interfaces on the consuming side.
2. **One reporter per run scope, no run id passing.** `IRunReporter` is scoped; the host
   creates or uses one DI scope per run, and every contributor resolved from that scope
   writes to the same reporter. Downstream code calls `AddIssue`/`AddTable`/`AddAttribute`
   with no context arguments. There is no ambient `AsyncLocal` state.
3. **Run metadata is optional.** A scoped reporter starts empty and can complete without
   attributes. The library does not force a run id; callers add it as an ordinary attribute
   with `AddAttribute("runId", id)` when they have one.
4. **Report at origin.** Issues/tables/attributes are reported where they happen
   (validators, mappers, persisters), not accumulated in lists and merged at the end.
5. **Runtime never throws.** Every `IRunReporter` member logs-and-degrades on any failure
   (invalid explicit data, throwing table selectors, hostile enumerables, dead SMTP, null
   args). A reporting failure must never fail the pipeline it reports on. `CompleteAsync`
   has no caller cancellation token; publishers receive `CancellationToken.None` and own
   their transport timeout/retry behavior.
6. **Composition time fails fast.** `AddSmtpRunReportPublisher(IConfiguration)` requires an
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
   built-in and is registered only by `AddSmtpRunReportPublisher`. Core `AddRunReporting`
   is delivery-agnostic. Extra publishers are plain
   `AddSingleton<IRunReportPublisher, ...>` registrations; every publisher receives every
   report; one failing publisher is logged and never blocks the others.
   `Options.Enabled = false` silences only the email publisher.
10. **Formatting seam.** `IRunReportFormatter` (default `RazorRunReportFormatter`) turns a
   `RunReport` into subject + HTML. Services replace this formatter registration when they
   need a different layout; there is no second component-template customization seam.
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
14. **Issue identifiers are explicit and singular.** A record or operation issue is reported
    with `AddIssue(stepName, identifierName, identifierValue, message, severity?)`.
    `IdentifierName` says what the value represents (`InternalId`, `ExternalId2`, `RunId`),
    while `IdentifierValue` identifies the affected record or operation. General message-only
    issues have empty identifier fields. Issues retain collection order and do not carry an
    unused internal timestamp. There is no open-ended issue-data dictionary and no issue-removal
    API: determine record relevance before reporting an issue rather than adding provisional
    issues and deleting them later.
15. **Outcome and completion.** Outcome (`Succeeded` / `CompletedWithWarnings` / `Failed`)
    is derived from collected issues; callers may override it at completion
    (`CompleteAsync(RunOutcome.Failed)`). Completion snapshots and publishes once, then
    returns the exact `RunReport`. Repeated or concurrent completion returns the same report
    without republishing; later writes are logged and ignored.

## Architecture

```
src/RunReporting/                     net8.0, Sdk=Microsoft.NET.Sdk.Razor,
├── IRunReporter.cs                   FrameworkReference Microsoft.AspNetCore.App,
├── RunReporter.cs                    zero third-party dependencies.
├── RunReportingOptions.cs            Flat namespace `RunReporting` (folders are physical only).
├── ServiceCollectionExtensions.cs
├── Models/       RunReport, RunIssue, ResultTable, RunOutcome, IssueSeverity,
│                 TableColumn, ColumnAlignment, ValueFormatter
├── Formatting/   IRunReportFormatter, RazorRunReportFormatter (HtmlRenderer),
│   └── Templates/  RunReportEmailTemplate.razor, CompactRunReportEmailTemplate.razor,
│                   IssuesTable.razor, ResultTableView.razor
└── Publishing/   IRunReportPublisher, EmailRunReportPublisher
```

- `RunReporter` is scoped and owns one lock-guarded attribute/issue/table state. The DI scope
  is the run boundary. `CompleteAsync` atomically closes collection, captures one snapshot,
  and shares one completion task with every repeated/concurrent caller.
- `RunReporter` requires explicit options, publishers, and logger dependencies. Invalid
  construction fails at composition time; it never silently substitutes no-op defaults.
- `ResultTable.From` and `RunReport.DeriveOutcome` are internal collection helpers.
  Public formatters and publishers consume the resulting records.
- DI: `AddRunReporting` registers the delivery-agnostic core; `AddSmtpRunReportPublisher`
  validates email configuration and explicitly adds the SMTP publisher. Configured options
  replace any pre-registered `RunReportingOptions`; the formatter is singleton and the
  reporter is scoped.
- SMTP send is bounded by `Options.SendTimeout` via a linked CTS
  (`SmtpClient.Timeout` does not apply to `SendMailAsync`). Full and compact delivery are
  independent best-effort attempts; any failures are reported together after both.

## Consumer quick start

```csharp
services.AddRunReporting();
services.AddSmtpRunReportPublisher(configuration);  // requires "EmailReport" section
// appsettings: { "EmailReport": { "Enabled": true, "Host": "...", "Port": 25,
//                "From": "svc@x", "To": "team@x; ops@x", "ServiceName": "My Service" } }

reporter.AddAttribute("runId", id);
reporter.AddAttribute("environment", env);
reporter.AddIssue("Step3", "ExternalId2", id, "Rate missing");    // Warning by default
reporter.AddIssue("fatal", severity: IssueSeverity.Error);
reporter.AddTable("Persisted Records", rows,
    [
        TableColumn<PersistedRow>.Left("INTERNAL ID", row => row.InternalId),
        TableColumn<PersistedRow>.Number("AMOUNT", row => row.Amount, "N2")
    ]);
await reporter.CompleteAsync(failed ? RunOutcome.Failed : null);
```

## Reference integration (DataRetriever, in this repo)

- `DataRetrievalOrchestrator`: adds run attributes at the top, then calls
  `CompleteAsync(status == Failed ? RunOutcome.Failed : null)` at the bottom; API returns
  only `{ runId, status }` (`DataRetrievalRunResult`) — the email *is* the report.
- `StepRunner` bridges keyed fatal errors carried in `StepExecutionResult.Issues` into the
  reporter (ordinary warnings are origin-reported by validators/mappers directly).
- `Step4Persister` adds the "Persisted Records" table at origin.
- Simulator/dev mode: `EmailReport.Enabled` drives whether email actually sends
  (smtp4dev on localhost:2525 locally).

## Known accepted limitations (deliberate, revisit only with the owner)

- Every reporting contributor must be resolved from the same per-run DI scope. A singleton
  cannot depend on `IRunReporter`; singleton handlers resolve the reporter and workflow from
  the nested run scope they already create.
- Publishers run sequentially; concurrency to be decided when a second publisher exists.
- Table selectors or cell values that fail to evaluate/format render `-`/type-name; never throw.
- Package version/metadata in `RunReporting.csproj` is `0.2.0` — bump before publishing.

## Testing

Package-focused tests live in `tests/DataRetriever.Tests/RunReporting/RunReporterTests.cs`
(run `dotnet test`, use `-c Release` if a debugger holds Debug outputs). Coverage includes:
scoped lifetime/isolation, idempotent concurrent completion, ignored writes after completion,
case-insensitive attributes, `""` recipient blanking, never-throw guarantees (throwing
table selectors, null args, publisher failures, internal-timeout OCE containment), explicit
issue identifiers,
subject builder (custom/fallback/CRLF), explicit typed table selectors, alignment
flow, isolated full/compact email failures, Razor rendering + HTML
encoding, explicit-constructor failures, and composition-time validation failures.
