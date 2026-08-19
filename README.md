# Ste100Mark

[![GitHub forks][badge-forks]][link-forks]
[![GitHub stars][badge-stars]][link-stars]
[![GitHub contributors][badge-contributors]][link-contributors]
[![License][badge-license]][link-license]
[![Build][badge-build]][link-build]
[![Quality Gate][badge-quality]][link-quality]
[![Security][badge-security]][link-security]
[![NuGet][badge-nuget]][link-nuget]

Ste100Mark is a .NET CLI tool for linting Markdown prose against ASD-STE100-style rules,
while also demonstrating DEMA Consulting practices for command-line tooling,
self-validation, and continuous-compliance documentation.

## Features

Ste100Mark provides:

- 🔎 **ASD-STE100 Linting**: Checks Markdown sentences against configurable STE100 style rules
- 🗂️ **Configurable File Scope**: Selects files using globs, includes, and overrides
- 🧾 **Text and JSON Output**: Produces readable diagnostics or structured CI-friendly output
- 🚦 **Strict Mode**: Promotes warnings to build-breaking failures on demand
- 📖 **Embedded Example Dictionary**: Ships illustrative vocabulary for immediate linter evaluation
- ✅ **Self-Validation**: Built-in validation tests with TRX and JUnit output
- 🖥️ **Multi-Platform Support**: Builds and runs on Windows, Linux, macOS
- ⚙️ **Multi-Runtime Support**: Targets .NET 8, 9, and 10 runtimes
- 🔁 **Comprehensive CI/CD**: GitHub Actions workflows for quality checks, builds, tests
- 🛡️ **Continuous Compliance**: Generates automatic compliance evidence on every CI run,
  following the [Continuous Compliance][link-continuous-compliance] methodology
- 📚 **Documentation Generation**: Automates build notes, user guide, and compliance reports

## Installation

Install the tool globally using the .NET CLI:

```bash
dotnet tool install -g DemaConsulting.Ste100Mark
```

## Usage

```bash
# Lint Markdown files selected by positional globs
ste100mark docs/**/*.md

# Use the default .ste100mark.yaml in the current directory
ste100mark

# Use an explicit configuration file and JSON output
ste100mark docs/**/*.md --config .ste100mark.yaml --format json

# Promote warnings to a failing exit code
ste100mark docs/**/*.md --strict

# Display version
ste100mark --version

# Display help
ste100mark --help

# Run self-validation
ste100mark --validate
```

## ASD-STE100 Linting

The default tool path runs the Markdown linter:

```bash
ste100mark [globs...] [--config <file>] [--format text|json] [--strict]
```

When `[globs...]` is omitted, Ste100Mark uses the `include` and `exclude` patterns from the
resolved configuration file. If no configuration file is present, the tool falls back to
`**/*.md` in the current working directory.

Example configuration:

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

Mechanical rules reported by the linter are `STE100-4.1` (sentence length), `STE100-8.1`
(semicolons), `STE100-4.2` (contractions), and `STE100-DICT` (dictionary enforcement).
Advisory heuristics are `STE100-ADV-PARA` (paragraph length), `STE100-ADV-PASSIVE`
(passive voice), `STE100-ADV-COMPLEXVERB` (perfect/modal-perfect tense), and
`STE100-ADV-INGFORM` (`-ing` form).

> **Important dictionary notice:** The embedded default dictionary is a small,
> originally-authored, illustrative and representative example. It is **not** the official
> ASD-STE100 Part 2 Dictionary. Projects that require true ASD-STE100 Issue 9 compliance
> must supply your organization's licensed ASD-STE100 dictionary through `dictionary.file`.
> Ste100Mark does not provide full official ASD-STE100 dictionary content out of the box.

### Project Dictionary Files

A project dictionary file (referenced by `dictionary.file`) is a YAML document whose
top-level keys are the disallowed terms. Each term maps to a **list of one or more
part-of-speech-tagged senses**. Each sense has a `pos` (`noun`, `verb`, `adjective`,
`adverb`, or `any` for a role-independent connector), an `alternatives` list of suggested
replacement words or phrases for that sense, and an optional free-text `note` explaining the
rationale. Ste100Mark merges this file with the embedded default dictionary (unless
`dictionary.use-embedded: false` is set) and the inline `dictionary.disallow` entries.

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

A term with exactly one sense (any `pos` value) is always reported using that sense,
regardless of context — no ambiguity is possible when only one sense exists. When a term has
more than one sense, Ste100Mark applies a lightweight, deterministic part-of-speech heuristic
to the surrounding sentence context to decide which sense applies. When the heuristic can
confidently tell noun from verb usage apart, only the matching sense is reported, its
alternatives are joined with natural "or"/Oxford-comma phrasing, and the diagnostic message
notes the grammatical role, for example: `Avoid 'impact'; use 'effect' instead (used as a
noun).` When the heuristic cannot decide, every sense is reported, grouped per part of speech
and clearly labeled as ambiguous, for example: `Ambiguous part of speech for 'impact' —
possible corrections: as a noun, use 'effect'; as a verb, use 'affect'.`

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

Use `--format json` when you need one machine-readable JSON document on stdout. Use
`--strict` when you want warn-severity findings to fail the run without changing their
reported severity.

## Command-Line Options

| Option | Description |
| --- | --- |
| `[globs...]` | Optional Markdown glob patterns to lint. When omitted, configuration `include` and `exclude` patterns are used. |
| `-v`, `--version` | Display version information |
| `-?`, `-h`, `--help` | Display help message |
| `--silent` | Suppress console output |
| `--validate` | Run self-validation |
| `--results <file>`, `--result <file>` | Write validation results to `.trx` (TRX) or `.xml` (JUnit XML) file |
| `--depth <#>` | Set heading depth for markdown output (default: 1) |
| `--log <file>` | Write output to log file |
| `--config <file>` | Path to lint configuration file (default lookup: `.ste100mark.yaml`) |
| `--format <text\|json>` | Diagnostic output format for linting (default: `text`) |
| `--strict` | Promote warn-severity lint findings to a failing exit code |

## Error Handling

Unrecognized arguments cause the tool to print an error message to standard error and exit
with a non-zero exit code. Configuration and dictionary file problems also produce a
non-zero exit code. For example:

```text
Error: Unsupported argument '--unknown'
```

This behavior enables CI/CD pipelines to detect and surface misconfiguration failures
automatically.

## Self Validation

Running self-validation produces a report containing the following information:

```text
# DEMA Consulting Ste100Mark

| Information         | Value                                              |
| :------------------ | :------------------------------------------------- |
| Tool Version        | <version>                                          |
| Machine Name        | <machine-name>                                     |
| OS Version          | <os-version>                                       |
| DotNet Runtime      | <dotnet-runtime-version>                           |
| Time Stamp          | <timestamp> UTC                                    |

✓ Ste100Mark_VersionDisplay - Passed
✓ Ste100Mark_HelpDisplay - Passed
✓ Ste100Mark_LintCleanFileNoDiagnostics - Passed
✓ Ste100Mark_LintViolationFileDetectsIssues - Passed
✓ Ste100Mark_LintJsonOutputIsValidJson - Passed

Total Tests: 5
Passed: 5
Failed: 0
```

Each test in the report proves:

- **`Ste100Mark_VersionDisplay`** - `--version` outputs a valid version string.
- **`Ste100Mark_HelpDisplay`** - `--help` outputs usage and options information.
- **`Ste100Mark_LintCleanFileNoDiagnostics`** - linting a fully compliant Markdown file
  produces no diagnostics and a zero exit code.
- **`Ste100Mark_LintViolationFileDetectsIssues`** - linting a file with deliberate
  violations detects each expected rule code and returns a non-zero exit code.
- **`Ste100Mark_LintJsonOutputIsValidJson`** - `--format json` output parses as valid JSON.

Use `--depth <#>` to control the heading level of the validation output (default: `1`).
This is useful when embedding validation output into a larger markdown document:

```bash
# Embed validation at heading level 2
ste100mark --validate --depth 2
```

See the [User Guide][link-guide] for more details on linting and self-validation.

On validation failure the tool will exit with a non-zero exit code.

## Documentation

Generated documentation includes:

- **Build Notes**: Release information and changes
- **User Guide**: Comprehensive usage documentation
- **Code Quality Report**: CodeQL and SonarCloud analysis results
- **Requirements**: Functional and non-functional requirements
- **Verification**: Requirement-to-test mapping and verification evidence design

## Contributing

See [CONTRIBUTING.md](https://github.com/demaconsulting/Ste100Mark/blob/main/CONTRIBUTING.md) for
guidelines on reporting bugs, suggesting features, and submitting pull requests.

## License

Copyright (c) DEMA Consulting. Licensed under the MIT License. See [LICENSE][link-license] for details.

By contributing to this project, you agree that your contributions will be licensed under the MIT License.

<!-- Badge References -->
[badge-forks]: https://img.shields.io/github/forks/demaconsulting/Ste100Mark?style=plastic
[badge-stars]: https://img.shields.io/github/stars/demaconsulting/Ste100Mark?style=plastic
[badge-contributors]: https://img.shields.io/github/contributors/demaconsulting/Ste100Mark?style=plastic
[badge-license]: https://img.shields.io/github/license/demaconsulting/Ste100Mark?style=plastic
[badge-build]: https://img.shields.io/github/actions/workflow/status/demaconsulting/Ste100Mark/build_on_push.yaml?style=plastic
[badge-quality]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_Ste100Mark&metric=alert_status
[badge-security]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_Ste100Mark&metric=security_rating
[badge-nuget]: https://img.shields.io/nuget/v/DemaConsulting.Ste100Mark?style=plastic

<!-- Link References -->
[link-forks]: https://github.com/demaconsulting/Ste100Mark/network/members
[link-stars]: https://github.com/demaconsulting/Ste100Mark/stargazers
[link-contributors]: https://github.com/demaconsulting/Ste100Mark/graphs/contributors
[link-license]: https://github.com/demaconsulting/Ste100Mark/blob/main/LICENSE
[link-build]: https://github.com/demaconsulting/Ste100Mark/actions/workflows/build_on_push.yaml
[link-quality]: https://sonarcloud.io/dashboard?id=demaconsulting_Ste100Mark
[link-security]: https://sonarcloud.io/dashboard?id=demaconsulting_Ste100Mark
[link-nuget]: https://www.nuget.org/packages/DemaConsulting.Ste100Mark
[link-guide]: https://github.com/demaconsulting/Ste100Mark/blob/main/docs/user_guide/introduction.md
[link-continuous-compliance]: https://github.com/demaconsulting/ContinuousCompliance
