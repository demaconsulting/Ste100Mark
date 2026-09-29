### RuleCodes

![Linting Structure](LintingView.svg)

#### Purpose

`RuleCodes` is the single, shared registry of the rule-code string constants the linter can
emit. Its single responsibility is to hold one `const string` per rule code so
`StructuralRules`, `DictionaryChecker`, and `RuleCatalog` reference the same textual value
instead of each embedding independent string literals, eliminating typo/copy drift between
what the emitters actually produce and what `RuleCatalog` advertises via `--list-rules`.

#### Data Model

**Static class**: `internal static class RuleCodes` holding eight `public const string`
fields, one per rule code: `SentenceWordLimit` (`STE100-4.1`), `NoSemicolons` (`STE100-8.1`),
`NoContractions` (`STE100-4.2`), `Dictionary` (`STE100-DICT`), `AdvisoryParagraphLength`
(`STE100-ADV-PARA`), `AdvisoryPassiveVoice` (`STE100-ADV-PASSIVE`), `AdvisoryComplexVerb`
(`STE100-ADV-COMPLEXVERB`), and `AdvisoryIngForm` (`STE100-ADV-INGFORM`).

`RuleCodes` only centralizes the code strings themselves; it holds no title, classification,
suggestion-kind, or mode metadata - that remains `RuleCatalog`'s responsibility. Adding a
constant here does not by itself add a `RuleCatalog` entry; that step remains a separate,
deliberate edit, verified by `RuleCatalog_Entries_ContainsEveryEmittedRuleCode`, which
exercises `StructuralRules` and `DictionaryChecker` directly and compares the observed codes
against `RuleCatalog.Entries`.

#### Key Methods

N/A - `RuleCodes` is a static holder of `const string` fields and exposes no behavior.

#### Error Handling

N/A - `RuleCodes` performs no validation, I/O, or exception handling.

#### Dependencies

- **.NET BCL** - `const string` support only.

#### Callers

- **StructuralRules** - references these constants when constructing structural-rule and
  advisory-heuristic diagnostics.
- **DictionaryChecker** - references `RuleCodes.Dictionary` when constructing dictionary
  diagnostics.
- **RuleCatalog** - references the same constants when building each published `--list-rules`
  entry.
