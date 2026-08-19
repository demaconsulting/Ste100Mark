### Validation

![SelfTest Structure](SelfTestView.svg)

#### Purpose

`Validation` orchestrates the self-validation test suite. Its single responsibility is to run a
fixed set of internal tests that exercise the tool's own functionality, print a summary to the
provided `Context`, and optionally serialize the results to a file. It does not define the
tool's requirements; it verifies that the tool behaves correctly in the deployment environment.

#### Data Model

`Validation` holds no instance state. The class is `internal static`; all state is local to
`Run` and the private test methods.

#### Key Methods

**Run**: Entry point for the self-validation suite.

- *Parameters*: `Context context` — the output channel and results configuration.
- *Returns*: `void`.
- *Preconditions*: `context` is not null.
- *Postconditions*: A summary has been printed; if `context.ResultsFile` was set, a results
  file has been written or an error has been recorded for an unsupported extension.

Calls `PrintValidationHeader`, constructs a `TestResults` object named
`"Ste100Mark Self-Validation"`, calls `RunVersionTest`, `RunHelpTest`, `RunLintCleanFileTest`,
`RunLintViolationFileTest`, and `RunLintJsonOutputTest`, prints totals (using `WriteError` if
any tests failed), and calls `WriteResultsFile` if `context.ResultsFile` is set.

**RunVersionTest**: Verifies that `--version` produces a version string.

- *Parameters*: `Context context`, `DemaConsulting.TestResults.TestResults testResults`.
- *Returns*: `void`.

Creates a `TemporaryDirectory`, constructs a log path with `PathHelpers.SafePathCombine`,
invokes `Program.Run` with `["--silent", "--log", logFile, "--version"]`, reads the log, and
asserts the content matches a semver-like regex (`\b\d+\.\d+\.\d+`). Records pass or fail.
Any exception is caught by a broad `catch (Exception)` and recorded via `HandleTestException`.

**RunHelpTest**: Verifies that `--help` produces usage text.

- *Parameters*: `Context context`, `DemaConsulting.TestResults.TestResults testResults`.
- *Returns*: `void`.

Creates a `TemporaryDirectory`, constructs a log path with `PathHelpers.SafePathCombine`,
invokes `Program.Run` with `["--silent", "--log", logFile, "--help"]`, reads the log, and
asserts the content contains both `"Usage:"` and `"Options:"`. Records pass or fail. Any
exception is caught by a broad `catch (Exception)` and recorded via `HandleTestException`.

**RunLintCleanFileTest**: Verifies that linting a clean Markdown file produces a zero exit
code.

- *Parameters*: `Context context`, `DemaConsulting.TestResults.TestResults testResults`.
- *Returns*: `void`.

Creates a `TemporaryDirectory`, writes a short compliant Markdown file, temporarily switches
the current directory to the temp directory (so the linter's glob resolution finds the file by
its relative name), invokes `Program.Run` with
`[relativeFile, "--silent", "--log", logFile]` (no `--validate`, so the run reaches
`Linter.Run` instead of `Validation.Run`), and asserts `Context.ExitCode` is `0`. Records pass
or fail. Any exception is caught by a broad `catch (Exception)` and recorded via
`HandleTestException`.

**RunLintViolationFileTest**: Verifies that linting a Markdown file with multiple known
violations reports every expected rule code and returns a non-zero exit code.

- *Parameters*: `Context context`, `DemaConsulting.TestResults.TestResults testResults`.
- *Returns*: `void`.

Writes a Markdown file containing a too-long sentence, a semicolon, a contraction, and a
disallowed dictionary term ("utilize"), invokes the linter the same way as
`RunLintCleanFileTest`, and asserts `Context.ExitCode` is non-zero and that the log content
contains `STE100-4.1`, `STE100-8.1`, `STE100-4.2`, and `STE100-DICT`. Records pass or fail. Any
exception is caught by a broad `catch (Exception)` and recorded via `HandleTestException`.

**RunLintJsonOutputTest**: Verifies that `--format json` produces valid, parseable JSON.

- *Parameters*: `Context context`, `DemaConsulting.TestResults.TestResults testResults`.
- *Returns*: `void`.

Writes a Markdown file, invokes the linter with `--format json` appended to the arguments,
reads the log content, and parses it with `System.Text.Json.JsonDocument.Parse`. A
`JsonException` on a parse failure is caught by the broad `catch (Exception)` and recorded via
`HandleTestException` like any other test failure.

**WriteResultsFile**: Serializes `testResults` to `context.ResultsFile`.

- *Parameters*: `Context context`, `DemaConsulting.TestResults.TestResults testResults`.
- *Returns*: `void`.

Determines the format from the file extension: `.trx` uses `TrxSerializer.Serialize`; `.xml`
uses `JUnitSerializer.Serialize`. Any other extension calls `context.WriteError` with a
descriptive message and returns without writing. File-write exceptions are caught by a broad
`catch (Exception)` and reported via `context.WriteError`.

**CreateTestResult**: Creates a `TestResult` pre-populated with class and code-base metadata.

- *Parameters*: `string testName`.
- *Returns*: `DemaConsulting.TestResults.TestResult` with `ClassName = "Validation"` and
  `CodeBase = "Ste100Mark"`.

**FinalizeTestResult**: Sets elapsed duration and appends the result to the collection.

- *Parameters*: `DemaConsulting.TestResults.TestResult test`, `DateTime startTime`,
  `DemaConsulting.TestResults.TestResults testResults`.
- *Returns*: `void`.

**HandleTestException**: Records a test failure from a caught exception.

- *Parameters*: `DemaConsulting.TestResults.TestResult test`, `Context context`,
  `string testName`, `Exception ex`.
- *Returns*: `void`.

Sets `test.Outcome` to `Failed`, records `ex.Message` as `test.ErrorMessage`, and calls
`context.WriteError` with a failure message.

**TemporaryDirectory** (nested class): Manages a temporary directory for test execution.
Implements `IDisposable`. The constructor calls `Directory.CreateDirectory` and wraps
`IOException`, `UnauthorizedAccessException`, and `ArgumentException` in
`InvalidOperationException`. `Dispose` attempts best-effort deletion of the directory tree;
`IOException` and `UnauthorizedAccessException` during cleanup are silently ignored.

#### Error Handling

`Run` throws `ArgumentNullException` if `context` is null. Each test runner wraps its body in
a broad `catch (Exception)` to ensure test-suite robustness: any unexpected exception is
recorded as a test failure via `HandleTestException` and execution continues with the next test.
`WriteResultsFile` catches file-write exceptions and reports them via `context.WriteError`.

An unsupported `context.ResultsFile` extension is treated as a user error: `WriteError` is
called with a descriptive message
(e.g., `"Error: Unsupported results file format '.json'. Use .trx or .xml extension."`) and
the method returns without writing a file, causing `context.ExitCode` to return 1.

#### Dependencies

- **Context** — output channel for header lines, test result lines, and summary totals.
- **Program** — `Program.Run` is called within each test runner to exercise the tool.
- **PathHelpers** — `SafePathCombine` constructs log file paths inside temporary directories.
- **DemaConsulting.TestResults** — `TestResults`, `TestResult`, and `TestOutcome` types for
  accumulating and representing self-validation results.
- **DemaConsulting.TestResults.IO** — `TrxSerializer` and `JUnitSerializer` for serializing
  results to `.trx` and `.xml` files.

#### Callers

- **Program** — calls `Validation.Run(context)` when the `--validate` flag is set.
