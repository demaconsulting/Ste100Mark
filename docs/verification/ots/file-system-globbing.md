## Microsoft.Extensions.FileSystemGlobbing Verification

This document provides the verification evidence for the Microsoft.Extensions.FileSystemGlobbing
OTS software item. Requirements for this OTS item are defined in the
Microsoft.Extensions.FileSystemGlobbing OTS Software Requirements document.

### Required Functionality

Microsoft.Extensions.FileSystemGlobbing is the glob matching library used by the Linting
subsystem. It resolves command-line and configuration-file include/exclude patterns and the
first-match-wins mode override patterns via `Matcher`, `AddInclude`, `AddExclude`, `Match`, and
`Execute`/`DirectoryInfoWrapper`. Correct pattern matching, exclusion, and first-match ordering
confirm the library is functioning correctly for the project's usage.

### Verification Approach

Microsoft.Extensions.FileSystemGlobbing has no dedicated self-validation CLI of its own, so it is
verified by transitive evidence from the Linting subsystem's own passing tests. Each scenario
names a specific test method that exercises `LintConfig.ResolveMode` or `Linter.ResolveFiles`,
both of which depend directly on Microsoft.Extensions.FileSystemGlobbing to match file paths
against glob patterns. A passing test run for all scenarios constitutes evidence that the library
correctly matches, excludes, and orders pattern results as the subsystem relies on.

### Test Scenarios

#### ResolveMode_NoMatchingProfile_ReturnsDefaultMode

**Scenario**: A file path is checked against a mode-override glob that does not match it.

**Expected**: Microsoft.Extensions.FileSystemGlobbing reports no match, and the default mode is
returned.

**Requirement coverage**: `Ste100Mark-OTS-FileSystemGlobbing`.

#### ResolveMode_MatchingProfileGlob_ReturnsOverriddenMode

**Scenario**: A file path is checked against a mode-override glob that matches it.

**Expected**: Microsoft.Extensions.FileSystemGlobbing reports a match, and the overridden mode is
returned.

**Requirement coverage**: `Ste100Mark-OTS-FileSystemGlobbing`.

#### ResolveMode_MultipleProfiles_UsesFirstMatch

**Scenario**: A file path matches multiple configured mode-override globs.

**Expected**: Microsoft.Extensions.FileSystemGlobbing evaluates the overrides in order, and the
first matching override's mode is used.

**Requirement coverage**: `Ste100Mark-OTS-FileSystemGlobbing`.

#### Run_PositionalGlobs_OverrideConfigInclude

**Scenario**: Positional command-line globs are supplied instead of the configuration file's
include patterns.

**Expected**: Microsoft.Extensions.FileSystemGlobbing resolves files using the positional globs,
overriding the configuration file's include patterns.

**Requirement coverage**: `Ste100Mark-OTS-FileSystemGlobbing`.

#### Run_ProcedureModeOverride_AppliesStricterWordLimit

**Scenario**: A file path matches a procedure-mode override glob.

**Expected**: Microsoft.Extensions.FileSystemGlobbing reports the match, and the stricter
procedure-mode word limit is applied to that file.

**Requirement coverage**: `Ste100Mark-OTS-FileSystemGlobbing`.

#### Run_CleanMarkdownFile_ProducesSuccessExitCode

**Scenario**: A clean Markdown file is resolved from the working directory using the effective
include/exclude glob set.

**Expected**: Microsoft.Extensions.FileSystemGlobbing resolves the file for linting, and the
lint run completes with a success exit code.

**Requirement coverage**: `Ste100Mark-OTS-FileSystemGlobbing`.
