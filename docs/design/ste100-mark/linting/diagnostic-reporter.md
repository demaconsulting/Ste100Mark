### DiagnosticReporter

![Linting Structure](LintingView.svg)

#### Purpose

`DiagnosticReporter` formats the aggregated findings from a lint run and writes them through
the shared `Context`. Its single responsibility is presentation: text lines for humans or one
stable JSON document for machine consumers.

#### Data Model

**JsonReport**: private record representing the JSON root object.

- `FilesChecked`: `int` - number of linted files.
- `ErrorCount`: `int` - count of `Severity.Error` findings.
- `WarningCount`: `int` - count of `Severity.Warn` findings.
- `Diagnostics`: `IReadOnlyList<JsonDiagnostic>` - serialized diagnostic payload.

**JsonDiagnostic**: private record representing one serialized diagnostic.

- `File`, `Line`, `Column`, `RuleCode`, `Severity`, `Message`, `Suggestion` map directly
  from `Diagnostic`.

**JsonReportContext**: source-generated `JsonSerializerContext` for reflection-free JSON
serialization.

#### Key Methods

**Report**: Chooses text or JSON output based on `context.Format`.

- *Parameters*: `Context context` - output target and format selector;
  `IReadOnlyList<Diagnostic> diagnostics` - aggregated findings; `int filesChecked` - number
  of linted files.
- *Returns*: `void`.
- *Preconditions*: `context` and `diagnostics` are non-null.
- *Postconditions*: Exactly one output path (`WriteText` or `WriteJson`) is executed.

**WriteText**: Emits one line per diagnostic followed by a summary line.

- *Parameters*: same logical payload as `Report`.
- *Returns*: `void`.
- *Postconditions*: Each line contains location, uppercase severity, rule code, message, and
  optional suggestion.

**WriteJson**: Serializes the findings to one pretty-printed JSON document and writes it in a
single `Context.WriteLine` call.

- *Parameters*: same logical payload as `Report`.
- *Returns*: `void`.
- *Postconditions*: Stdout contains one parseable JSON document with camelCase property names.

#### Error Handling

`Report` and its helpers propagate `ArgumentNullException` for null required arguments.
`DiagnosticReporter` does not set the exit code itself; `Linter` is responsible for calling
`Context.MarkFailure` or `Context.WriteError` after reporting.

#### Dependencies

- **Context** and **OutputFormat** - determine where and how to write the report.
- **Diagnostic** and **Severity** - source diagnostic payload and summary counts.
- **System.Text.Json** - serializes the JSON report.

#### Callers

- **Linter** - reports the aggregated results of a lint run.
