### LintGlossary

![Linting Structure](LintingView.svg)

#### Purpose

`LintGlossary` loads the project glossary: approved terms with a part of speech, and deprecated
terms with a replacement. It reads them from the configuration file and from glossary files,
merges and validates them, and builds the `DemaConsulting.PartOfSpeech` `Glossary` that the
`PartOfSpeechResolver` passes to the tagger. The entry format is shared with DictionaryMark, so
one file can serve both tools.

#### Data Model

**GlossaryTerm**: immutable record of one approved term: `Term`, `Definition`, `Pos` (a set of
universal part-of-speech tags), `Variants`, `Visible`, and `Source` (the file path or the
configuration file).

**DeprecatedTerm**: immutable record of one deprecated term: `Term`, `Use` (the replacement),
`Where` (optional context), and `Source`.

**Entry shape**: an approved entry is a string (the definition, noun by default) or an object with
the keys `definition`, `pos`, `variants`, and `visible`. The `definition` is required and must not
be empty or only white space, whatever the value of `visible`; a missing or empty definition is an
error that names the term and the source file. `pos` is one value or a list of `noun`,
`verb`, `adjective`, `adverb`, and `proper-noun`. A deprecated entry is an object with a required
`use` and an optional `where`. An unknown key is an error.

**Sources**: `dictionary.glossary` (inline approved entries), `dictionary.glossary-file` and
`dictionary.deprecated-file` (a path or glob, or a list of them), and `dictionary.glossary-mode`
(`hard` by default, or `soft`).

#### Key Methods

**Load**: `Load(DictionaryConfig, string? configPath, string configDirectory)` reads inline and
file entries and returns a `LintGlossary`.

- *Path rules*: `${VAR}` references are expanded first (an undefined variable is an error). A
  relative path or glob is resolved against the configuration file directory, an absolute or UNC
  path is used as is. A pattern that matches nothing, a missing file, and an unreachable
  directory are errors with distinct messages.
- *Merge rules*: terms compare case-insensitively after whitespace normalization. Identical
  duplicates collapse into one. A term whose fields differ between two sources, a term that is
  both approved (or a variant of an approved term) and deprecated, and a deprecated term that is
  defined twice with different values are errors. The message lists every conflict in an order
  that does not depend on the file order, naming the term and the sources.

**ValidateAgainst**: checks that no approved term or variant is an effective dictionary entry,
so a glossary never silently overrides the dictionary. The error names the term and the
dictionary source. A term that `allow` or `ignore` removed is not a conflict.

**GetAllowedTerms**: the approved terms and variants, which `DictionaryChecker` treats as allowed.
An approved term is allowed only by whole-term or variant equality. A dictionary word inside a
multi-word glossary term is still checked on its own.

**Tagging**: the package `Glossary` built from the approved terms (each `GlossaryEntry` receives the
real definition) with the configured mode. The
package rejects duplicate token sequences and variants with a different token count; the error is
re-thrown with the term name. A term with `visible: false` is still tagged and allowed.

#### Error Handling

Every problem raises `InvalidOperationException` with a message that names the file, the term, or
the pattern. `Linter` reports it as an error and fails the run. A glossary is not loaded when
`dictionary.enabled` is false.

#### Dependencies

- **LintConfig** (`DictionaryConfig`) - the configuration values.
- **LintDictionary** - the effective dictionary for conflict checks.
- **YamlDotNet** (OTS) - parses glossary files.
- **Microsoft.Extensions.FileSystemGlobbing** (OTS) - resolves globs.
- **PartOfSpeech** (OTS) - the `Glossary` type.

#### Callers

- **Linter** - loads the glossary once per run.
