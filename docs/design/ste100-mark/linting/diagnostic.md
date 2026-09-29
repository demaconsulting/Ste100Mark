### Diagnostic

![Linting Structure](LintingView.svg)

#### Purpose

`Diagnostic` is the immutable data-transfer record for one lint finding. Its single
responsibility is to carry rule output unchanged from the producing rule engine to the
reporter and exit-code logic, so the subsystem can aggregate, compare, and serialize findings
without any later mutation.

#### Data Model

**Record type**: `internal sealed record Diagnostic(
string File,
int Line,
int? Column,
string RuleCode,
Severity Severity,
string Message,
string? Suggestion,
IReadOnlyList<DictionaryCitation>? Citations = null)`.

- `File`: `string` - file path reported to the user exactly as resolved by `Linter`.
- `Line`: `int` - 1-based source line number where the finding begins.
- `Column`: `int?` - 1-based source column number, or `null` when the producing rule has no
  column-level location.
- `RuleCode`: `string` - stable identifier such as `STE100-4.1` or `STE100-DICT`.
- `Severity`: `Severity` - effective enforcement level of the finding.
- `Message`: `string` - human-readable explanation of the problem.
- `Suggestion`: `string?` - optional suggested correction.
- `Citations`: `IReadOnlyList<DictionaryCitation>?` - optional trailing field, defaulting to
  `null`, carrying structured approved-term citations backing `Suggestion`. Populated only by
  `DictionaryChecker` for `STE100-DICT` findings that have at least one real alternative term;
  `null` for every other rule, for a purely self-referential dictionary sense, and for a
  dictionary sense/candidate with no alternatives at all. Additive alongside `Suggestion`
  (which is unchanged) rather than a replacement, so a consumer can use structured term/
  part-of-speech data instead of parsing it back out of prose, without breaking existing
  text-mode/JSON consumers of `Suggestion`.

**DictionaryCitation**: `internal sealed record DictionaryCitation(string Term, string Pos)` -
one structured approved-term citation backing a `Diagnostic.Citations` list.

- `Term`: `string` - the approved replacement term (for example `"GIVE"`).
- `Pos`: `string` - the grammatical role the term is approved in, rendered the same way as
  `Diagnostic.Message`'s part-of-speech wording (for example `"noun"`, `"verb"`, or `"general"`
  for a role-independent term).

The record relies on compiler-generated immutability and value-based equality, which the
subsystem uses for predictable aggregation and simple test assertions.

#### Key Methods

N/A - `Diagnostic` is a record type with compiler-generated constructor, deconstruction,
equality, and formatting behavior only. It defines no custom methods.

#### Error Handling

`Diagnostic` performs no validation of constructor arguments and no I/O. Producers are
responsible for supplying already-resolved paths, line numbers, rule codes, severities,
messages, and suggestions.

#### Dependencies

- **Severity** - stores the finding severity.
- **DictionaryCitation** - structured payload type for the optional `Citations` field.
- **.NET record support** - provides immutability and value-based equality semantics.

#### Callers

- **StructuralRules** - creates diagnostics for sentence, punctuation, contraction, and
  advisory findings.
- **DictionaryChecker** - creates diagnostics for disallowed dictionary terms.
- **Linter** - aggregates diagnostics across files and interprets severities for exit-code
  handling.
- **DiagnosticReporter** - formats diagnostics for text and JSON output.
