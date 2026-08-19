### LintDictionary

![Linting Structure](LintingView.svg)

#### Purpose

`LintDictionary` builds and serves the effective vocabulary used by the dictionary check.
Its single responsibility is to merge all configured dictionary sources into one
case-insensitive lookup structure.

> **Dictionary notice:** The embedded default dictionary resource is a small,
> originally-authored, representative example. It is **not** the official ASD-STE100 Part 2
> Dictionary. Projects that require true ASD-STE100 Issue 9 compliance must supply your
> organization's licensed ASD-STE100 dictionary through `dictionary.file`.

#### Data Model

**PartOfSpeech**: enum of grammatical roles a sense applies to - `Any`, `Noun`, `Verb`,
`Adjective`, `Adverb`.

**DictionarySense**: immutable record describing one grammatical sense of a term.

- `Pos`: `PartOfSpeech` - grammatical role this sense applies to.
- `Alternatives`: `IReadOnlyList<string>` - suggested replacements for this sense.
- `Note`: `string?` - optional rationale.

**DictionaryEntry**: immutable record describing one disallowed term.

- `Term`: `string` - disallowed word or phrase.
- `Senses`: `IReadOnlyList<DictionarySense>` - one or more POS-tagged senses, in declaration
  order. A term with exactly one sense is reported unconditionally by `DictionaryChecker`,
  regardless of that sense's `Pos` value.

**EmbeddedResourceName**: `const string` - manifest-resource name of the embedded default
dictionary file.

**_entries**: `Dictionary<string, DictionaryEntry>` - effective merged entries keyed
case-insensitively by term.

**Entries**: `IReadOnlyCollection<DictionaryEntry>` - exposes the merged entry values.

**DictionarySenseYaml**: internal YAML binding class used both while parsing dictionary files
(embedded and project) and by `LintConfig`'s `DictionaryConfig.Disallow` for inline entries -
a single shared binding type for the one per-term sense-list schema.

#### Key Methods

**Load**: Produces the effective merged dictionary for one lint run.

- *Parameters*: `LintConfig config` - effective lint configuration; `string configDirectory`
  - base directory for resolving a relative `dictionary.file` path.
- *Returns*: `LintDictionary` - merged dictionary ready for lookups.
- *Preconditions*: Both arguments are non-null.
- *Postconditions*: Merge order is embedded dictionary (unless disabled), project dictionary
  file, inline `disallow`, then removal of every term listed in `allow` or `ignore`. Each layer
  performs a full per-term sense-list replacement (not a per-sense merge) - the same
  "last writer wins by term" semantics as the original flat schema.

**TryGetEntry**: Attempts an exact case-insensitive lookup by term.

- *Parameters*: `string term` - candidate term; `out DictionaryEntry? entry` - result slot.
- *Returns*: `bool` - `true` when the term exists in `_entries`.
- *Preconditions*: `term` is not null.
- *Postconditions*: `entry` is populated only when a matching term exists.

**LoadEmbeddedDictionary**: Reads the embedded manifest resource and parses it.

**LoadDictionaryFile**: Reads a project-supplied dictionary file from disk and parses it.

**ParseDictionaryYaml**: Converts raw YAML text into `DictionaryEntry` records.

**ConvertSenses**: Converts a raw `List<DictionarySenseYaml>?` into an
`IReadOnlyList<DictionarySense>`, returning an empty list for a null/empty input.

#### Error Handling

`Load` throws `InvalidOperationException` when the embedded manifest resource is missing,
when a configured project dictionary file does not exist, or when YamlDotNet cannot parse a
dictionary source. `TryGetEntry` delegates to the underlying dictionary and propagates
`ArgumentNullException` for a null key.

#### Dependencies

- **LintConfig** - supplies `DictionaryConfig` settings.
- **YamlDotNet** - deserializes term-to-alternatives mappings.
- **.NET BCL** - uses `File`, `Path`, `StreamReader`, and assembly manifest-resource APIs.
- **DictionaryChecker** - consumes the merged entries.

#### Callers

- **Linter** - loads the effective dictionary once per lint run.
- **DictionaryChecker** - consumes `Entries` and `TryGetEntry` semantics.
