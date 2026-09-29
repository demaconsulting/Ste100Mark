### Context

![Cli Structure](CliView.svg)

#### Purpose

`Context` handles command-line argument parsing and program output for one tool invocation. Its
single responsibility is to parse the argument list, expose the parsed flags as read-only
properties, own the two output channels (console and log file), and derive the exit code from
whether any errors were reported.

#### Data Model

**_logWriter**: `StreamWriter?` — Log file writer; `null` when logging is not active.

**_hasErrors**: `bool` — Set to `true` on the first `WriteError` call; once set, cannot return
to `false` within the same invocation.

**_noFilesMatched**: `bool` — Set to `true` by `WriteNoFilesMatchedError` or
`MarkNoFilesMatched` when the file selection matched zero files and this was not accepted via
`AllowEmpty`.

**Version**: `bool` — `true` when `-v` or `--version` was present in the argument list.

**Help**: `bool` — `true` when `-?`, `-h`, or `--help` was present in the argument list.

**Silent**: `bool` — `true` when `--silent` was present in the argument list.

**Validate**: `bool` — `true` when `--validate` was present in the argument list.

**ResultsFile**: `string?` — Path supplied after `--results` or `--result`, or `null` if
neither flag was present.

**HeadingDepth**: `int` — Heading depth for markdown output; valid range 1–6, default 1;
supplied via `--depth`.

**AllowEmpty**: `bool` — `true` when `--allow-empty` was present in the argument list; opts a
zero-matched file selection back into a success exit code instead of the distinct
`NoFilesMatchedExitCode`.

**ListRules**: `bool` — `true` when `--list-rules` was present in the argument list; requests
that `Program.Run` dispatch to the rule-catalog JSON output path instead of the main lint flow.

**NoFilesMatchedExitCode**: `int` (const, `2`) — The distinct exit code returned by `ExitCode`
when the file selection matched zero files and `AllowEmpty` was not specified. Kept as a single
named constant so `Program`'s help text and the derivation logic below share one source of
truth.

**ExitCode**: `int` (derived) — Returns 1 if `_hasErrors` is true; otherwise returns
`NoFilesMatchedExitCode` (2) if `_noFilesMatched` is true; otherwise returns 0. `_hasErrors`
takes precedence so a genuine reported error is never masked by the zero-files exit code.

#### Key Methods

**Create**: Factory method that parses arguments and returns a fully initialized `Context`.

- *Parameters*: `string[] args` — raw command-line argument array.
- *Returns*: `Context` — a new instance with all flags set.
- *Preconditions*: `args` is not null.
- *Postconditions*: All flag properties reflect the parsed argument state, including
  `ListRules`; the log file is open
  if `--log` was supplied.

Delegates to the private `ArgumentParser` helper to parse flags, then opens the log file by
calling `OpenLogFile` if `--log` was present. Throws `ArgumentException` for unknown or
malformed arguments; throws `InvalidOperationException` if the log file cannot be opened.

**WriteLine**: Writes a message to standard output and to the log file.

- *Parameters*: `string message` — the message to write.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: Message is on stdout (unless `Silent`) and in the log file (if open).

**WriteError**: Writes an error message, sets the error state, and records to the log file.

- *Parameters*: `string message` — the error message.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: `_hasErrors` is true; message is on stderr in red (unless `Silent`) and in
  the log file (if open). Shares its console/log-writing body with
  `WriteNoFilesMatchedError` via the private `WriteErrorLine` helper.

**WriteNoFilesMatchedError**: Writes the zero-files-matched failure message and sets the
distinct exit-code state, without affecting `_hasErrors`.

- *Parameters*: `string message` — the error message.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: `_noFilesMatched` is true; message is on stderr in red (unless `Silent`)
  and in the log file (if open); `ExitCode` returns `NoFilesMatchedExitCode` (2) unless a
  separate error was also reported.

**MarkNoFilesMatched**: Sets the zero-files-matched exit-code state without any console or log
output, for JSON output mode where the single buffered JSON document must not be interleaved
with a stderr line (mirrors `MarkFailure`).

- *Parameters*: None.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: `_noFilesMatched` is true; no output is produced.

**Dispose**: Disposes the log file writer and is idempotent.

- *Parameters*: None.
- *Returns*: `void`.
- *Preconditions*: None.
- *Postconditions*: `_logWriter` is disposed and set to null; any buffered log content is
  flushed. Repeated calls are safe, do not throw, and have no additional effect after the first
  successful disposal.

#### Error Handling

`Create` throws `ArgumentException` ("Unsupported argument '{arg}'") for any unrecognized flag
or missing required value. It throws `InvalidOperationException`
("Failed to open log file '{path}': {detail}") when the `--log` file cannot be opened. Both
exceptions propagate to `Program.Main`.

`WriteLine` and `WriteError` do not throw; they write to whichever output channels are
available.

`Dispose` is idempotent: repeated calls are safe and do not throw. Any disposal errors are
silently ignored.

#### Dependencies

- **.NET BCL** — `Console`, `StreamWriter`, and `Path` are the only dependencies. No other
  tool units are used.

#### Callers

- **Program** — creates `Context` via `Context.Create` and calls `WriteLine` and `WriteError`.
- **Validation** — receives `Context` from `Program` and calls `WriteLine` and `WriteError`.
