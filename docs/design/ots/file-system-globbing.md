## Microsoft.Extensions.FileSystemGlobbing

This document describes the integration and usage design for the
`Microsoft.Extensions.FileSystemGlobbing` OTS software item.

### Purpose

Microsoft.Extensions.FileSystemGlobbing is chosen as the glob matching library for the
project. It resolves command-line and configuration-file include/exclude patterns and the
first-match-wins mode override patterns used by the `Linting` subsystem.

### Features Used

- `Matcher` for in-memory include/exclude and single-pattern matching
- `AddInclude` for include glob registration
- `AddExclude` for exclude glob registration
- `Match` for per-file override evaluation
- `Execute` together with `DirectoryInfoWrapper` for working-directory file discovery

### Integration Pattern

Microsoft.Extensions.FileSystemGlobbing is consumed as a NuGet package reference in the main
application project. `LintConfig.ResolveMode` constructs a short-lived `Matcher` for each
override glob and checks whether a relative file path matches it. `Linter.ResolveFiles`
constructs a `Matcher` for the effective include/exclude set and executes it against the
current working directory through `DirectoryInfoWrapper`. The library is used synchronously,
with no persistent matcher state shared between invocations and no disposal requirements.
