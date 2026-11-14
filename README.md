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

The workflow below shows how to use it: prepare your Markdown files and an optional `.ste100mark.yaml`, run
`ste100mark` from the command line or a CI pipeline, and use the reported findings and exit code as a writing-quality
gate.

![Ste100Mark workflow: prepare Markdown files, an optional .ste100mark.yaml with profiles, and a dictionary file, run
ste100mark with globs and the config, format, and strict options, get text or JSON diagnostics with rule codes, a run
summary, and an exit code, then use them as a CI writing-quality gate, in pull request reviews, for tool integration,
and for clearer documentation][workflow]

## Features

Ste100Mark provides:

- 🔎 **ASD-STE100 Linting**: Checks Markdown sentences against configurable STE100 style rules
- 🗂️ **Configurable File Scope**: Selects files using globs, includes, and overrides
- 🧾 **Text and JSON Output**: Produces readable diagnostics or structured CI-friendly output
- 🔬 **Detailed JSON Output**: Opt-in `json-detailed` format adds sentence, context, tagger evidence,
  confidence class and suppression hints to each dictionary finding
- 🚦 **Strict Mode**: Promotes warnings to build-breaking failures on demand
- 📖 **Embedded Example Dictionary**: Ships illustrative vocabulary for immediate linter evaluation
- 🔤 **Word-Form Matching**: Flags inflected spellings of disallowed verbs and nouns (for example
  "produced" for "produce", when used as a verb), with `dictionary.match-word-forms: false` to switch it off
- 🧩 **Project Glossary**: Approves project terms with a part of speech, and reports deprecated terms
  with a replacement
- ✅ **Self-Validation**: Built-in validation tests with TRX and JUnit output
- 🖥️ **Multi-Platform Support**: Builds and runs on Windows, Linux, macOS
- ⚙️ **Multi-Runtime Support**: Targets .NET 10 and 11 runtimes
- 🔁 **Comprehensive CI/CD**: GitHub Actions workflows for quality checks, builds, tests
- 🛡️ **Continuous Compliance**: This repository's CI pipeline generates compliance evidence and
  reports using the [Continuous Compliance][link-continuous-compliance] methodology
- 📚 **Repository Documentation Tooling**: This repository also automates build notes, user guide,
  and compliance report generation

## Installation

Install the tool globally using the .NET CLI:

```bash
dotnet tool install -g DemaConsulting.Ste100Mark
```

## Quick Start

```bash
# Lint Markdown files selected by globs
ste100mark docs/**/*.md

# Use the .ste100mark.yaml in the current directory, or pick a file and a JSON report
ste100mark
ste100mark docs/**/*.md --config .ste100mark.yaml --format json

# Fail the run on warnings too
ste100mark docs/**/*.md --strict

# Show the machine-readable rule catalog
ste100mark --list-rules
```

A minimal `.ste100mark.yaml`:

```yaml
include:
  - docs/**/*.md
exclude:
  - docs/**/generated/**
default-mode: descriptive
rules:
  max-words-descriptive: 25
dictionary:
  file: your-licensed-asd-ste-dictionary.yaml
  use-embedded: false
```

> **Dictionary notice:** The embedded dictionary is a small, illustrative example. It is **not** the
> official ASD-STE100 Part 2 Dictionary. Organizations that need true compliance must supply their own
> licensed dictionary through `dictionary.file`.

## Project Glossary

A project glossary pins your product names and domain terms, with a part of speech for each, so the
part-of-speech tagger stops producing false findings on them. Hard mode (the default) makes the tagger
use your part of speech. Configure it under `dictionary`:

```yaml
dictionary:
  glossary-file: docs/glossary.yaml
  deprecated-file: docs/deprecated-terms.yaml
  glossary-mode: hard   # hard (default) or soft
```

Every term in a glossary file needs a definition that is not empty. Use a plain string, or an object
for more detail:

```yaml
# docs/glossary.yaml
Test Mode: A mode of Product A that runs the acceptance tests.
Zorbix:
  definition: The name of the control unit that Acme supplies for Product A.
  pos: proper-noun
  variants: [Zorbixes]
  visible: false
bleed valve:
  definition: A valve that lets air out of a closed system.
  pos: noun
```

A deprecated-terms file reports a retired term with its replacement. It needs no definitions:

```yaml
# docs/deprecated-terms.yaml
Acme Hub:
  use: Zorbix
  where: Documents before version 2.
```

A missing or empty definition is an error. The [User Guide][link-guide-glossary] explains path rules
(relative, absolute, UNC, and `${VAR}`), conflict rules, and risks.

## Learn More

The [User Guide][link-guide] covers everything else:

- [Configuration File][link-guide-config] and [Profiles][link-guide-profiles]
- [Project Dictionary Files][link-guide-dictionary] and [Word-Form Matching][link-guide-word-forms]
- [Detailed JSON Output][link-guide-json]
- [Error Handling][link-guide-errors] and exit codes
- [Self-Validation][link-guide-validation]
- [Command-Line Options][link-guide-options]

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

Copyright (c) DEMA Consulting. The code is licensed under the MIT License. See [LICENSE][link-license] for details.

The NuGet package also bundles the `DemaConsulting.PartOfSpeech` library (a dependency of the tool), whose assembly
embeds language data adapted from Open English WordNet (CC BY 4.0) and the Manually Annotated Sub-Corpus (CC BY 3.0
US). The license of
the package is [PACKAGE-LICENSE.md][link-package-license], and the required attribution is in
[THIRD-PARTY-NOTICES.md][link-third-party-notices].

By contributing to this project, you agree that your contributions will be licensed under the MIT License.

<!-- Badge References -->
[workflow]: https://raw.githubusercontent.com/demaconsulting/Ste100Mark/main/docs/images/workflow.png
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
[link-package-license]: https://github.com/demaconsulting/Ste100Mark/blob/main/PACKAGE-LICENSE.md
[link-third-party-notices]: https://github.com/demaconsulting/Ste100Mark/blob/main/THIRD-PARTY-NOTICES.md
[link-build]: https://github.com/demaconsulting/Ste100Mark/actions/workflows/build_on_push.yaml
[link-quality]: https://sonarcloud.io/dashboard?id=demaconsulting_Ste100Mark
[link-security]: https://sonarcloud.io/dashboard?id=demaconsulting_Ste100Mark
[link-nuget]: https://www.nuget.org/packages/DemaConsulting.Ste100Mark
[link-continuous-compliance]: https://github.com/demaconsulting/ContinuousCompliance
[link-guide]: https://github.com/demaconsulting/Ste100Mark/blob/main/docs/user_guide/introduction.md
[link-guide-config]: https://github.com/demaconsulting/Ste100Mark/blob/main/docs/user_guide/introduction.md#configuration-file
[link-guide-profiles]: https://github.com/demaconsulting/Ste100Mark/blob/main/docs/user_guide/introduction.md#profiles
[link-guide-dictionary]: https://github.com/demaconsulting/Ste100Mark/blob/main/docs/user_guide/introduction.md#project-dictionary-files
[link-guide-word-forms]: https://github.com/demaconsulting/Ste100Mark/blob/main/docs/user_guide/introduction.md#word-form-matching
[link-guide-glossary]: https://github.com/demaconsulting/Ste100Mark/blob/main/docs/user_guide/introduction.md#project-glossary
[link-guide-json]: https://github.com/demaconsulting/Ste100Mark/blob/main/docs/user_guide/introduction.md#detailed-json-output
[link-guide-errors]: https://github.com/demaconsulting/Ste100Mark/blob/main/docs/user_guide/introduction.md#error-handling
[link-guide-validation]: https://github.com/demaconsulting/Ste100Mark/blob/main/docs/user_guide/introduction.md#self-validation
[link-guide-options]: https://github.com/demaconsulting/Ste100Mark/blob/main/docs/user_guide/introduction.md#command-line-options
