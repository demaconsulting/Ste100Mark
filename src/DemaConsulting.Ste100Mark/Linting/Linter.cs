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

        var dictionary = LintDictionary.Load(config, configDirectory);
        var files = ResolveFiles(context.Globs, config);

        var diagnostics = new List<Diagnostic>();
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), file).Replace('\\', '/');
            var content = File.ReadAllText(file);
            var mode = config.ResolveMode(relativePath);
            var segments = MarkdownProseExtractor.Extract(content);

            diagnostics.AddRange(StructuralRules.Evaluate(relativePath, segments, mode, config.Rules));
            diagnostics.AddRange(DictionaryChecker.Evaluate(relativePath, segments, dictionary, mode));
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
    /// </remarks>
    /// <param name="globs">Positional glob arguments from the command line.</param>
    /// <param name="config">Resolved lint configuration.</param>
    /// <returns>Matched absolute file paths, sorted for deterministic output.</returns>
    private static List<string> ResolveFiles(IReadOnlyList<string> globs, LintConfig config)
    {
        var matcher = new Matcher();

        if (globs.Count > 0)
        {
            foreach (var glob in globs)
            {
                matcher.AddInclude(glob);
            }
        }
        else
        {
            var includePatterns = config.Include.Count > 0 ? config.Include : [DefaultIncludePattern];
            foreach (var pattern in includePatterns)
            {
                matcher.AddInclude(pattern);
            }

            foreach (var pattern in config.Exclude)
            {
                matcher.AddExclude(pattern);
            }
        }

        var root = Directory.GetCurrentDirectory();
        var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(root)));

        return result.Files
            .Select(f => Path.GetFullPath(Path.Combine(root, f.Path)))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }
}
