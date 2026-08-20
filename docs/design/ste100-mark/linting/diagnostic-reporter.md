### DiagnosticReporter

![Linting Structure](LintingView.svg)

#### Purpose

`DiagnosticReporter` formats the aggregated findings from a lint run and writes them through
the shared `Context`. Its single responsibility is presentation: line-oriented console output
for human readers or one stable JSON document for machine consumers, without performing any
rule evaluation or exit-code decisions itself.

#### Data Model

**JsonReport**: private record representing the JSON root object.

- `FilesChecked`: `int` - number of linted files.
- `ErrorCount`: `int` - count of `Severity.Error` findings.
- `WarningCount`: `int` - count of `Severity.Warn` findings.
- `Diagnostics`: `IReadOnlyList<JsonDiagnostic>` - serialized diagnostic payload in produced
  order.

**JsonDiagnostic**: private record representing one serialized diagnostic.

- `File`: `string` - path from `Diagnostic.File`.
- `Line`: `int` - 1-based source line from `Diagnostic.Line`.
- `Column`: `int?` - optional 1-based source column from `Diagnostic.Column`.
- `RuleCode`: `string` - stable rule identifier from `Diagnostic.RuleCode`.
- `Severity`: `string` - lowercase text form of `Diagnostic.Severity`.
- `Message`: `string` - diagnostic message text.
- `Suggestion`: `string?` - optional suggested correction.

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

**WriteText**: Emits one human-readable line per diagnostic followed by a summary line.

- *Parameters*: `Context context`; `IReadOnlyList<Diagnostic> diagnostics`; `int filesChecked`.
- *Returns*: `void`.
- *Postconditions*: Each diagnostic line contains location, uppercase severity, rule code,
  message, and optional suggestion. A final summary line reports checked files plus error and
  warning counts.

**WriteJson**: Serializes the findings to one pretty-printed JSON document and writes it in a
single `Context.WriteLine` call.

- *Parameters*: `Context context`; `IReadOnlyList<Diagnostic> diagnostics`; `int filesChecked`.
- *Returns*: `void`.
- *Postconditions*: Standard output contains one parseable JSON document with camelCase
  property names, summary counts, and every produced diagnostic.

#### Error Handling

`Report` and its helpers propagate `ArgumentNullException` for null required arguments.
`WriteJson` does not catch `JsonSerializer` failures. `DiagnosticReporter` does not set the
exit code itself; `Linter` remains responsible for calling `Context.MarkFailure` or
`Context.WriteError` after reporting. In JSON mode, the implementation buffers the full
document and writes it in one call so stdout remains a single JSON value.

#### Dependencies

- **Context** and **OutputFormat** - determine where and how to write the report.
- **Diagnostic** and **Severity** - provide the source payload and summary counts.
- **System.Text.Json** - serializes the stable JSON schema.

#### Callers

- **Linter** - reports the aggregated results of a lint run after all files are evaluated.
