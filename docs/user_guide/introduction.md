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

This template follows the
[Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance) methodology, which ensures
compliance evidence is generated automatically on every CI run.

## Key Practices

- **Requirements Traceability**: Every requirement is linked to passing tests, and a trace matrix is
  auto-generated on each release
- **Linting Enforcement**: markdownlint, cspell, and yamllint are enforced before any build proceeds
- **Automated Audit Documentation**: Each release ships with generated requirements, justifications,
  trace matrix, and quality reports
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
overrides:
  - glob: docs/user_guide/procedures/**/*.md
    mode: procedure
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
```

Configuration fields:

- `include` - file-selection globs used when no positional globs are supplied
- `exclude` - exclusions applied to `include`
- `default-mode` - `descriptive` or `procedure`
- `overrides` - first-match-wins glob-to-mode mappings
- `rules` - rule tuning for sentence limits, semicolons, contractions, paragraph cap, and
  passive voice
- `dictionary` - project dictionary file plus inline allow/disallow/ignore tuning

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

# Command-Line Options

The following command-line options are supported:

| Option | Description |
| --- | --- |
| `[globs...]` | Optional Markdown glob patterns to lint. When omitted, configuration `include` and `exclude` patterns are used. |
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
