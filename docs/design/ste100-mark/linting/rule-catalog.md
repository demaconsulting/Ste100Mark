### RuleCatalog

![Linting Structure](LintingView.svg)

#### Purpose

`RuleCatalog` is the single, hand-authored source of truth for every rule code the `Linting`
subsystem can emit. Its single responsibility is to expose static, machine-readable metadata
(title, official/advisory classification, suggestion kind, and applicable mode(s)) for each
rule code and serialize that metadata as JSON. It performs no rule evaluation itself and has no
dependency on `Context`, `StructuralRules`, or `DictionaryChecker`; it is pure, callable static
data consumed externally by `Program`'s `--list-rules` dispatch path.

#### Data Model

**RuleCatalogEntry**: `internal sealed record` representing one catalog entry.

- `Code`: `string` - the stable rule identifier, for example `STE100-4.1`.
- `Title`: `string` - short human-readable description of what the rule checks.
- `Official`: `bool` - `true` for an actual ASD-STE100 numbered rule; `false` for an
  advisory/heuristic rule that is not an official STE100 rule.
- `SuggestionKind`: `string` - `"advice"` when the rule's suggestion is free prose advice, or
  `"citationForm"` when the rule's suggestion is an ASD-STE100 dictionary citation-form
  replacement.
- `Modes`: `IReadOnlyList<string>` - the linting mode(s) (`"procedure"`, `"descriptive"`) the
  rule applies in.

**Entries**: `IReadOnlyList<RuleCatalogEntry>` (static property) - the fixed list of eight
entries, one per rule code emitted by `StructuralRules` or `DictionaryChecker`:

| Code | Official | SuggestionKind | Modes |
| ------ | ---------- | ---------------- | ------- |
| `STE100-4.1` | `true` | `advice` | procedure, descriptive |
| `STE100-8.1` | `true` | `advice` | procedure, descriptive |
| `STE100-4.2` | `true` | `advice` | procedure, descriptive |
| `STE100-DICT` | `true` | `citationForm` | procedure, descriptive |
| `STE100-ADV-PARA` | `false` | `advice` | procedure, descriptive |
| `STE100-ADV-PASSIVE` | `false` | `advice` | procedure, descriptive |
| `STE100-ADV-COMPLEXVERB` | `false` | `advice` | procedure, descriptive |
| `STE100-ADV-INGFORM` | `false` | `advice` | procedure, descriptive |

`STE100-DICT` is classified `Official: true` because it is grouped alongside `STE100-4.1`,
`STE100-8.1`, and `STE100-4.2` as a "mechanical rule" in the tool's user-facing documentation,
and `DictionaryChecker`'s own remarks describe it as "a mechanical, non-advisory rule",
distinct from the `STE100-ADV-*` advisory heuristics. Every rule applies in both linting
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
  `Entries`, using camelCase property names (`code`, `title`, `official`, `suggestionKind`,
  `modes`).

#### Error Handling

None beyond propagating a `JsonException` from `JsonSerializer.Serialize`, which cannot occur
for this fixed, hand-authored payload of plain strings, booleans, and string lists.

#### Dependencies

- **System.Text.Json** - serializes the stable JSON schema via a source-generated
  `JsonSerializerContext`.

#### Callers

- **Program** - calls `RuleCatalog.ToJson()` when `Context.ListRules` is set, before the main
  lint path and before the banner is printed.
