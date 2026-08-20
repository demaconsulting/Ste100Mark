## Program

![Ste100Mark Structure](Ste100MarkView.svg)

### Purpose

`Program` is the entry point and execution orchestrator for the Ste100Mark. Its single
responsibility is to create a `Context` from the command-line arguments, dispatch to the
appropriate handler based on the parsed flags, and return the exit code.

### Data Model

**Version**: `string` (static property) — The tool version read from
`AssemblyInformationalVersionAttribute` on every access, falling back to `AssemblyVersion`, then
`"0.0.0"`. No caching is applied; callers that need the value more than once should store it
locally.

### Key Methods

**Main**: Entry point for the tool process.

- *Parameters*: `string[] args` — command-line arguments from the host environment.
- *Returns*: `int` — exit code; 0 for success, 1 for expected errors.
- *Preconditions*: None.
- *Postconditions*: Exit code reflects whether any errors were reported during execution.

Creates a `Context` using `Context.Create(args)`, calls `Run(context)`, and returns
`context.ExitCode`. Catches `ArgumentException` and `InvalidOperationException` — writes
`"Error: {message}"` to stderr and returns 1. Catches any other `Exception` — writes
`"Unexpected error: {message}"` to stderr and re-throws so the runtime can record it.

**Run**: Dispatches execution based on parsed flags.

- *Parameters*: `Context context` — the parsed context.
- *Returns*: `void`.
- *Preconditions*: `context` is not null.
- *Postconditions*: Exactly one handler has been called.

Inspects flags in priority order. First, if `context.Version` is true, it writes only the
version string and returns without printing the banner. Otherwise it determines banner behavior
before dispatch: the banner is suppressed only when `context.Format == OutputFormat.Json` and both
`context.Help` and `context.Validate` are false, because the main lint path must keep stdout to a
single JSON document. For all other paths, including `--help --format json` and
`--validate --format json`, `Run` prints the banner first. After that it dispatches in order:
(1) help via `PrintHelp`; (2) self-validation via `Validation.Run(context)`; (3) main lint tool
logic via `RunToolLogic(context)`. Exactly one path runs per invocation.

**PrintBanner**: Writes the tool name, version, and copyright line to `context`.

- *Parameters*: `Context context` — output target.
- *Returns*: `void`.

**PrintHelp**: Writes the usage synopsis and the umbrella CLI options table to `context`,
including the lint-related options `--config`, `--format`, `--strict`, and the optional input
glob arguments used by the main lint workflow.

- *Parameters*: `Context context` — output target.
- *Returns*: `void`.

**RunToolLogic**: Invokes the current main lint tool implementation by calling
`Linter.Run(context)`. The method remains the structural hook where downstream projects could
replace the dispatched tool behavior, but in the current implementation it routes directly to the
linter.

- *Parameters*: `Context context` — output target.
- *Returns*: `void`.

### Error Handling

`Main` handles errors at two levels. Expected errors (`ArgumentException` and
`InvalidOperationException`) are written to stderr as `"Error: {message}"` and cause exit
code 1 without a stack trace. Unexpected errors (`Exception`) are written to stderr as
`"Unexpected error: {message}"` and re-thrown so the runtime can record them in event logs.
`Run`, `PrintBanner`, `PrintHelp`, and `RunToolLogic` do not catch exceptions; all errors
propagate to `Main`.

### Dependencies

- **Context** — `Program` reads parsed flags from `Context` and calls `Context.WriteLine` and
  `Context.WriteError` for all output.
- **Validation** — `Program.Run` calls `Validation.Run(context)` when the `--validate` flag is
  set.
- **Linter** — `Program.RunToolLogic` calls `Linter.Run(context)` for the main lint execution path.

### Callers

N/A - entry point, called by the host environment.
