// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using DemaConsulting.Ste100Mark.Cli;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace DemaConsulting.Ste100Mark.Linting;

/// <summary>
///     Orchestrates one end-to-end lint run: resolves configuration and the effective file set,
///     runs the structural and dictionary checks over each file, aggregates diagnostics, applies
///     <c>--strict</c> severity promotion for exit-code purposes, reports the results, and drives
///     <see cref="Context.ExitCode"/>.
/// </summary>
/// <remarks>
///     This is the single entry point <see cref="Program.RunToolLogic"/> dispatches to; it is the
///     only unit in the <c>Linting</c> subsystem that other subsystems call directly.
/// </remarks>
internal static class Linter
{
    /// <summary>
    ///     Default include pattern used when neither positional glob arguments nor a configured
    ///     <c>include</c> list are available.
    /// </summary>
    private const string DefaultIncludePattern = "**/*.md";

    /// <summary>
    ///     Glob metacharacters recognized by <see cref="Matcher"/>; used to locate the first
    ///     wildcard segment of a rooted pattern so its fixed directory prefix can be split off as
    ///     the matcher root.
    /// </summary>
    private static readonly char[] GlobMetaCharacters = ['*', '?', '['];

    /// <summary>
    ///     Runs a full lint pass using the globs, configuration path, output format, and strict flag
    ///     carried on <paramref name="context"/>.
    /// </summary>
    /// <param name="context">Parsed command-line context.</param>
    /// <remarks>
    ///     Configuration and dictionary loading failures (missing/invalid <c>--config</c> file,
    ///     missing/invalid dictionary file) are reported via <see cref="Context.WriteError"/> and
    ///     cause a non-zero exit code; they do not throw out of this method, so that
    ///     <see cref="Program.Main"/>'s top-level exception handling is reserved for genuinely
    ///     unexpected failures.
    /// </remarks>
    public static void Run(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            RunCore(context);
        }
        catch (InvalidOperationException ex)
        {
            // Configuration or dictionary loading failures are expected, user-correctable
            // problems; report them the same way as any other lint failure instead of letting
            // them propagate to the top-level unexpected-exception handling.
            context.WriteError(ex.Message);
        }
    }

    /// <summary>
    ///     Performs the actual lint pass; separated from <see cref="Run"/> so that configuration and
    ///     dictionary loading failures can be caught in one place.
    /// </summary>
    /// <param name="context">Parsed command-line context.</param>
    private static void RunCore(Context context)
    {
        var configPath = ResolveConfigPath(context.ConfigFile);
        var config = LintConfig.Load(configPath);
        var configDirectory = configPath is null
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? Directory.GetCurrentDirectory();

        // The dictionary check as a whole is opt-out (dictionary.enabled: false), for projects that
        // want only the structural/mechanical STE100 checks enforced, without ASD-STE100 vocabulary
        // restrictions. The dictionary itself is only loaded when the check is enabled, so a project
        // that disables it is not required to supply (or license) any dictionary file.
        var dictionaryEnabled = config.Dictionary?.Enabled ?? true;
        var dictionary = dictionaryEnabled ? LintDictionary.Load(config, configDirectory) : null;
        var files = ResolveFiles(context.Globs, config);

        var diagnostics = new List<Diagnostic>();
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), file).Replace('\\', '/');
            var content = File.ReadAllText(file);
            var mode = config.ResolveMode(relativePath);
            var rules = config.ResolveRules(relativePath);
            var segments = MarkdownProseExtractor.Extract(content);

            diagnostics.AddRange(StructuralRules.Evaluate(relativePath, segments, mode, rules));

            if (dictionary is not null)
            {
                var allowedTerms = config.ResolveAllowedTerms(relativePath);
                var allowedPhrases = config.ResolveAllowedPhrases(relativePath);
                diagnostics.AddRange(DictionaryChecker.Evaluate(relativePath, segments, dictionary, mode, allowedTerms, allowedPhrases));
            }
        }

        DiagnosticReporter.Report(context, diagnostics, files.Count);

        // Exit-code semantics: any error-severity finding always fails the build; under --strict,
        // warn-severity findings are promoted to fail the build too, but their reported severity
        // in the text/JSON output above is left untouched.
        var hasFailure = diagnostics.Any(d =>
            d.Severity == Severity.Error || (context.Strict && d.Severity == Severity.Warn));

        if (!hasFailure)
        {
            return;
        }

        if (context.Format == OutputFormat.Json)
        {
            // Avoid emitting any additional text so the JSON document remains the only content on
            // the combined output stream (see DiagnosticReporter remarks).
            context.MarkFailure();
        }
        else
        {
            var errorCount = diagnostics.Count(d => d.Severity == Severity.Error);
            var warnCount = diagnostics.Count(d => d.Severity == Severity.Warn);
            var reason = context.Strict && errorCount == 0
                ? $"{warnCount} warning(s) promoted to errors by --strict"
                : $"{errorCount} error(s)";
            context.WriteError($"Ste100Mark found linting issues ({reason}).");
        }
    }

    /// <summary>
    ///     Resolves the configuration file path to load: an explicit <c>--config</c> value is
    ///     passed through as-is (so a missing explicit path is reported as an error by
    ///     <see cref="LintConfig.Load"/>); otherwise, <c>.ste100mark.yaml</c> in the current
    ///     directory is used only if it exists, and <see langword="null"/> is returned to mean
    ///     "use all defaults".
    /// </summary>
    /// <param name="configFileArgument">Value of <c>--config</c>, or <see langword="null"/>.</param>
    /// <returns>Resolved configuration file path, or <see langword="null"/>.</returns>
    private static string? ResolveConfigPath(string? configFileArgument)
    {
        if (configFileArgument is not null)
        {
            return configFileArgument;
        }

        var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), ".ste100mark.yaml");
        return File.Exists(defaultPath) ? defaultPath : null;
    }

    /// <summary>
    ///     Resolves the effective set of Markdown files to lint.
    /// </summary>
    /// <remarks>
    ///     When positional <paramref name="globs"/> are supplied, they entirely replace the
    ///     configuration's <c>include</c>/<c>exclude</c> patterns (mode, rules, and dictionary
    ///     configuration still apply). Otherwise, the configuration's <c>include</c> patterns are
    ///     used (defaulting to <c>**/*.md</c> when empty) together with its <c>exclude</c> patterns.
    ///     Include and exclude patterns are each resolved independently to a set of absolute file
    ///     paths via <see cref="ResolvePatterns"/>, and excluded paths are then subtracted from the
    ///     included paths by absolute path equality; this supports include and exclude patterns
    ///     that use different roots (for example, an absolute include combined with a relative
    ///     exclude), which a single shared <see cref="Matcher"/> instance cannot do because a
    ///     <see cref="Matcher"/> only ever matches patterns relative to one root directory. Every
    ///     pattern - relative or rooted (a Windows drive letter, a UNC path, or a POSIX-style
    ///     leading <c>/</c>) - is supported: rooted patterns are split into a fixed root directory
    ///     and a remaining pattern relative to it, so absolute globs and absolute literal file
    ///     paths now match, whereas previously they were fed unchanged to a <see cref="Matcher"/>
    ///     rooted at the current directory and silently matched nothing.
    /// </remarks>
    /// <param name="globs">Positional glob arguments from the command line.</param>
    /// <param name="config">Resolved lint configuration.</param>
    /// <returns>Matched absolute file paths, sorted for deterministic output.</returns>
    private static List<string> ResolveFiles(IReadOnlyList<string> globs, LintConfig config)
    {
        List<string> includePatterns;
        List<string> excludePatterns;

        if (globs.Count > 0)
        {
            includePatterns = [.. globs];
            excludePatterns = [];
        }
        else
        {
            includePatterns = config.Include.Count > 0 ? [.. config.Include] : [DefaultIncludePattern];
            excludePatterns = [.. config.Exclude];
        }

        var includedFiles = ResolvePatterns(includePatterns);
        if (excludePatterns.Count == 0)
        {
            return includedFiles
                .Distinct(StringComparer.Ordinal)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
        }

        var excludedFiles = ResolvePatterns(excludePatterns).ToHashSet(StringComparer.Ordinal);
        return includedFiles
            .Where(f => !excludedFiles.Contains(f))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    ///     Resolves a list of glob/literal-path patterns to a set of matched absolute file paths.
    /// </summary>
    /// <remarks>
    ///     Because a <see cref="Matcher"/> only matches patterns relative to a single root
    ///     directory, patterns are first grouped by their effective root - the current directory
    ///     for a non-rooted (relative) pattern, or the fixed directory computed by
    ///     <see cref="ResolvePatternRoot"/> for a rooted (absolute) pattern - and one
    ///     <see cref="Matcher"/> is executed per root group. A root that does not exist on disk is
    ///     skipped rather than throwing, so a rooted pattern under a nonexistent directory simply
    ///     contributes zero matches, consistent with how a relative pattern that matches nothing
    ///     also contributes zero matches.
    /// </remarks>
    /// <param name="patterns">Glob or literal file-path patterns to resolve.</param>
    /// <returns>
    ///     Matched absolute file paths; may contain duplicates if multiple patterns/roots resolve
    ///     to the same file, and is not sorted (callers are responsible for deduplication and
    ///     ordering).
    /// </returns>
    private static List<string> ResolvePatterns(IReadOnlyList<string> patterns)
    {
        var patternsByRoot = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var pattern in patterns)
        {
            string root;
            string remainingPattern;
            if (Path.IsPathRooted(pattern))
            {
                (root, remainingPattern) = ResolvePatternRoot(pattern);
            }
            else
            {
                root = Directory.GetCurrentDirectory();
                remainingPattern = pattern;
            }

            if (!patternsByRoot.TryGetValue(root, out var rootPatterns))
            {
                rootPatterns = [];
                patternsByRoot[root] = rootPatterns;
            }

            rootPatterns.Add(remainingPattern);
        }

        var files = new List<string>();
        foreach (var (root, rootPatterns) in patternsByRoot)
        {
            // A rooted pattern's directory prefix may not exist (a typo'd absolute path, or a
            // configuration written for a different machine); skip it silently so it contributes
            // zero matches instead of throwing, matching the existing "no files matched" contract.
            if (!Directory.Exists(root))
            {
                continue;
            }

            var matcher = new Matcher();
            foreach (var rootPattern in rootPatterns)
            {
                matcher.AddInclude(rootPattern);
            }

            var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(root)));
            files.AddRange(result.Files.Select(f => Path.GetFullPath(Path.Combine(root, f.Path))));
        }

        return files;
    }

    /// <summary>
    ///     Splits a rooted (absolute) pattern into a fixed root directory and the remaining
    ///     pattern relative to that root, at the first glob metacharacter (<c>*</c>, <c>?</c>, or
    ///     <c>[</c>).
    /// </summary>
    /// <remarks>
    ///     A literal absolute file path with no glob metacharacter (for example,
    ///     <c>C:\docs\readme.md</c>) reduces to its parent directory and file name. Both
    ///     <c>\</c> and <c>/</c> path separators are accepted, since Windows accepts either; the
    ///     returned root always uses <see cref="Path.GetFullPath(string)" /> normalization. A
    ///     drive-relative pattern with no directory separator before its first wildcard (for
    ///     example, <c>C:*.md</c>) is a rare corner case of raw <see cref="Path" /> APIs that falls
    ///     back to the pattern's path root; this is a known, documented limitation rather than a
    ///     regression, since it is not a form of pattern this method is required to fully support.
    /// </remarks>
    /// <param name="pattern">Rooted glob or literal file-path pattern.</param>
    /// <returns>The fixed absolute root directory and the remaining pattern relative to it.</returns>
    private static (string Root, string Pattern) ResolvePatternRoot(string pattern)
    {
        var normalized = pattern.Replace('\\', '/');
        var metaIndex = normalized.IndexOfAny(GlobMetaCharacters);
        if (metaIndex < 0)
        {
            // Literal absolute file path: reduce to (parent directory, file name).
            var directory = Path.GetDirectoryName(pattern);
            var fileName = Path.GetFileName(pattern);
            return (Path.GetFullPath(string.IsNullOrEmpty(directory) ? "." : directory), fileName);
        }

        var separatorIndex = normalized.LastIndexOf('/', Math.Max(metaIndex - 1, 0));
        if (separatorIndex < 0)
        {
            // Drive-relative pattern with no directory separator before the first wildcard; fall
            // back to the pattern's path root (see remarks).
            var pathRoot = Path.GetPathRoot(pattern) ?? Directory.GetCurrentDirectory();
            var normalizedRoot = pathRoot.Replace('\\', '/');
            return (Path.GetFullPath(pathRoot), normalized[normalizedRoot.Length..]);
        }

        var rootDirectory = normalized[..(separatorIndex + 1)];
        var remainingPattern = normalized[(separatorIndex + 1)..];
        return (Path.GetFullPath(rootDirectory), remainingPattern);
    }
}
