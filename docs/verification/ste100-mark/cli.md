## Cli

### Verification Approach

The `Cli` subsystem is verified by integration tests defined in `CliSubsystemTests.cs`, plus
selected lint-flow unit tests in `LinterTests.cs` for behaviors whose externally visible effect is
produced only when the parsed CLI state reaches the lint orchestrator. Each integration test
exercises `Context.Create` and `Program.Run` together, treating the pair as the observable
subsystem interface. Tests pass controlled argument arrays and assert on captured console output,
file-system side-effects, parsed context state, and exit codes. `Validation` (part of the
`SelfTest` subsystem) executes its real logic in scenarios that exercise the `--validate` path; no
mocking is applied at any level.

### Test Environment

N/A - standard test environment.

### Acceptance Criteria

- All listed CLI integration and lint-flow tests pass with zero failures.
- Each supported CLI flag and alias produces its documented externally visible behavior.
- Errors are routed to stderr, while normal output routing remains on stdout unless `--silent` is
  active.
- Exit codes are 0 for successful flows and non-zero for rejected arguments, reported errors, and
  strict-mode lint failures.

### Test Scenarios

**CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits**: Arguments `[`"`--version`"`]`
are passed through `Context.Create` and `Program.Run`; standard output contains the version
string and exit code is 0. This scenario verifies `Ste100Mark-Cli-ArgumentParsing`,
`Ste100Mark-Cli-Version`, and `Ste100Mark-Cli-ExitCode`.

**CliSubsystem_VersionFlow_ContextAndProgram_DisplaysVersionAndExits_WithShortVFlag**: Arguments
`[`"`-v`"`]` are passed through `Context.Create` and `Program.Run`; standard output contains the
version string and exit code is 0. This scenario verifies `Ste100Mark-Cli-Version`.

**CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits**: Arguments `[`"`--help`"`]` are
passed through `Context.Create` and `Program.Run`; standard output contains help text and exit
code is 0. This scenario verifies `Ste100Mark-Cli-ArgumentParsing`, `Ste100Mark-Cli-Help`, and
`Ste100Mark-Cli-ExitCode`.

**CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortQuestionFlag**: Arguments
`[`"`-?`"`]` are passed; standard output contains help text and exit code is 0. This scenario
verifies `Ste100Mark-Cli-Help`.

**CliSubsystem_HelpFlow_ContextAndProgram_DisplaysHelpAndExits_WithShortHFlag**: Arguments
`[`"`-h`"`]` are passed; standard output contains help text and exit code is 0. This scenario
verifies `Ste100Mark-Cli-Help`.

**CliSubsystem_ValidateFlow_ContextAndProgram_RunsValidationAndExits**: Arguments
`[`"`--validate`"`]` are passed; standard output contains `"Total Tests:"` and exit code is 0.
This scenario verifies `Ste100Mark-Cli-ArgumentParsing`, `Ste100Mark-Cli-Validate`, and
`Ste100Mark-Cli-ExitCode`.

**CliSubsystem_SilentFlow_ContextAndProgram_SuppressesOutput**: Arguments
`[`"`--version`"`,`"`--silent`"`]` are passed; standard output and standard error are empty and
exit code is 0, confirming `--silent` suppresses console output. This scenario verifies
`Ste100Mark-Cli-ArgumentParsing`, `Ste100Mark-Cli-OutputChannels`, and `Ste100Mark-Cli-Silent`.

**CliSubsystem_ResultsFlow_ContextAndProgram_WritesResultsFile**: Arguments
`[`"`--validate`"`,`"`--silent`"`,`"`--results`"`,`"`<tmp>.trx`"`]` are passed; a results file
is created at the specified path and exit code is 0. This scenario verifies
`Ste100Mark-Cli-ArgumentParsing` and `Ste100Mark-Cli-Results`.

**CliSubsystem_ResultAliasFlow_ContextAndProgram_WritesResultsFile**: Arguments
`[`"`--validate`"`,`"`--silent`"`,`"`--result`"`,`"`<tmp>.trx`"`]` are passed; a results file is
created at the specified path and exit code is 0. This scenario verifies
`Ste100Mark-Cli-ResultAlias`.

**CliSubsystem_DepthFlow_ContextAndProgram_AdjustsHeadingDepth**: Arguments
`[`"`--validate`"`,`"`--silent`"`,`"`--depth`"`,`"`2`"`,`"`--log`"`,`"`<tmp>.log`"`]` are
passed; `Context.HeadingDepth` is 2, the log contains a level-2 heading, and exit code is 0.
This scenario verifies `Ste100Mark-Cli-ArgumentParsing` and `Ste100Mark-Cli-Depth`.

**CliSubsystem_LogFlow_ContextAndProgram_WritesLogFile**: Arguments
`[`"`--version`"`,`"`--log`"`,`"`<tmp>.log`"`]` are passed; a log file is created at the
specified path and contains version output. This scenario verifies `Ste100Mark-Cli-ArgumentParsing`
and `Ste100Mark-Cli-Log`.

**CliSubsystem_ConfigFlow_ContextAndProgram_AcceptsExplicitConfigPath**: Arguments
`[`"`--config`"`,`"`custom.ste100mark.yaml`"`]` are parsed through `Context.Create`; the explicit
configuration path is preserved in `Context.ConfigFile`. This scenario verifies
`Ste100Mark-Cli-ArgumentParsing` and `Ste100Mark-Cli-Config`.

**CliSubsystem_FormatFlow_ContextAndProgram_SelectsJsonOutputAndSuppressesBanner**: Arguments
`[`"`<tmp>.md`"`,`"`--format`"`,`"`json`"`]` are passed through `Context.Create` and
`Program.Run`; `Context.Format` is JSON, stdout begins with a JSON document, the normal banner is
suppressed, and exit code is 0. This scenario verifies `Ste100Mark-Cli-ArgumentParsing` and
`Ste100Mark-Cli-Format`.

**CliSubsystem_StrictFlow_ContextAndProgram_ParsesStrictFlag**: Arguments `[`"`--strict`"`]` are
parsed through `Context.Create`; `Context.Strict` is true. This scenario verifies
`Ste100Mark-Cli-ArgumentParsing` and `Ste100Mark-Cli-Strict`.

**CliSubsystem_ErrorOutput_ContextAndProgram_WritesErrorToStderr**: A `Context` is created with no
arguments and `WriteError` is called with a known message; standard error receives the message and
`ExitCode` becomes 1. This scenario verifies `Ste100Mark-Cli-OutputChannels`,
`Ste100Mark-Cli-ErrorOutput`, and `Ste100Mark-Cli-ExitCode`.

**CliSubsystem_InvalidArgs_ContextAndProgram_RejectsUnknownArgumentsAndExitsNonZero**: Arguments
`[`"`--unknown-flag`"`]` are passed directly to `Program.Main`; exit code is 1 and standard error
contains an error message including the unknown flag. This scenario verifies
`Ste100Mark-Cli-ErrorOutput`, `Ste100Mark-Cli-InvalidArgs`, and `Ste100Mark-Cli-ExitCode`.

**Run_MissingExplicitConfigFile_ReportsErrorWithoutThrowing**: `Linter.Run` is invoked with a
`Context` created from `[`"`--config`"`,`"`missing.yaml`"`,`"`--silent`"`]`; the call does not
throw, and `ExitCode` becomes 1 for the missing explicit configuration file. This scenario
verifies `Ste100Mark-Cli-Config`.

**Run_WarnOnlyFinding_WithoutStrict_ProducesSuccessExitCode**: `Linter.Run` is invoked with a
warn-only Markdown file and no `--strict` flag; exit code remains 0. This scenario verifies
`Ste100Mark-Cli-Strict`.

**Run_WarnOnlyFinding_WithStrict_ProducesFailureExitCode**: `Linter.Run` is invoked with the same
warn-only Markdown file and `--strict`; exit code becomes 1 without requiring an error-severity
finding. This scenario verifies `Ste100Mark-Cli-Strict` and `Ste100Mark-Cli-ExitCode`.
