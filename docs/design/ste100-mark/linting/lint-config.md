### LintConfig

![Linting Structure](LintingView.svg)

#### Purpose

`LintConfig` and its supporting types model the `.ste100mark.yaml` schema and provide the
configuration behaviors the subsystem needs at runtime: loading YAML into typed objects and
resolving the effective `LintMode`, `RulesConfig`, and per-file dictionary allowances for a
file. Document-type-scoped tuning (for example, a requirements folder that legitimately uses
"shall") is expressed through `Profiles`, whose deltas are layered over the global `Rules` and
`Dictionary` settings rather than duplicating the whole configuration file per document type.

> **Dictionary notice:** The `dictionary.file` setting is how projects provide your
> organization's licensed ASD-STE100 dictionary when true ASD-STE100 Issue 9 compliance is
> required. The embedded default dictionary is only an illustrative example and must not be
> treated as the official ASD-STE100 Part 2 Dictionary.

#### Data Model

**LintMode**: enum - selects the sentence-length profile applied to a file.

- `Procedure` - 20-word Rule 4.1 sentence limit.
- `Descriptive` - 25-word Rule 4.1 sentence limit.

**RulesConfig**: configuration class for structural and advisory rule tuning; also the global
`rules:` section value.

- `MaxWordsProcedure`: `int` - default 20.
- `MaxWordsDescriptive`: `int` - default 25.
- `AllowSemicolons`: `bool` - default `false`; disables Rule 8.1 when `true`.
- `AllowContractions`: `bool` - default `false`; disables Rule 4.2 when `true`.
- `MaxSentencesParagraph`: `int` - advisory paragraph cap; default 6; `0` disables it.
- `PassiveVoice`: `Severity` - advisory passive-voice severity; default `Warn`.
- `ComplexVerb`: `Severity` - advisory complex-verb (perfect/modal-perfect tense) severity;
  default `Warn`.
- `IngForm`: `Severity` - advisory `-ing` form severity; default `Warn`.
- `WithOverrides(RulesOverride? overrideValues)`: `RulesConfig` - returns a copy of this
  instance with every non-null field of `overrideValues` applied on top; fields left `null` in
  `overrideValues` keep this instance's value. Used by `LintConfig.ResolveRules` to layer a
  matching profile's rule delta over the global rules.

**RulesOverride**: partial rule-tuning deltas bound from a `Profile`'s `rules:` section; every
property is nullable so a profile only states the knobs it changes.

- `MaxWordsProcedure`, `MaxWordsDescriptive`: `int?`
- `AllowSemicolons`, `AllowContractions`: `bool?`
- `MaxSentencesParagraph`: `int?`
- `PassiveVoice`, `ComplexVerb`, `IngForm`: `Severity?`

**DictionaryOverride**: partial dictionary deltas bound from a `Profile`'s `dictionary:`
section. Unlike the global `DictionaryConfig`, a profile cannot supply its own `file`/
`disallow`/`use-embedded` dictionary source - the merged term-to-sense dictionary is always
identical project-wide; a profile can only layer additional allowances on top of it for the
files it matches.

- `Allow`: `List<string>?` - additional terms allowed for matching files, unioned with the
  global `DictionaryConfig.Allow`.
- `Ignore`: `List<string>?` - additional terms ignored for matching files, unioned with the
  global `DictionaryConfig.Ignore`. Applied identically to `Allow`.
- `AllowInPhrase`: `List<string>?` - additional phrase-scoped allowances for matching files,
  unioned with the global `DictionaryConfig.AllowInPhrase`.

**Profile**: glob-scoped mode/rules/dictionary tuning entry (the `profiles:` list item type).

- `Glob`: `string` - glob pattern relative to the configuration directory.
- `Mode`: `LintMode?` - writing mode to apply when `Glob` matches, or `null` to leave mode
  resolution to `DefaultMode` or another matching profile.
- `Rules`: `RulesOverride?` - partial rule-tuning delta applied for files matching `Glob`, or
  `null` for no rule changes.
- `Dictionary`: `DictionaryOverride?` - additional dictionary allow/ignore/allow-in-phrase terms
  for files matching `Glob`, or `null` for no dictionary changes.

**DictionaryConfig**: global dictionary merge settings.

- `File`: `string?` - optional project dictionary file path.
- `Disallow`: `Dictionary<string, List<DictionarySenseYaml>>?` - inline disallowed terms and
  their POS-tagged sense list(s).
- `Allow`: `List<string>?` - terms to remove from the effective dictionary, project-wide.
- `Ignore`: `List<string>?` - terms excluded for documentation clarity but removed the same
  way as `Allow`.
- `AllowInPhrase`: `List<string>?` - phrases within which a disallowed term match is
  suppressed, without suppressing the same term elsewhere in the segment; unlike `Allow`/
  `Ignore`, does not remove the term from the effective dictionary project-wide.
- `UseEmbedded`: `bool` - default `true`; disables the embedded illustrative baseline when
  `false`.
- `Enabled`: `bool` - default `true`; when `false`, `Linter.RunCore` skips loading a dictionary
  entirely and does not run `DictionaryChecker`, disabling the `STE100-DICT` check project-wide
  without requiring any dictionary source. Distinct from `UseEmbedded`, which only removes the
  embedded baseline while the check itself still runs against any remaining sources.

**LintConfig**: root configuration class.

- `Include`: `List<string>` - default empty; treated by `Linter` as `**/*.md` when empty.
- `Exclude`: `List<string>` - exclusion globs.
- `DefaultMode`: `LintMode` - default `Descriptive`.
- `Profiles`: `List<Profile>` - glob-scoped mode/rules/dictionary tuning, evaluated in
  declaration order (see Key Methods for the distinct first-match-wins vs. layered-merge
  resolution rules).
- `Rules`: `RulesConfig` - global rule tuning; defaults to a new `RulesConfig` instance.
- `Dictionary`: `DictionaryConfig?` - optional global dictionary configuration.

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

- *Parameters*: `string relativeFilePath` - file path relative to the profile glob base,
  using forward slashes.
- *Returns*: `LintMode` - the first matching profile's mode, or `DefaultMode`.
- *Preconditions*: `relativeFilePath` is not null.
- *Postconditions*: Iterates `Profiles` in declaration order and returns the `Mode` of the
  first entry that both matches the glob and has a non-null `Mode`; profiles matching the
  glob but carrying only `Rules`/`Dictionary` deltas (a `null` `Mode`) are skipped for this
  resolution and considered separately by `ResolveRules`/`ResolveAllowedTerms`. Returns
  `DefaultMode` when no matching profile specifies a mode.

**ResolveRules**: Resolves the effective `RulesConfig` for one file path.

- *Parameters*: `string relativeFilePath` - file path relative to the profile glob base.
- *Returns*: `RulesConfig` - the global `Rules`, layered with every matching profile's
  `Rules` delta.
- *Preconditions*: `relativeFilePath` is not null.
- *Postconditions*: Unlike `ResolveMode` (first match wins), **every** profile whose glob
  matches and whose `Rules` is non-null contributes its delta via
  `RulesConfig.WithOverrides`, applied in declaration order, so a later matching profile
  wins over an earlier one for any single knob both set. A file can therefore pick up, for
  example, a stricter word limit from one profile and a relaxed passive-voice severity from
  another, simultaneously.

**ResolveAllowedTerms**: Resolves the additional dictionary allow/ignore terms for one file
path.

- *Parameters*: `string relativeFilePath` - file path relative to the profile glob base.
- *Returns*: `IReadOnlyCollection<string>` - the global `Dictionary.Allow`/`Dictionary.Ignore`
  terms, unioned with the `Dictionary.Allow`/`Dictionary.Ignore` terms of every matching
  profile, case-insensitively de-duplicated.
- *Preconditions*: `relativeFilePath` is not null.
- *Postconditions*: The returned set is passed to `DictionaryChecker.Evaluate` as
  `extraAllowedTerms`, so it suppresses matches for this file only without altering the
  merged `LintDictionary` used for every other file.

**ResolveAllowedPhrases**: Resolves the phrase-scoped dictionary allowances for one file path.

- *Parameters*: `string relativeFilePath` - file path relative to the profile glob base.
- *Returns*: `IReadOnlyCollection<string>` - the global `Dictionary.AllowInPhrase` phrases,
  unioned with the `Dictionary.AllowInPhrase` phrases of every matching profile,
  case-insensitively de-duplicated.
- *Preconditions*: `relativeFilePath` is not null.
- *Postconditions*: The returned set is passed to `DictionaryChecker.Evaluate` as
  `allowedPhrases`, so a term match falling wholly inside one of these phrases is suppressed
  for this file only, without suppressing the same term elsewhere in the segment or altering
  the merged `LintDictionary` used for every other file.

Creates a fresh `Matcher` per profile glob check (via the private `MatchesGlob` helper) so
each glob is evaluated independently.

#### Error Handling

`Load` throws `InvalidOperationException` when `path` is non-null but the file does not
exist, or when YamlDotNet fails to parse the file. `ResolveMode`, `ResolveRules`,
`ResolveAllowedTerms`, and `ResolveAllowedPhrases` do not catch matcher or null-argument
failures; `ArgumentNullException` propagates for a null path.

#### Dependencies

- **YamlDotNet** - `DeserializerBuilder`, `HyphenatedNamingConvention`, and YAML binding.
- **Microsoft.Extensions.FileSystemGlobbing** - `Matcher` for profile glob resolution.
- **Severity** - advisory severity configuration.
- **.NET BCL** - file-system access through `File`.

#### Callers

- **Linter** - loads the configuration and resolves each file's lint mode, effective rules,
  and per-file dictionary allowances.
- **LintDictionary** - consumes the global `DictionaryConfig` during dictionary merge.
- **DictionaryChecker** - consumes the per-file `ResolveAllowedTerms`/`ResolveAllowedPhrases`
  results as `extraAllowedTerms`/`allowedPhrases`.
