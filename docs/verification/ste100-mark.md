# Ste100Mark

## Verification Approach

System-level verification uses end-to-end integration tests that invoke the tool as a real
process via the `Runner.Run` helper in `IntegrationTests.cs`. Each test exercises the full
stack — argument parsing, dispatch, execution, and output — and validates both exit code and
combined console output. The tests treat the tool as a black box and assert only on observable
outputs; no internal implementation details are assumed.

`Runner.Run` merges stdout and stderr into a single combined output string. Per-stream assertions
(e.g., "standard error is empty") are not possible at this level; all assertions are made
against the combined output.

## Test Environment

Integration tests run on .NET 8.0, .NET 9.0, and .NET 10.0 across Windows, Linux, and macOS.
All scenarios are expected to produce identical results on all supported runtime and platform
combinations. Temporary files and directories are created and cleaned up within each test.

## Acceptance Criteria

- All integration tests pass with zero failures across all supported runtimes and platforms.
- Help and version invocations return exit code 0 and include the documented observable markers
  asserted by the tests: `Ste100Mark_VersionFlag_Provided_OutputsVersion` checks for a version
  string without banner text, and `Ste100Mark_HelpFlag_Provided_OutputsUsageInformation` checks
  for `Usage:` and `Options:` content.
- Exit code 0 is returned for all valid invocations.
- Exit code non-zero is returned for all invalid argument combinations and explicit failure paths,
  including `Ste100Mark_UnknownArgument_Provided_ReturnsError`,
  `Ste100Mark_ValidateWithBadExtension_ExtensionInvalid_ReturnsNonZero`, and
  `Ste100Mark_LintWithMissingConfigFile_ReturnsNonZeroWithErrorMessage`.
- Results files are created at the specified paths when `--results` is used with `--validate`,
  and the legacy singular alias `--result` behaves equivalently as covered by
  `Ste100Mark_ResultAlias_LegacyFlag_WritesResultsFile`.
- Result-file content assertions are limited to the markers exercised by the tests: TRX output is
  checked for `<TestRun` and `</TestRun>` by
  `Ste100Mark_ValidateWithTrxResults_Requested_GeneratesTrxFile`, and JUnit-style XML output is
  checked for a `<testsuites` marker by
  `Ste100Mark_ValidateWithXmlResults_Requested_GeneratesJUnitFile`; schema validation is out of
  scope for these tests. Unsupported result-file extensions are verified separately by
  `Ste100Mark_ValidateWithBadExtension_ExtensionInvalid_ReturnsNonZero`.
- Silent mode (`--silent`) produces empty combined output.

## Test Scenarios

**Ste100Mark_VersionFlag_Provided_OutputsVersion**: The `--version` flag is passed as
the sole argument; the tool outputs a version string matching the integration test pattern and
exits with code 0, without the banner text that appears on other execution paths. This scenario
is tested by `Ste100Mark_VersionFlag_Provided_OutputsVersion`.

**Ste100Mark_HelpFlag_Provided_OutputsUsageInformation**: The `--help` flag is passed
as the sole argument; the combined output contains the `Usage:` and `Options:` headings together
with key option names such as `--version` and `--help`, and the tool exits with code 0. This
scenario is tested by `Ste100Mark_HelpFlag_Provided_OutputsUsageInformation`.

**Ste100Mark_ValidateFlag_Provided_RunsValidation**: The `--validate` flag is passed as
the sole argument; the combined output contains "Total Tests:" and the tool exits with code 0,
confirming the self-validation suite runs and completes successfully. This scenario is tested by
`Ste100Mark_ValidateFlag_Provided_RunsValidation`.

**Ste100Mark_ValidateWithTrxResults_Requested_GeneratesTrxFile**: The `--validate` flag
is combined with `--results <path>.trx`; a results file is created at the specified path, and the
assertion is limited to presence of the TRX root markers `<TestRun` and `</TestRun>` rather than
full TRX schema validation. This scenario is tested by
`Ste100Mark_ValidateWithTrxResults_Requested_GeneratesTrxFile`.

**Ste100Mark_ValidateWithXmlResults_Requested_GeneratesJUnitFile**: The `--validate` flag
is combined with `--results <path>.xml`; a results file is created at the specified path, and the
assertion is limited to presence of a `<testsuites` marker rather than full JUnit schema
validation. This scenario is tested by
`Ste100Mark_ValidateWithXmlResults_Requested_GeneratesJUnitFile`.

**Ste100Mark_SilentFlag_Provided_SuppressesOutput**: The `--version` and `--silent`
flags are passed together; the combined output is empty or whitespace-only while the tool exits
with code 0, confirming silent mode suppresses all console output. This scenario is tested by
`Ste100Mark_SilentFlag_Provided_SuppressesOutput`.

**Ste100Mark_LogFlag_Provided_WritesOutputToFile**: The `--log <path>` flag is passed
pointing to a temporary file; the tool exits with code 0 and the log file is created containing
output that also appears in the combined console output. This scenario is tested by
`Ste100Mark_LogFlag_Provided_WritesOutputToFile`.

**Ste100Mark_UnknownArgument_Provided_ReturnsError**: An unrecognized argument
(`--unknown`) is passed; the tool exits with a non-zero code and the combined output contains
an error message identifying the unknown argument. This scenario is tested by
`Ste100Mark_UnknownArgument_Provided_ReturnsError`.

**Ste100Mark_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel**: The `--validate`
flag is combined with `--depth 3`; the combined output contains `###` (heading at depth 3) and
the tool exits with code 0. This scenario is tested by
`Ste100Mark_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel`.

**Ste100Mark_NoArguments_Invoked_DisplaysBanner**: The tool is invoked with no
arguments; the combined output contains the tool name and copyright notice and the exit code is
0. This scenario is tested by `Ste100Mark_NoArguments_Invoked_DisplaysBanner`.

**Ste100Mark_ResultAlias_LegacyFlag_WritesResultsFile**: The `--validate` flag is
combined with `--result <path>.trx`, confirming that the legacy singular alias is still accepted
as an exact behavioral alias for `--results`; a results file is created at the specified path and
the tool exits with code 0. This scenario is tested by
`Ste100Mark_ResultAlias_LegacyFlag_WritesResultsFile`.

**Ste100Mark_ValidateWithBadExtension_ExtensionInvalid_ReturnsNonZero**: The `--validate`
flag is combined with `--results <path>.bad` to exercise unsupported-extension handling
specifically; the tool exits with a non-zero code and no file is created at the specified path.
This scenario is tested by `Ste100Mark_ValidateWithBadExtension_ExtensionInvalid_ReturnsNonZero`.

**Ste100Mark_LintWithMissingConfigFile_ReturnsNonZeroWithErrorMessage**: Lint mode is invoked
with `--config missing.yaml` in a directory where that file does not exist; the tool exits with a
non-zero code and the combined output includes the missing file name, demonstrating the explicit
missing-configuration failure path. This scenario is tested by
`Ste100Mark_LintWithMissingConfigFile_ReturnsNonZeroWithErrorMessage`.
