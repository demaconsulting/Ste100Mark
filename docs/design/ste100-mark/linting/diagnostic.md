### Diagnostic

![Linting Structure](LintingView.svg)

#### Purpose

`Diagnostic` is the immutable data-transfer record for one lint finding. Its single
responsibility is to carry rule output unchanged from the producing rule engine to the
reporter and exit-code logic.

#### Data Model

**File**: `string` - file path reported to the user exactly as resolved by `Linter`.

**Line**: `int` - 1-based source line number where the finding begins.

**Column**: `int?` - 1-based column number, or `null` when the producing rule has no
column-level location.

**RuleCode**: `string` - stable identifier such as `STE100-4.1` or `STE100-DICT`.

**Severity**: `Severity` - effective enforcement level of the finding.

**Message**: `string` - human-readable explanation of the problem.

**Suggestion**: `string?` - optional suggested fix.

#### Key Methods

N/A - `Diagnostic` is a record type with compiler-generated value semantics only.

#### Error Handling

`Diagnostic` performs no validation of constructor arguments. Producers are responsible for
supplying already-resolved paths, line numbers, rule codes, and messages.

#### Dependencies

- **Severity** - stores the finding severity.
- **.NET record support** - provides immutability and value-based equality.

#### Callers

- **StructuralRules** - creates diagnostics for sentence, punctuation, contraction, and
  advisory findings.
- **DictionaryChecker** - creates diagnostics for disallowed dictionary terms.
- **Linter** - aggregates diagnostics across files.
- **DiagnosticReporter** - formats diagnostics for output.
