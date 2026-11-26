---
title: Troubleshooting
summary: Exit codes, common error messages, and how to fix them.
aliases:
  - errors
  - exit-codes
  - problems
  - faq
tags:
  - troubleshooting
order: 10
see-also:
  - command-line
  - configuration
  - suppressions
examples:
  - name: unknown-option
    kind: command
    language: shell
    description: An unknown option. Exits 1.
    content: '{{tool}} --unknown'
    exit: 1
    expect: "Error: Unsupported argument '--unknown'"
  - name: version-bad-argument
    kind: command
    language: shell
    description: --version does not hide a bad argument. Exits 1.
    content: '{{tool}} --version --bogus'
    exit: 1
    expect: "Error: Unsupported argument '--bogus'"
  - name: no-files
    kind: command
    language: shell
    description: No file matches the glob. Exits 2.
    content: '{{tool}} "docs/**/*.md"'
    exit: 2
    expect: 'found no files to check'
  - name: context-lines
    kind: command
    language: shell
    description: --context-lines needs the detailed format. Exits 1.
    content: '{{tool}} fixed.md --context-lines 3'
    exit: 1
    expect: '--context-lines is only valid with --format json-detailed'
  - name: missing-config
    kind: command
    language: shell
    description: The configuration file does not exist. Exits 1.
    content: '{{tool}} --config nope.yaml fixed.md'
    exit: 1
    expect: 'not found'
  - name: unknown-topic
    kind: command
    language: shell
    description: A help topic that does not exist. Exits 2.
    content: '{{tool}} help no-such-topic'
    exit: 2
    expect: 'no-such-topic'
---

| Exit code | Meaning |
| --- | --- |
| 0 | Success: no errors, or warnings only without `--strict`. |
| 1 | Errors in the text, or an argument or configuration error. |
| 2 | No file matched, or an unknown help topic ([[#unknown-topic]]). |

**Argument errors** print `Error: ...` and exit 1, even with `--silent`,
and are never logged ([[#unknown-option]]). `--version` prints only when
every argument is valid ([[#version-bad-argument]]).
`--context-lines` needs `--format json-detailed` ([[#context-lines]]).

**No files matched** ([[#no-files]]). Quote the globs, run from the
folder that the globs start at, or add `--allow-empty` when an empty
selection is expected.

**Configuration errors** exit 1. `Lint configuration file ... not found`
names a missing `--config` file ([[#missing-config]]).
`Failed to parse lint configuration file` means a wrong value. A
misspelled key is silently ignored, so a setting that has no effect may
be misspelled. `Dictionary file ... was not found` names the full path:
paths in `dictionary` are relative to the configuration file. A glossary
or deprecated file can fail with `matched no files`, a missing
`definition`, or a `Conflict`.

**Quiet failures.** `--silent` hides the findings and the configuration
errors; only the exit code remains. `--format json` prints no failure
line; read `errorCount`.

**Surprises.** A file named `help` must be written `./help`, because
`help` as the first argument asks for help. HTML comments are checked as
prose. To accept a false positive, see [[suppressions]].
