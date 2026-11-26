## Cli

![Cli Structure](CliView.svg)

### Overview

The `Cli` subsystem is the boundary between the host environment's command-line invocation and
the tool's internal logic. It owns argument parsing, output channel management, and exit-code
tracking. All other subsystems receive a `Context` object from this subsystem to read parsed
flags and write output. It also answers the built-in `help` command. The `Cli` subsystem
contains two units: `Context` and `HelpCommand`.

### Interfaces

**Context.Create**: Factory method that constructs a `Context` from a command-line argument array.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Parses `string[] args` into flag properties and opens the log file if `--log` is
  present. Returns a fully initialized `Context`. Accepts `--result` as a legacy alias for
  `--results`, stores an explicit lint configuration path from `--config`, stores `text` or
  `json` or `json-detailed` output-format selection from `--format`, stores the number of context
  lines from `--context-lines` (0-10, default 2, accepted only with `--format json-detailed`),
  records whether `--strict` was requested,
  records whether `--allow-empty` was requested, and records whether `--list-rules` was
  requested.
- *Constraints*: Throws `ArgumentException` for unknown or malformed arguments; throws
  `InvalidOperationException` when the log file cannot be opened.

**Context.WriteLine**: Writes a message to stdout and to the log file.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Writes `message` to `Console.Out` and to the log file if one is open. Stdout
  output is suppressed when `Silent` is true; the log file always receives the message.
- *Constraints*: None.

**Context.WriteError**: Writes an error message and sets the error exit code.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Sets `_hasErrors` to true, writes `message` to `Console.Error`, temporarily requests
  `ConsoleColor.Red` before writing, restores the previous foreground color afterward, and writes
  to the log file if one is open. Color is best-effort and host-dependent; some terminals and CI
  log captures may not render it. Stderr routing, not color rendering, is what the automated
  subsystem tests verify. Stderr output is suppressed when `Silent` is true, but `ExitCode` is set
  to 1 regardless.
- *Constraints*: Once set, `ExitCode` cannot return to 0 within the same invocation.

**Context.WriteNoFilesMatchedError**: Writes the zero-files-matched failure message and sets the
distinct exit-code state.

- *Type*: In-process .NET instance method.
- *Role*: Provider.
- *Contract*: Sets `_noFilesMatched` to true and shares `WriteError`'s console/log-writing body,
  so the message reaches stderr (unless `Silent`) and the log file (if open) the same way.
- *Constraints*: Does not set `_hasErrors`; used instead of `WriteError` so `ExitCode` returns the
  distinct `NoFilesMatchedExitCode` (2) rather than 1.

**Context.MarkNoFilesMatched**: Sets the zero-files-matched exit-code state without console/log
output.

- *Type*: In-process .NET method (`internal`).
- *Role*: Provider.
- *Contract*: Sets `_noFilesMatched` to true with no output, mirroring `MarkFailure`, for callers
  that already reported the condition through a self-contained JSON document.
- *Constraints*: None.

**Context.ExitCode**: Derived property returning 0, 1, or 2.

- *Type*: In-process .NET property.
- *Role*: Provider.
- *Contract*: Returns 1 if `WriteError` (or `MarkFailure`) has been called at least once;
  otherwise returns `NoFilesMatchedExitCode` (2) if the file selection matched zero files and
  `AllowEmpty` was not specified; otherwise returns 0.
- *Constraints*: Read-only. `_hasErrors` takes precedence over the zero-files condition so a
  reported error is never masked.

**Context.Dispose**: Releases the log file `StreamWriter`.

- *Type*: In-process .NET method (`IDisposable`).
- *Role*: Provider.
- *Contract*: Disposes `_logWriter` and sets it to null; flushes any buffered content. Callers
  must use a `using` statement to guarantee disposal.
- *Constraints*: Safe to call multiple times (idempotent after first call).

**HelpCommand.TryRun**: Answers the `help` command from the built-in help cards.

- *Type*: In-process .NET instance method (on the shared `HelpCommand.Default` instance).
- *Role*: Provider.
- *Contract*: Returns `false`, writing nothing, when the first argument is not `help`. Otherwise
  writes the answer to the console, returns `true`, and sets the exit code (see *Help Command* in
  the system design).
- *Constraints*: Does not throw for user input; does not use `Context`.

**HelpCommand.Footer**: The sentence that points a reader of the usage text to the help command.

- *Type*: In-process .NET property.
- *Role*: Provider.
- *Contract*: Returns the one-line pointer that HelpCards supplies.
- *Constraints*: Reading it never builds the help catalog.

### Design

The `Cli` subsystem has no subsystem-level code of its own; its behavior is provided by the
`Context` and `HelpCommand` units, which do not call each other. `Program.Main` offers the
arguments to `HelpCommand.TryRun` first. When the help command answers, the invocation ends
with its exit code and no `Context` is created. Otherwise `Program` creates a `Context` and
passes it to all other units that produce output.

`HelpCommand` writes its answers to the console itself. It is the one exception to `Context`
owning all output, because the `help` command is answered before a `Context` exists.

The subsystem has no dependencies on other tool subsystems. `Context` uses only .NET BCL types
(`Console`, `StreamWriter`); `HelpCommand` uses the HelpCards OTS library and reads the
tool version from `Program.Version`.

`Program.Run` dispatches in priority order: `--version`, then `--list-rules`, then help, then
self-validation, then the
main lint flow. `--list-rules` is dispatched immediately after `--version` and before the banner
decision so its JSON output remains a single parseable document. During main lint flow,
`--format json` and `--format json-detailed` (both true for `OutputFormat.IsJson()`) suppress the
normal banner so stdout can
remain a single parseable JSON document; help and self-validation still emit the banner even if a
JSON format argument is present.

The lint path uses `Context.ConfigFile`, `Context.Format`, `Context.Strict`, and
`Context.AllowEmpty` exactly as parsed. `Context.ListRules` is consumed only by `Program.Run`'s
dispatch decision, not by the lint path itself.
`Linter` passes an explicit `--config` path through to configuration loading, defaults to
`.ste100mark.yaml` only when no explicit path was supplied, reports diagnostics in the selected
text or JSON format, treats `--strict` as an exit-code policy that promotes warn-severity
findings to failure without changing the severities reported in diagnostic output, and treats a
zero-matched file selection as a distinct failure (exit code 2) unless `--allow-empty` was
specified.

Error handling flows from `Context.Create` to `Program.Main`: argument parsing errors propagate
as `ArgumentException`; log-file errors propagate as `InvalidOperationException`. Both are
caught and handled in `Program.Main`, which writes the error to stderr and returns exit code 1.

When `--log` is active, `Context` holds an open `StreamWriter` for the duration of the
invocation. The `Program.Main` call site wraps the `Context` in a `using` statement to ensure
the file handle is released and buffered content is flushed on exit.
