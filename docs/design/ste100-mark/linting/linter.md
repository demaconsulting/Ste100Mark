### Linter

![Linting Structure](LintingView.svg)

#### Purpose

`Linter` is the subsystem orchestrator and sole public entry point. Its single responsibility
is to run the end-to-end lint workflow for one invocation of the tool.

#### Data Model

**DefaultIncludePattern**: `const string` - `**/*.md`, used when neither positional globs nor
configured include patterns are available.

`Linter` holds no mutable instance state; all run-specific data lives in local variables
inside `RunCore`.

#### Key Methods

**Run**: Public wrapper for one lint pass.

- *Parameters*: `Context context` - parsed command-line context.
- *Returns*: `void`.
- *Preconditions*: `context` is not null.
- *Postconditions*: Either a full lint report has been written or an expected configuration/
  dictionary error has been reported through `context.WriteError`.

`Run` delegates to `RunCore` and catches `InvalidOperationException` so expected
user-fixable configuration problems become normal lint errors rather than top-level crashes.

**RunCore**: Private implementation of the lint pass.

- *Parameters*: `Context context` - parsed command-line context.
- *Returns*: `void`.
- *Preconditions*: `context` is not null.
- *Postconditions*: Files are resolved, diagnostics are reported, and the exit code is driven
  according to error and strict-mode semantics.

`RunCore` resolves the configuration path, loads `LintConfig` and `LintDictionary`, resolves
files, reads file contents, extracts prose, resolves each file's mode/rules/allowed-terms/
allowed-phrases via `LintConfig.ResolveMode`/`ResolveRules`/`ResolveAllowedTerms`/
`ResolveAllowedPhrases` (applying any matching `Profile` deltas), evaluates structural and
dictionary rules against that per-file configuration, and reports the aggregated diagnostics.

**ResolveConfigPath**: Chooses the configuration file path.

- *Parameters*: `string? configFileArgument` - explicit `--config` value, or `null`.
- *Returns*: `string?` - explicit path unchanged, `.ste100mark.yaml` when present in the
  current directory, or `null` to signal built-in defaults.

**ResolveFiles**: Computes the Markdown file set for the run.

- *Parameters*: `IReadOnlyList<string> globs` - positional globs; `LintConfig config` -
  effective configuration.
- *Returns*: `List<string>` - absolute file paths sorted in ordinal order.
- *Preconditions*: `config` is not null.
- *Postconditions*: Positional globs fully replace configured include/exclude patterns for the
  invocation.

Include and exclude patterns are each resolved independently to absolute file paths via
`ResolvePatterns`, and excludes are subtracted from includes by absolute path equality. This
supports include/exclude patterns rooted differently from one another (for example, an
absolute include with a relative exclude), which a single shared `Matcher` cannot do.

**ResolvePatterns**: Resolves a list of glob patterns (including plain literal file paths, which
are simply patterns with no wildcard characters) to matched absolute file paths.

- *Parameters*: `IReadOnlyList<string> patterns` - glob patterns (or plain literal file paths).
- *Returns*: `List<string>` - matched absolute file paths (unsorted, may contain duplicates).

Because a `Matcher` only matches patterns relative to one root directory, patterns are grouped
by their effective root - the current directory for a relative pattern, or the fixed directory
computed by `ResolvePatternRoot` for a rooted (absolute) pattern - and one `Matcher` runs per
root group. A root that does not exist on disk is skipped, contributing zero matches rather
than throwing.

**ResolvePatternRoot**: Splits a rooted pattern into a fixed root directory and the remaining
pattern relative to it, at the first glob metacharacter (`*`, `?`, `[`).

- *Parameters*: `string pattern` - rooted glob pattern (or plain literal file path).
- *Returns*: `(string Root, string Pattern)` - the fixed absolute root directory and the
  remaining pattern relative to it.

A literal absolute file path with no metacharacter reduces to its parent directory and file
name. Both `\` and `/` separators are accepted. This enables any absolute pattern (Windows drive
letters, UNC paths, or POSIX-style leading `/`) to match, whether or not it contains wildcard
characters, whereas previously it was fed unchanged to a `Matcher` rooted at the current
directory and silently matched nothing.

#### Error Handling

`Run` propagates `ArgumentNullException` for a null context. It catches
`InvalidOperationException` from configuration or dictionary loading and reports the message
through `Context.WriteError`. Other exceptions are not caught and propagate to `Program.Main`
as unexpected failures.

#### Dependencies

- **Context** and **OutputFormat** - provide CLI inputs and output channels.
- **LintConfig** - loads configuration and resolves per-file mode, rules, and allowed terms.
- **LintDictionary** - builds the effective dictionary.
- **MarkdownProseExtractor** - extracts prose segments from each Markdown file.
- **StructuralRules** and **DictionaryChecker** - produce diagnostics.
- **DiagnosticReporter** - formats and writes the final report.
- **Microsoft.Extensions.FileSystemGlobbing** - resolves file-selection patterns.
- **.NET BCL** - file-system and file-content access.

#### Callers

- **Program** - dispatches the default tool path to `Linter.Run`.
