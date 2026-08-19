### LintConfig

![Linting Structure](LintingView.svg)

#### Purpose

`LintConfig` and its supporting types model the `.ste100mark.yaml` schema and provide the
two configuration behaviors the subsystem needs at runtime: loading YAML into typed objects
and resolving the effective `LintMode` for a file.

> **Dictionary notice:** The `dictionary.file` setting is how projects provide your
> organization's licensed ASD-STE100 dictionary when true ASD-STE100 Issue 9 compliance is
> required. The embedded default dictionary is only an illustrative example and must not be
> treated as the official ASD-STE100 Part 2 Dictionary.

#### Data Model

**LintMode**: enum - selects the sentence-length profile applied to a file.

- `Procedure` - 20-word Rule 4.1 sentence limit.
- `Descriptive` - 25-word Rule 4.1 sentence limit.

**RulesConfig**: configuration class for structural and advisory rule tuning.

- `MaxWordsProcedure`: `int` - default 20.
- `MaxWordsDescriptive`: `int` - default 25.
- `AllowSemicolons`: `bool` - default `false`; disables Rule 8.1 when `true`.
- `AllowContractions`: `bool` - default `false`; disables Rule 4.2 when `true`.
- `MaxSentencesParagraph`: `int` - advisory paragraph cap; default 6; `0` disables it.
- `PassiveVoice`: `Severity` - advisory passive-voice severity; default `Warn`.

**ModeOverride**: glob-to-mode mapping entry.

- `Glob`: `string` - glob pattern relative to the configuration directory.
- `Mode`: `LintMode` - writing mode to apply when `Glob` matches.

**DictionaryConfig**: dictionary merge settings.

- `File`: `string?` - optional project dictionary file path.
- `Disallow`: `Dictionary<string, List<string>>?` - inline disallowed terms and
  alternatives.
- `Allow`: `List<string>?` - terms to remove from the effective dictionary.
- `Ignore`: `List<string>?` - terms excluded for documentation clarity but removed the same
  way as `Allow`.
- `UseEmbedded`: `bool` - default `true`; disables the embedded illustrative baseline when
  `false`.

**LintConfig**: root configuration class.

- `Include`: `List<string>` - default empty; treated by `Linter` as `**/*.md` when empty.
- `Exclude`: `List<string>` - exclusion globs.
- `DefaultMode`: `LintMode` - default `Descriptive`.
- `Overrides`: `List<ModeOverride>` - first-match-wins mode overrides.
- `Rules`: `RulesConfig` - defaults to a new `RulesConfig` instance.
- `Dictionary`: `DictionaryConfig?` - optional dictionary configuration.

#### Key Methods

**Load**: Parses a YAML configuration file or returns all defaults.

- *Parameters*: `string? path` - resolved configuration path, or `null` when no file was
  found.
- *Returns*: `LintConfig` - fully populated configuration object.
- *Preconditions*: None.
- *Postconditions*: Returns built-in defaults when `path` is `null` or the YAML file is
  empty/comment only; otherwise returns the deserialized file content merged with type
  defaults.

Uses YamlDotNet with the hyphenated naming convention and `IgnoreUnmatchedProperties`.
`Load` treats `null` as "no configuration file resolved" rather than an error.

**ResolveMode**: Resolves the effective mode for one file path.

- *Parameters*: `string relativeFilePath` - file path relative to the override glob base,
  using forward slashes.
- *Returns*: `LintMode` - first matching override mode, or `DefaultMode`.
- *Preconditions*: `relativeFilePath` is not null.
- *Postconditions*: The returned mode reflects first-match-wins evaluation over `Overrides`.

Creates a fresh `Matcher` per override so each glob is evaluated independently.

#### Error Handling

`Load` throws `InvalidOperationException` when `path` is non-null but the file does not
exist, or when YamlDotNet fails to parse the file. `ResolveMode` does not catch matcher or
null-argument failures; `ArgumentNullException` propagates for a null path.

#### Dependencies

- **YamlDotNet** - `DeserializerBuilder`, `HyphenatedNamingConvention`, and YAML binding.
- **Microsoft.Extensions.FileSystemGlobbing** - `Matcher` for override resolution.
- **Severity** - advisory severity configuration.
- **.NET BCL** - file-system access through `File`.

#### Callers

- **Linter** - loads the configuration and resolves each file's lint mode.
- **LintDictionary** - consumes `DictionaryConfig` during dictionary merge.
