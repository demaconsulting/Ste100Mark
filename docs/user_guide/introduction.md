# Introduction

## Purpose

Ste100Mark is a .NET command-line tool for linting Markdown prose against ASD-STE100-style
rules. It also includes a built-in self-validation mode so teams can generate tool
qualification evidence for regulated environments.

## Scope

This user guide covers:

- Installation instructions
- Markdown linting usage and output formats
- `.ste100mark.yaml` configuration and dictionary setup
- Self-validation usage
- Command-line options and practical examples

# Continuous Compliance

This project follows the
[Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance) methodology, with
compliance evidence generated through the repository's documented CI requirements pipeline.

## Key Practices

- **Requirements Traceability**: Requirements link to passing tests as defined by the
  requirements set, while tests may also exist without requirement links
- **Linting Enforcement**: markdownlint, cspell, and yamllint are enforced before any build proceeds
- **Automated Audit Documentation**: ReqStream generates the trace matrix and related compliance
  reports as part of the documented requirements pipeline
- **CodeQL and SonarCloud**: Security and quality analysis runs on every build

# Installation

Install the tool globally using the .NET CLI:

```bash
dotnet tool install -g DemaConsulting.Ste100Mark
```

# ASD-STE100 Linting

The default tool path runs the Markdown linter:

```bash
ste100mark [globs...] [--config <file>] [--format text|json] [--strict]
```

When no positional globs are supplied, Ste100Mark uses `include` and `exclude` patterns from
the resolved configuration file. If no configuration file is present, the tool falls back to
`**/*.md` in the current working directory.

## Lint Markdown Files

Lint one or more Markdown files selected by positional globs:

```bash
ste100mark docs/**/*.md
ste100mark README.md docs/user_guide/**/*.md
```

Use the working-directory defaults:

```bash
ste100mark
```

Use JSON output for CI tooling:

```bash
ste100mark docs/**/*.md --format json
```

JSON mode writes one parseable JSON document to stdout. In that mode, the tool suppresses the
normal banner so no non-JSON text is mixed into the output.

## Mechanical and Advisory Checks

Ste100Mark reports these official mechanical checks:

- `STE100-4.1` - sentence length, with procedure and descriptive limits
- `STE100-8.1` - semicolons in prose
- `STE100-4.2` - contractions in prose
- `STE100-DICT` - matched disallowed terms from the effective dictionary

It also reports these advisory heuristics:

- `STE100-ADV-PARA` - paragraph sentence-count cap
- `STE100-ADV-PASSIVE` - passive-voice heuristic
- `STE100-ADV-COMPLEXVERB` - perfect/modal-perfect tense heuristic
- `STE100-ADV-INGFORM` - `-ing` form heuristic

Advisory findings do not fail the process unless they are configured as `error` or `--strict`
is used.

## '-ing' Form Role Heuristic

`STE100-ADV-INGFORM` only flags an `-ing` word when it resolves as a genuine present-participle
verb use. A gerund or participial-adjective use of the same word is not flagged, including:

- the subject of a sentence or list item (for example, "Metering the flow is required.")
- the object of a preposition (for example, "before testing the gauge.")
- a gerund complement directly after a catenative verb such as "continue" or "begin" (for
  example, "continue monitoring the gauge.")
- a material noun (for example, "the housing")
- a participial adjective directly before a noun (for example, "the moving structure")
- any match inside a heading, a table column-header row, an admonition label, or a
  quoted/cited title

For example, "Checking the gauge is required." is not flagged (gerund subject), while "The
technician is checking the gauge." is still flagged (genuine present-participle verb).

## Passive-Voice Exemptions

`STE100-ADV-PASSIVE` does not flag an "is"/"are" plus past-participle match that reads as a
predicate adjective describing a current state rather than a genuine passive construction.
This exemption applies when either:

- the participle is one of a curated set of common stative/adjectival participles (for
  example "energized", "closed", "connected", "locked"), or
- the sentence is an imperative-mode instruction that begins with a common lead verb (for
  example "Keep", "Confirm", "Verify")

Either exemption is overridden - the match is still flagged as passive - when an explicit "by
\<agent\>" phrase immediately follows the participle, since naming the agent makes the
construction unambiguously passive again.

For example, "Keep the panel closed while it is energized." is not flagged, but "The panel is
closed by the technician." is still flagged, since an explicit agent follows the participle.

## Discovering Rule Codes

Run `ste100mark --list-rules` to display the full, machine-readable rule catalog as a JSON
array on stdout, without requiring any Markdown files to exist:

```bash
ste100mark --list-rules
```

Each entry has the shape:

```json
{
  "code": "STE100-4.1",
  "title": "Sentence word-count limit (Rules 4.1, 8.4-8.7).",
  "classification": "official",
  "suggestionKind": "advice",
  "modes": ["procedure", "descriptive"]
}
```

`classification` is one of `"official"` (an actual ASD-STE100 numbered rule), `"mechanical"`
(a tool-defined, deterministic check that is not itself a numbered rule, for example
`STE100-DICT`), or `"advisory"` (a heuristic that may under- or over-detect and is not itself
a numbered rule).

## Strict Mode

Use `--strict` when you want warn-severity findings to fail the run without changing the
reported diagnostic severity:

```bash
ste100mark docs/**/*.md --strict
```

This is useful when a project wants to treat advisory warnings as temporary release gates in
CI while still keeping the underlying rule configuration at `warn`.

## Configuration File

Ste100Mark reads `.ste100mark.yaml` from the current working directory by default. Use
`--config <file>` to select a different file:

```bash
ste100mark docs/**/*.md --config config/ste100mark.yaml
```

Configuration schema example:

```yaml
include:
  - docs/**/*.md
exclude:
  - docs/**/generated/**
default-mode: descriptive
profiles:
  - glob: docs/user_guide/procedures/**/*.md
    mode: procedure
  - glob: docs/requirements/**/*.md
    dictionary:
      allow: [shall]
    rules:
      passive-voice: off
rules:
  max-words-procedure: 20
  max-words-descriptive: 25
  allow-semicolons: false
  allow-contractions: false
  max-sentences-paragraph: 6
  passive-voice: warn
  complex-verb: warn
  ing-form: warn
dictionary:
  file: your-licensed-asd-ste-dictionary.yaml
  use-embedded: false
  disallow:
    utilize:
      - pos: verb
        alternatives: [use]
  allow:
    - ste100mark
  ignore:
    - api
  allow-in-phrase:
    - trail mix
```

Configuration fields:

- `include` - file-selection globs used when no positional globs are supplied
- `exclude` - exclusions applied to `include`
- `default-mode` - `descriptive` or `procedure`
- `profiles` - glob-scoped mode/rules/dictionary tuning; see "Profiles" below
- `rules` - global rule tuning for sentence limits, semicolons, contractions, paragraph
  cap, and passive voice
- `dictionary` - project dictionary file plus inline allow/disallow/ignore/allow-in-phrase
  tuning

## Profiles

A repository often mixes document types that need different linting behavior: procedural
step-by-step guides need the shorter Rule 4.1 sentence limit, while a requirements folder
legitimately uses the word "shall" in every entry and would otherwise trip a project's
dictionary. Each entry in `profiles` may set:

- `glob` (required) - the pattern (relative to the configuration file's directory)
  identifying the files the profile applies to.
- `mode` (optional) - sets the writing mode for matching files. When a file matches more
  than one profile, the **first** declared profile that specifies a `mode` wins; a
  profile with no `mode` is skipped for mode resolution and falls through to
  `default-mode` or a later matching profile.
- `rules` (optional) - a partial override of the top-level `rules` section: only the
  knobs you list change, everything else keeps its global value. When a file matches
  multiple profiles that set `rules`, every matching profile's delta applies in
  declaration order, so a later profile wins on any single knob both profiles set.
- `dictionary.allow` / `dictionary.ignore` / `dictionary.allow-in-phrase` (optional) -
  additional terms/phrases allowed for matching files only, unioned with the
  corresponding top-level `dictionary` lists. A profile cannot supply its own
  `file`/`disallow`/`use-embedded` dictionary source - the merged term-to-sense
  dictionary is always the same project-wide; profiles only add per-file allowances on
  top of it.

## Project Dictionary Files

> **Important dictionary notice:** The embedded default dictionary in the tool is a small,
> originally-authored, illustrative and representative example. It is **not** the official
> ASD-STE100 Part 2 Dictionary. Ste100Mark does not provide full official ASD-STE100
> dictionary content out of the box.

Projects that require true ASD-STE100 Issue 9 compliance must supply your organization's
licensed ASD-STE100 dictionary file through `dictionary.file`. To avoid mixing illustrative
content with licensed vocabulary, set `dictionary.use-embedded: false` when you use your own
dictionary file.

The project dictionary file uses the same YAML shape as the embedded example: each top-level
key is a disallowed term, mapping to a **list of one or more part-of-speech-tagged senses**.
Each sense has a `pos` (`noun`, `verb`, `adjective`, `adverb`, or `any` for a role-independent
connector), an `alternatives` list of suggested replacement words or phrases for that sense,
and an optional free-text `note`:

```yaml
utilize:
  - pos: verb
    alternatives: [use]
    note: Prefer the shorter, more common word.
impact:
  - pos: noun
    alternatives: [effect]
    note: Prefer the plain noun over the vague noun sense of "impact".
  - pos: verb
    alternatives: [affect]
    note: Prefer the plain verb over the vague verb sense of "impact".
prior to:
  - pos: any
    alternatives: [before]
    note: Simpler time relation.
```

A term with exactly one sense is always reported using that sense, regardless of context, so
no ambiguity is possible when only one sense exists. When a term has more than one sense,
Ste100Mark applies a lightweight, deterministic part-of-speech heuristic to the surrounding
sentence context to decide which sense applies. A confident noun-versus-verb decision reports
only the matching sense, whose alternatives are joined with natural "or"/Oxford-comma
phrasing, and the diagnostic message notes the grammatical role, for example
`Avoid 'impact'; use 'effect' instead (used as a noun).` When the heuristic cannot decide,
every sense is reported, grouped per part of speech and clearly labeled as ambiguous, for
example `Ambiguous part of speech for 'impact' — possible corrections: as a noun, use
'effect'; as a verb, use 'affect'.`

A reported suggestion never includes the exact word that was just flagged as one of its own
alternatives, for both a confidently-resolved sense and an ambiguous, multi-sense result. For
example, if a matched inflected form such as "running" also appears in its sense's
alternatives list alongside "operating", only "operating" is suggested, since suggesting
"running" as a replacement for "running" would be a meaningless no-op.

The part-of-speech heuristic also recognizes a curated set of common technical plural nouns
(for example "modules", "units", "sensors") as the head of a noun-noun compound, even though
each of these words ends in "-s", which is otherwise treated as ambiguous with a verb form.
For example, in "Backup modules are monitored.", "backup" is recognized as a noun modifier (not
a verb) because it is immediately followed by the recognized compound-noun head "modules".

A table's column-header row (for example a "Hazard"/"Use" header above a table of data rows)
and a bold admonition label at the very start of a block (for example `**Caution.**`) are
treated as short labels, not prose sentences, and are skipped entirely by `STE100-DICT` and
`STE100-ADV-INGFORM` - a bare label word is routinely a noun/verb-looking word out of any
sentence context, and flagging it would be noise rather than a genuine finding. The same word
appearing in an ordinary sentence elsewhere in the document, or in a table's data row rather
than its header row, is still checked normally.

A disallowed word appearing only inside a double-quoted span (for example a cited document
title) or a single-asterisk/underscore emphasis span (italicized text) is a mention of that
exact text, not a use of the word in the document's own prose, and is likewise skipped by
`STE100-DICT` and `STE100-ADV-INGFORM`. For example, in `The chapter is titled "System
Utilization".`, the disallowed word inside the quoted title is not flagged, while the same
word used in ordinary prose elsewhere in the document is still flagged.

**Why the real ASD-STE100 dictionary is not included:** The official ASD-STE100 Part 2
Dictionary is commercially-licensed, copyrighted content owned by ASD (Aerospace, Security
and Defence Industries Association of Europe). Because this repository is public and
published to NuGet, redistributing ASD's dictionary content would infringe ASD's copyright.
The embedded default dictionary shipped with Ste100Mark is therefore only an
originally-authored, illustrative and representative word list, intended to demonstrate the
feature and provide a reasonable default — it is **not** a substitute for the real standard.
Any organization that requires true ASD-STE100 compliance must obtain its own license from
ASD and supply its own dictionary file, in the format shown above, via the `dictionary.file`
configuration option.

## Phrase-Scoped Allowances

`dictionary.allow`/`dictionary.ignore` suppress a term project-wide, regardless of context -
useful for a term that is simply never a problem in your project (for example a product name).
Sometimes, though, a disallowed word is genuinely fine as part of one specific approved phrase,
but should still be flagged everywhere else it appears on its own. `dictionary.allow-in-phrase`
expresses exactly that: a match is suppressed only when it falls entirely inside an occurrence
of one of the listed phrases.

```yaml
dictionary:
  allow-in-phrase:
    - trail mix
    - primary mix
    - spark gap
    - stylus tip
    - duty cycle
```

For example, if `mix` is a disallowed term, `allow-in-phrase: [trail mix]` suppresses the
finding in "Fill the trail mix tank" (where "trail mix" is the name of a thing) while still
flagging "check the fuel mix" elsewhere in the same document. Matching is case-insensitive and
tolerant of extra whitespace, identical to a multi-word `dictionary.disallow` term. Like
`dictionary.allow`/`dictionary.ignore`, `dictionary.allow-in-phrase` can also be layered per
profile (see "Profiles" above) to scope a phrase allowance to matching files only.

# Usage

## Display Version

Display the tool version:

```bash
ste100mark --version
```

## Display Help

Display usage information:

```bash
ste100mark --help
```

## Self-Validation

Self-validation produces a report demonstrating that Ste100Mark is functioning
correctly. This is useful in regulated industries where tool validation evidence is required.

### Running Validation

To perform self-validation:

```bash
ste100mark --validate
```

To save validation results to a file:

```bash
ste100mark --validate --results results.trx
```

The `--result` option is an accepted alias for `--results`.

The results file format is determined by the file extension: `.trx` for TRX (MSTest) format,
or `.xml` for JUnit format.

### Heading Depth

Use `--depth <#>` to control the heading level of the validation output (default: `1`).
This is useful when embedding the validation report into a larger markdown document:

```bash
# Embed validation at heading level 2
ste100mark --validate --depth 2
```

### Validation Report

The validation report contains the tool version, machine name, operating system version,
.NET runtime version, timestamp, and test results.

Example validation report:

```text
# DEMA Consulting Ste100Mark

| Information         | Value                                              |
| :------------------ | :------------------------------------------------- |
| Tool Version        | 1.0.0                                              |
| Machine Name        | BUILD-SERVER                                       |
| OS Version          | Ubuntu 22.04.3 LTS                                 |
| DotNet Runtime      | .NET 10.0.0                                        |
| Time Stamp          | 2024-01-15 10:30:00 UTC                            |

✓ Ste100Mark_VersionDisplay - Passed
✓ Ste100Mark_HelpDisplay - Passed
✓ Ste100Mark_LintCleanFileNoDiagnostics - Passed
✓ Ste100Mark_LintViolationFileDetectsIssues - Passed
✓ Ste100Mark_LintJsonOutputIsValidJson - Passed

Total Tests: 5
Passed: 5
Failed: 0
```

### Validation Tests

Each test proves specific functionality works correctly:

- **`Ste100Mark_VersionDisplay`** - `--version` outputs a valid version string.
- **`Ste100Mark_HelpDisplay`** - `--help` outputs usage and options information.
- **`Ste100Mark_LintCleanFileNoDiagnostics`** - linting a fully compliant Markdown file
  produces no diagnostics and a zero exit code.
- **`Ste100Mark_LintViolationFileDetectsIssues`** - linting a file with deliberate
  violations detects each expected rule code and returns a non-zero exit code.
- **`Ste100Mark_LintJsonOutputIsValidJson`** - `--format json` output parses as valid JSON.

## Silent Mode

Suppress console output:

```bash
ste100mark --silent
```

## Logging

Write output to a log file:

```bash
ste100mark --log output.log
```

## Error Handling

Unrecognized arguments cause the tool to print an error message to standard error and exit
with a non-zero exit code. Missing or malformed configuration and dictionary files also cause
a non-zero exit code. For example:

```text
Error: Unsupported argument '--unknown'
```

This behavior enables automated scripts and CI/CD pipelines to detect and surface
misconfiguration failures automatically.

Exit codes are: `0` for a clean pass (including a pass with only warn-severity findings when
`--strict` is not active), `1` when an error-severity lint finding was reported, a warn-severity
finding was reported with `--strict` active, or an argument/configuration error occurred, and
`2` when the file selection (positional globs, or configured `include`/`exclude` patterns)
matched zero files. The `2` exit code exists because
`filesChecked: 0` alone is indistinguishable from a genuine clean pass to a caller that only
inspects the exit code — a typo'd glob, a misconfigured include pattern, or a moved directory
could otherwise silently "pass" a CI job that verified nothing. The JSON report also carries
an explicit `noFilesMatched: true` field for this case. When an empty selection is
legitimately expected (for example, a glob scoped to a directory that does not always exist),
pass `--allow-empty` to accept it as a normal success:

```bash
ste100mark docs/optional/**/*.md --allow-empty
```

# Command-Line Options

The following command-line options are supported:

| Option | Description |
| --- | --- |
| `[globs...]` | Optional Markdown globs/paths to lint (relative or absolute). Defaults to include/exclude patterns. |
| `-v`, `--version` | Display version information |
| `-?`, `-h`, `--help` | Display help message |
| `--silent` | Suppress console output |
| `--validate` | Run self-validation |
| `--results <file>`, `--result <file>` | Write validation results to file (TRX or JUnit format) |
| `--depth <#>` | Set heading depth for markdown output (default: 1) |
| `--log <file>` | Write output to log file |
| `--config <file>` | Path to lint configuration file (default lookup: `.ste100mark.yaml`) |
| `--format <text\|json>` | Diagnostic output format for linting (default: `text`) |
| `--strict` | Promote warn-severity lint findings to a failing exit code |
| `--allow-empty` | Treat a file selection that matches zero files as success (default: exit code 2) |
| `--list-rules` | Display the rule catalog as a JSON array and exit |

# Examples

## Example 1: Lint Markdown Files

```bash
ste100mark docs/**/*.md
```

## Example 2: Lint with JSON Output and Explicit Config

```bash
ste100mark docs/**/*.md --config .ste100mark.yaml --format json
```

## Example 3: Strict Mode

```bash
ste100mark docs/**/*.md --strict
```

## Example 4: Self-Validation with Results

```bash
ste100mark --validate --results validation-results.trx
```

## Example 5: Silent Mode with Logging

```bash
ste100mark --silent --log tool-output.log
```

## References

N/A
