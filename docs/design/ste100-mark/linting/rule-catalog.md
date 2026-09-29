### RuleCatalog

![Linting Structure](LintingView.svg)

#### Purpose

`RuleCatalog` is the single, hand-authored source of truth for every rule code the `Linting`
subsystem can emit. Its single responsibility is to expose static, machine-readable metadata
(title, classification, suggestion kind, and applicable mode(s)) for each
rule code and serialize that metadata as JSON. It performs no rule evaluation itself and has no
dependency on `Context`, `StructuralRules`, or `DictionaryChecker`; it is pure, callable static
data consumed externally by `Program`'s `--list-rules` dispatch path.

#### Data Model

**RuleCatalogEntry**: `internal sealed record` representing one catalog entry.

- `Code`: `string` - the stable rule identifier, for example `STE100-4.1`.
- `Title`: `string` - short human-readable description of what the rule checks.
- `Classification`: `string` - `"official"` for an actual ASD-STE100 numbered rule;
  `"mechanical"` for a tool-defined, deterministic check that is not itself a numbered
  ASD-STE100 rule (for example `STE100-DICT`); or `"advisory"` for a heuristic that may
  under- or over-detect and is not itself a numbered ASD-STE100 rule (for example
  `STE100-ADV-PASSIVE`). This distinguishes a reliable non-numbered check from a fallible
  heuristic, which a bare official/advisory boolean could not express.
- `SuggestionKind`: `string` - `"advice"` when the rule's suggestion is free prose advice, or
  `"citationForm"` when the rule's suggestion is an ASD-STE100 dictionary citation-form
  replacement.
- `Modes`: `IReadOnlyList<string>` - the linting mode(s) (`"procedure"`, `"descriptive"`) the
  rule applies in.

**Entries**: `IReadOnlyList<RuleCatalogEntry>` (static property) - the fixed list of eight
entries, one per rule code emitted by `StructuralRules` or `DictionaryChecker`:

| Code | Classification | SuggestionKind | Modes |
| ------ | ---------------- | ---------------- | ------- |
| `STE100-4.1` | `official` | `advice` | procedure, descriptive |
| `STE100-8.1` | `official` | `advice` | procedure, descriptive |
| `STE100-4.2` | `official` | `advice` | procedure, descriptive |
| `STE100-DICT` | `mechanical` | `citationForm` | procedure, descriptive |
| `STE100-ADV-PARA` | `advisory` | `advice` | procedure, descriptive |
| `STE100-ADV-PASSIVE` | `advisory` | `advice` | procedure, descriptive |
| `STE100-ADV-COMPLEXVERB` | `advisory` | `advice` | procedure, descriptive |
| `STE100-ADV-INGFORM` | `advisory` | `advice` | procedure, descriptive |

`STE100-DICT` is classified `"mechanical"` rather than `"official"` because it has no
corresponding numbered ASD-STE100 rule: it is instead a tool-defined dictionary/vocabulary
check whose word list may be the embedded illustrative default or a project-supplied
configured dictionary, per `DictionaryChecker`'s own remarks and
`docs/design/ste100-mark/linting.md`. It is also classified `"mechanical"` rather than
`"advisory"` because, unlike the `STE100-ADV-*` heuristics, it is a deterministic check
(reported at error severity) that does not guess or under/over-detect: a term either is or
is not present in the effective dictionary's disallow/citation-form list. Every rule applies
in both linting
modes: `StructuralRules.Evaluate` and `DictionaryChecker.Evaluate` run unconditionally for
every file regardless of the resolved `LintMode`; only `STE100-4.1`'s numeric word-count
threshold (not its applicability) varies between `LintMode.Procedure` and
`LintMode.Descriptive`.

**RuleCatalogJsonContext**: private source-generated `JsonSerializerContext` for
reflection-free JSON serialization of `IReadOnlyList<RuleCatalogEntry>`.

#### Key Methods

**ToJson**: Serializes `Entries` to a single, stable-schema, pretty-printed JSON array.

- *Parameters*: None.
- *Returns*: `string` - the JSON document text.
- *Preconditions*: None.
- *Postconditions*: The returned text parses as a JSON array with one object per entry in
  `Entries`, using camelCase property names (`code`, `title`, `classification`,
  `suggestionKind`, `modes`).

#### Error Handling

None beyond propagating a `JsonException` from `JsonSerializer.Serialize`, which cannot occur
for this fixed, hand-authored payload of plain strings, booleans, and string lists.

#### Dependencies

- **System.Text.Json** - serializes the stable JSON schema via a source-generated
  `JsonSerializerContext`.

#### Callers

- **Program** - calls `RuleCatalog.ToJson()` when `Context.ListRules` is set, before the main
  lint path and before the banner is printed.
