# Email Reporting

This template uses a generic report-table model for email reporting. The reporting layer should not know about service-specific concepts such as persisted records, coupons, trades, instruments, rejected rows, or reminders.

## Core Shape

`RunReport` contains:

- run metadata;
- status;
- request summary;
- generic summary metrics;
- step results;
- warnings and errors;
- zero or more named report tables.

Service-specific data is represented as `RunReportTable`:

```csharp
public sealed record RunReportTable(
    string Name,
    string Title,
    IReadOnlyList<RunReportColumn> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows);
```

Each service decides which tables to add. Examples:

- `persisted-records`
- `rejected-records`
- `expiring-coupons`
- `missing-reference-data`
- `records-skipped`

The shared email formatter renders every table as a grid. This supports one table, two tables, or no service-specific tables without changing the email template.

By default, the email keeps the body focused:

1. errors, only when present;
2. warnings, only when present;
3. service-specific tables.

Extra run statistics are hidden by default and can be enabled with:

```json
"RunReportEmail": {
  "DisplayStats": true
}
```

## Ownership

Reporting owns:

- `RunReport`;
- `RunReportMetric`;
- `RunReportTable`;
- `RunReportColumn`;
- Razor email layout;
- warnings/errors rendering;
- generic table rendering.

The application/service owns:

- deciding which business rows are report-worthy;
- mapping business output into `RunReportTable` rows;
- naming and ordering tables;
- choosing summary metrics.

Infrastructure owns:

- actual email sending;
- SMTP/internal mail package integration;
- email configuration.

## Current Template Example

This prototype has one service-specific table:

```text
persisted-records -> Persisted Records
```

`Step4ReportTableBuilder` maps `Step4Output.PersistedRecords` into a generic table. Another service can replace this with its own table builder, or return multiple tables.

## Guidelines

- Keep table rows as simple string dictionaries.
- Keep columns explicit so the email renderer can preserve order and alignment.
- Format numbers/dates before putting them into the table row.
- Use `CultureInfo.InvariantCulture` for external/report-stable numeric values.
- Keep warnings and errors in `RunReport.Issues`; do not duplicate them into service-specific tables unless users need a custom view.
- Keep statistics optional in the email. They remain available in the structured `RunReport`.
- Do not add a generic workflow/report-section framework unless several services need richer layouts than summary metrics, issues, and tables.
