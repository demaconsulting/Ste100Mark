---
title: Command-line options
summary: Every option of the tool, and the order in which a run handles them.
aliases:
  - options
  - flags
  - usage
  - arguments
tags:
  - reference
order: 2
see-also:
  - configuration
  - output
  - troubleshooting
examples:
  - name: usage
    kind: command
    language: shell
    description: Print the short usage text. It ends with a pointer to this help.
    content: '{{tool}} --help'
    expect: "Run '{{tool}} help' for topics and examples."
  - name: glob
    kind: command
    language: shell
    description: Check the files that a quoted glob matches.
    content: '{{tool}} "fixed*.md"'
    expect: 'Checked 1 file(s)'
  - name: silent-log
    kind: command
    language: shell
    description: Hide the console output and keep it in a log file.
    content: '{{tool}} --silent --log output.log --version'
  - name: unknown-option
    kind: command
    language: shell
    description: An unknown argument is an error, printed even with --silent. Exits 1.
    content: '{{tool}} --silent --unknown'
    exit: 1
    expect: "Error: Unsupported argument '--unknown'"
---
Arguments that do not start with `-` are globs of the Markdown files to
check ([[#glob]]). A plain path is a glob without wildcards, and either
may be relative or absolute. Globs replace the `include` and `exclude`
settings of the configuration. Options may be given in any order.

| Option | Meaning |
| --- | --- |
| `-v`, `--version` | Print only the version. |
| `-?`, `-h`, `--help` | Print the short usage text. |
| `--silent` | Hide console output. Errors still set the exit code. |
| `--log <file>` | Also write the output to a file. |
| `--config <file>` | The configuration file; see [[configuration]]. |
| `--format <name>` | `text` (default), `json`, or `json-detailed`. |
| `--context-lines <n>` | Context lines for `json-detailed`, 0 to 10; default 2. |
| `--strict` | Warnings also fail the run (exit 1). |
| `--allow-empty` | An empty file selection passes (exit 0). |
| `--list-rules` | Print the rule catalog as JSON; see [[rules]]. |
| `--validate` | Run the self-tests; see [[self-validation]]. |
| `--results <file>` | Self-test results as `.trx` or `.xml`. |
| `--depth <n>` | First heading level of the self-test report, 1 to 6. |

`--result` also works for `--results`. [[output]] explains the formats.

**Order.** `--version`, when all arguments are valid, prints only the
version and skips everything else. Else `--list-rules` prints the rule
catalog. Else the tool prints a banner, which a check with `--format json`
or `json-detailed` leaves out, then handles the first of `--help`,
`--validate`, or the check of the files. The command that
[[#silent-log]] prints writes the version to the log file only.

**Errors.** An unknown argument, an option without its value, or a number
out of range prints `Error: ...` and exits 1 ([[#unknown-option]]). These
errors are found before `--silent` and `--log` take effect, so they are
always printed and never logged. `--silent` hides all other output,
including the errors of a check.

**Help.** `help` is a command, not an option, and must be the first
argument. `--silent` and `--log` do not apply to it, and an unknown topic
exits 2.
