### Severity

![Linting Structure](LintingView.svg)

#### Purpose

`Severity` defines the three allowed enforcement levels shared by lint configuration and
diagnostics. Its single responsibility is to let the subsystem distinguish disabled checks,
advisory findings, and build-breaking failures in a closed, exhaustively switchable set.

#### Data Model

**Off**: enum member - disables a check entirely so no diagnostics are produced.

**Warn**: enum member - reports findings without failing the run unless `--strict` is active.

**Error**: enum member - reports findings that always fail the run.

#### Key Methods

N/A - `Severity` is an enum and exposes no behavior beyond its named values.

#### Error Handling

N/A - `Severity` performs no validation or I/O.

#### Dependencies

- **.NET BCL** - enum support only.

#### Callers

- **LintConfig** - uses `Severity` for advisory rule configuration.
- **Diagnostic** - stores the effective severity for each finding.
- **StructuralRules** - emits configured severities for advisory findings.
- **Linter** - interprets `Warn` and `Error` when computing the exit code.
- **DiagnosticReporter** - renders severity text into text and JSON output.
