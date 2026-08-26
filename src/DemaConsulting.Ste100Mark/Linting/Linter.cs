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
    ///     Comparer used for absolute file/directory path equality, deduplication, and ordering
    ///     throughout this class.
    /// </summary>
    /// <remarks>
    ///     Case sensitivity is a per-volume/per-directory file system setting, not something that
    ///     can be inferred from the operating system alone: Windows directories can opt in to
    ///     case sensitivity, and Linux/macOS volumes can be formatted or mounted case-insensitive.
    ///     Rather than guessing from <see cref="OperatingSystem"/>, every root produced by
    ///     <see cref="ResolvePatterns"/> is first normalized to its actual on-disk casing (and
    ///     symlink-resolved) by <see cref="CanonicalizeRoot"/>, and file names returned by
    ///     <see cref="Matcher"/> already reflect the casing <see cref="DirectoryInfoWrapper"/>
    ///     enumerated from disk. Because every path compared here is therefore already in its
    ///     true, on-disk form, ordinary case-sensitive ordinal comparison is correct everywhere,
    ///     regardless of platform or volume case-sensitivity setting.
    /// </remarks>
    private static readonly StringComparer PathComparer = StringComparer.Ordinal;

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
    ///     and a remaining pattern relative to it, so any absolute pattern now matches - whether
    ///     it contains glob metacharacters or is a plain literal file path, which is simply a
    ///     pattern with none - whereas previously it was fed unchanged to a <see cref="Matcher"/>
    ///     rooted at the current directory and silently matched nothing. Both include and exclude
    ///     roots are canonicalized via <see cref="CanonicalizeRoot"/> inside
    ///     <see cref="ResolvePatterns"/> before matching, so the absolute-path equality used here
    ///     to subtract excluded files still works when a root directory is reached through a
    ///     symbolic link (for example, an absolute include or exclude rooted under macOS's
    ///     symlinked <c>/tmp</c>/<c>/var</c>) or spelled with different casing than the current
    ///     directory. Path equality, deduplication, and ordering use <see cref="PathComparer"/>
    ///     (ordinal), which is correct because every path involved has already been normalized to
    ///     its true on-disk casing by <see cref="CanonicalizeRoot"/>.
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
                .Distinct(PathComparer)
                .OrderBy(p => p, PathComparer)
                .ToList();
        }

        var excludedFiles = ResolvePatterns(excludePatterns).ToHashSet(PathComparer);
        return includedFiles
            .Where(f => !excludedFiles.Contains(f))
            .Distinct(PathComparer)
            .OrderBy(p => p, PathComparer)
            .ToList();
    }

    /// <summary>
    ///     Resolves a list of glob patterns (including plain literal file paths, which are simply
    ///     patterns with no wildcard characters) to a set of matched absolute file paths.
    /// </summary>
    /// <remarks>
    ///     Because a <see cref="Matcher"/> only matches patterns relative to a single root
    ///     directory, patterns are first grouped by their effective root - the current directory
    ///     for a non-rooted (relative) pattern, or the fixed directory computed by
    ///     <see cref="ResolvePatternRoot"/> for a rooted (absolute) pattern - and one
    ///     <see cref="Matcher"/> is executed per root group. Every root is passed through
    ///     <see cref="CanonicalizeRoot"/> before grouping so that the same physical directory
    ///     always produces the same root string, whether it was reached via a relative pattern
    ///     (root = <see cref="Directory.GetCurrentDirectory"/>, which the OS may return already
    ///     symlink-resolved) or a rooted pattern (root computed purely by
    ///     <see cref="Path.GetFullPath(string)"/>, which resolves neither symlinks nor casing);
    ///     without this, the exclude-subtraction in <see cref="ResolveFiles"/> could silently
    ///     fail to match an included file and its exclusion by absolute-path equality (for
    ///     example, an absolute include combined with a relative exclude, both naming the same
    ///     file under a symlinked directory such as macOS's <c>/tmp</c>, <c>/var</c>, or
    ///     <c>/etc</c>, or spelled with different casing). Roots are grouped using
    ///     <see cref="PathComparer"/> (ordinal), which is correct here because
    ///     <see cref="CanonicalizeRoot"/> has already normalized every root to its true on-disk
    ///     casing - two roots that differ only in as-supplied case are recognized as the same
    ///     physical directory regardless of whether the underlying volume is itself case
    ///     sensitive. A root that does not exist on disk is skipped rather than throwing, so a
    ///     rooted pattern under a nonexistent directory simply contributes zero matches,
    ///     consistent with how a relative pattern that matches nothing also contributes zero
    ///     matches.
    /// </remarks>
    /// <param name="patterns">Glob patterns (or plain literal file paths) to resolve.</param>
    /// <returns>
    ///     Matched absolute file paths; may contain duplicates if multiple patterns/roots resolve
    ///     to the same file, and is not sorted (callers are responsible for deduplication and
    ///     ordering).
    /// </returns>
    private static List<string> ResolvePatterns(IReadOnlyList<string> patterns)
    {
        var patternsByRoot = new Dictionary<string, List<string>>(PathComparer);
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

            root = CanonicalizeRoot(root);
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
    ///     Maximum number of symlink substitutions <see cref="CanonicalizeRoot"/> follows before
    ///     giving up, guarding against a symlink cycle producing unbounded recursion.
    /// </summary>
    private const int MaxSymlinkResolutionDepth = 32;

    /// <summary>
    ///     Resolves a directory path to its true on-disk form: the actual casing of each path
    ///     component as recorded by the file system, with any symbolic link or junction ancestor
    ///     followed to its target, so that two differently-spelled paths naming the same physical
    ///     directory always canonicalize to an identical string.
    /// </summary>
    /// <remarks>
    ///     Case sensitivity is a per-volume/per-directory file system setting - not something
    ///     that can be inferred from the operating system - so this method does not guess based
    ///     on <see cref="OperatingSystem"/>; it instead normalizes to whatever casing the file
    ///     system actually stored, which is correct regardless of case sensitivity. It also
    ///     resolves symlinks/junctions, so a root computed from a rooted pattern (via
    ///     <see cref="Path.GetFullPath(string)"/>, which follows neither symlinks nor casing)
    ///     compares equal to <see cref="Directory.GetCurrentDirectory"/> when both name the same
    ///     physical directory. Implemented as a component-by-component walk from the path's
    ///     root: for each component, <see cref="FindActualCaseName"/> looks up its true on-disk
    ///     spelling within its parent directory, and <see cref="FileSystemInfo.ResolveLinkTarget"/>
    ///     follows it if it is itself a symbolic link or junction - the same technique POSIX's
    ///     canonical path resolution uses - rather than by temporarily mutating the process-wide
    ///     current directory (for example via <c>SetCurrentDirectory</c>/
    ///     <c>GetCurrentDirectory</c> round-tripping), because <see cref="Linter"/> offers no
    ///     guarantee against concurrent callers and mutating global process state would be a data
    ///     race. Symlink resolution matters on platforms where common directories are themselves
    ///     symlinks - macOS aliases <c>/tmp</c>, <c>/var</c>, and <c>/etc</c> to
    ///     <c>/private/...</c>, and Linux/Windows junctions and symlinks are common in
    ///     containerized or virtualized checkouts. A single left-to-right component walk is not
    ///     sufficient on its own for symlinks: when an ancestor resolves to a symlink target,
    ///     that target string (as recorded by the symlink) may itself contain further unresolved
    ///     symlinked or differently-cased ancestors - for example, resolving a directory symlink
    ///     can land on a path under macOS's <c>/var</c>, which is itself a symlink to
    ///     <c>/private/var</c>. Each time a component resolves to a new target, this method
    ///     recurses on that target so its own ancestors are walked too, up to
    ///     <see cref="MaxSymlinkResolutionDepth"/> substitutions to guard against a symlink
    ///     cycle. A directory (or any ancestor) that does not exist, or that cannot be inspected
    ///     due to access restrictions, is left with its original (unresolved, as-supplied)
    ///     casing from that point rather than throwing, so this method never fails - it only ever
    ///     improves the precision of the returned path where the OS permits.
    /// </remarks>
    /// <param name="path">Absolute directory path to canonicalize.</param>
    /// <param name="depth">Number of symlink substitutions already followed.</param>
    /// <returns>
    ///     The on-disk-cased, symlink-resolved absolute path, or <paramref name="path"/>
    ///     unchanged where resolution was not possible or the depth limit was reached.
    /// </returns>
    private static string CanonicalizeRoot(string path, int depth = 0)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root))
        {
            // Not an absolute path (should not normally happen since callers always pass a
            // Path.GetFullPath or Directory.GetCurrentDirectory result); return unchanged.
            return path;
        }

        var segments = path[root.Length..]
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

        // A drive letter itself has no "true casing" recorded anywhere on disk (unlike a
        // directory entry); normalize it so two paths differing only in drive-letter case
        // still canonicalize identically.
        var current = root.Length == 3 && root[1] == ':' ? char.ToUpperInvariant(root[0]) + root[1..] : root;
        foreach (var segment in segments)
        {
            var actualSegment = FindActualCaseName(current, segment) ?? segment;
            current = Path.Combine(current, actualSegment);

            if (depth >= MaxSymlinkResolutionDepth)
            {
                // Depth limit reached (likely a symlink cycle); stop resolving further and
                // return the best-effort path accumulated so far.
                continue;
            }

            try
            {
                // Only ancestors that are themselves reparse points (symlinks/junctions) resolve
                // to a non-null target; ordinary directories pass through unchanged.
                var target = new DirectoryInfo(current).ResolveLinkTarget(returnFinalTarget: true);
                if (target is not null)
                {
                    // The target's own path may itself have unresolved symlinked or
                    // differently-cased ancestors (for example, landing under macOS's symlinked
                    // /var); recurse to resolve those too rather than continuing the original
                    // component walk on an only-partially-resolved path.
                    current = CanonicalizeRoot(target.FullName, depth + 1);
                }
            }
            catch (IOException)
            {
                // Ancestor does not exist or is otherwise unreadable; keep the unresolved
                // segment and continue, matching the "best effort" contract documented above.
            }
            catch (UnauthorizedAccessException)
            {
                // Ancestor exists but cannot be inspected under current permissions; same
                // best-effort fallback as the IOException case.
            }
        }

        return current;
    }

    /// <summary>
    ///     Looks up the true on-disk spelling of a directory entry, by name, within its parent
    ///     directory.
    /// </summary>
    /// <remarks>
    ///     Case sensitivity varies per volume and per directory rather than per operating system,
    ///     so the only reliable way to determine whether <paramref name="name"/> and an existing
    ///     entry are "the same" file is to ask the file system itself: this enumerates
    ///     <paramref name="parentDirectory"/> and prefers an exact (ordinal) match, since a
    ///     case-sensitive file system may legitimately contain sibling entries that differ only
    ///     by case. Only when no exact match exists does it fall back to the first
    ///     case-insensitive match (which is always at least as permissive as the file system's
    ///     actual case sensitivity). Returns <see langword="null"/> rather than throwing when
    ///     <paramref name="parentDirectory"/> does not exist or cannot be enumerated, or when no
    ///     entry matches at all, so callers can fall back to the as-supplied name.
    /// </remarks>
    /// <param name="parentDirectory">Directory to search within.</param>
    /// <param name="name">Entry name to look up, in whatever casing was supplied.</param>
    /// <returns>The entry's true on-disk name, or <see langword="null"/> if not found.</returns>
    private static string? FindActualCaseName(string parentDirectory, string name)
    {
        try
        {
            // Prefer an exact (ordinal) match first: on a case-sensitive file system, two
            // sibling entries may legitimately differ only by case (for example "Foo" and
            // "foo"), and an exact match unambiguously identifies the correct one. Only fall
            // back to a case-insensitive match - used to recover the true on-disk casing of an
            // as-supplied path segment - when no exact match exists.
            string? caseInsensitiveMatch = null;
            foreach (var entry in Directory.EnumerateFileSystemEntries(parentDirectory))
            {
                var entryName = Path.GetFileName(entry);
                if (string.Equals(entryName, name, StringComparison.Ordinal))
                {
                    return entryName;
                }

                if (caseInsensitiveMatch is null &&
                    string.Equals(entryName, name, StringComparison.OrdinalIgnoreCase))
                {
                    caseInsensitiveMatch = entryName;
                }
            }

            return caseInsensitiveMatch;
        }
        catch (IOException)
        {
            // Parent directory does not exist or is otherwise unreadable.
        }
        catch (UnauthorizedAccessException)
        {
            // Parent directory exists but cannot be enumerated under current permissions.
        }

        return null;
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
    /// <param name="pattern">Rooted glob pattern (or plain literal file path).</param>
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
