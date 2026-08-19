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

using Microsoft.Extensions.FileSystemGlobbing;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DemaConsulting.Ste100Mark.Linting;

/// <summary>
///     Identifies the writing style a Markdown file (or a glob-matched group of files) is checked
///     against, since ASD-STE100 applies different sentence word-count limits to procedural and
///     descriptive writing.
/// </summary>
internal enum LintMode
{
    /// <summary>
    ///     Step-by-step instructional writing (Rule 4.1: 20-word sentence limit).
    /// </summary>
    Procedure,

    /// <summary>
    ///     Narrative/explanatory writing (Rule 4.1: 25-word sentence limit).
    /// </summary>
    Descriptive
}

/// <summary>
///     Mechanical and advisory rule tuning knobs bound from the <c>rules:</c> section of
///     <c>.ste100mark.yaml</c>.
/// </summary>
/// <remarks>
///     Defaults mirror the values requested for the feature: 20/25-word official sentence limits,
///     no semicolons, no contractions, a 6-sentence advisory paragraph cap, and a <c>warn</c>-level
///     passive-voice heuristic. Mechanical rule severities (word limit, semicolons, contractions)
///     are intentionally not configurable to <see cref="Severity.Off"/> or <see cref="Severity.Warn"/>
///     here because they are official STE100 rules, not advisory heuristics; only the advisory
///     checks (<see cref="PassiveVoice"/>, <see cref="ComplexVerb"/>, <see cref="IngForm"/>, and
///     the boolean gate on paragraph length) are tunable.
/// </remarks>
internal sealed class RulesConfig
{
    /// <summary>
    ///     Maximum words per sentence in <see cref="LintMode.Procedure"/> mode (Rule 4.1). Default 20.
    /// </summary>
    public int MaxWordsProcedure { get; set; } = 20;

    /// <summary>
    ///     Maximum words per sentence in <see cref="LintMode.Descriptive"/> mode (Rule 4.1). Default 25.
    /// </summary>
    public int MaxWordsDescriptive { get; set; } = 25;

    /// <summary>
    ///     When <see langword="true"/>, disables the official semicolon ban (Rule 8.1). Default
    ///     <see langword="false"/>.
    /// </summary>
    public bool AllowSemicolons { get; set; }

    /// <summary>
    ///     When <see langword="true"/>, disables the official contraction ban (Rule 4.2). Default
    ///     <see langword="false"/>.
    /// </summary>
    public bool AllowContractions { get; set; }

    /// <summary>
    ///     Advisory maximum number of sentences per paragraph. A value of 0 disables the check.
    ///     Default 6. This is not an official STE100 rule.
    /// </summary>
    public int MaxSentencesParagraph { get; set; } = 6;

    /// <summary>
    ///     Severity of the advisory passive-voice heuristic. Default <see cref="Severity.Warn"/>.
    ///     This is not an official STE100 rule; it is a best-effort regex heuristic that may
    ///     produce false positives and negatives.
    /// </summary>
    public Severity PassiveVoice { get; set; } = Severity.Warn;

    /// <summary>
    ///     Severity of the advisory complex-verb heuristic (perfect and modal-perfect tense
    ///     detection), bound to <c>rules.complex-verb</c>. Default <see cref="Severity.Warn"/>.
    ///     This is not an official STE100 rule; it is a best-effort regex heuristic that may
    ///     produce false positives and negatives.
    /// </summary>
    public Severity ComplexVerb { get; set; } = Severity.Warn;

    /// <summary>
    ///     Severity of the advisory <c>-ing</c> form heuristic, bound to <c>rules.ing-form</c>.
    ///     Default <see cref="Severity.Warn"/>. This is not an official STE100 rule; it is a
    ///     best-effort regex heuristic that may produce false positives and negatives.
    /// </summary>
    public Severity IngForm { get; set; } = Severity.Warn;
}

/// <summary>
///     Maps a glob pattern to the <see cref="LintMode"/> that files matching it should be checked
///     against, overriding <see cref="LintConfig.DefaultMode"/>.
/// </summary>
internal sealed class ModeOverride
{
    /// <summary>
    ///     Glob pattern (relative to the configuration file's directory) identifying the files this
    ///     override applies to, for example <c>docs/user_guide/procedures/**/*.md</c>.
    /// </summary>
    public string Glob { get; set; } = string.Empty;

    /// <summary>
    ///     The mode to apply to files matching <see cref="Glob"/>.
    /// </summary>
    public LintMode Mode { get; set; }
}

/// <summary>
///     Dictionary/vocabulary configuration bound from the <c>dictionary:</c> section of
///     <c>.ste100mark.yaml</c>.
/// </summary>
internal sealed class DictionaryConfig
{
    /// <summary>
    ///     Path (relative to the configuration file's directory, or absolute) to a project-supplied
    ///     dictionary file in the same YAML schema as the embedded default dictionary. Entries in
    ///     this file override embedded entries with the same term. Projects that require true
    ///     ASD-STE100 Issue 9 compliance must supply their own licensed dictionary here — the
    ///     embedded default dictionary is only a small, originally-authored, illustrative example
    ///     (see <c>Linting/DefaultDictionary.yaml</c>).
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    ///     Inline disallowed terms and their POS-tagged sense list(s)
    ///     (<c>term: [{pos: noun|verb|adjective|adverb|any, alternatives: [..], note: "..."}, ...]</c>),
    ///     merged over (and overriding by term, full sense-list replacement) both the embedded and
    ///     project dictionary files.
    /// </summary>
    public Dictionary<string, List<DictionarySenseYaml>>? Disallow { get; set; }

    /// <summary>
    ///     Terms to remove from the merged disallow list, even if present in the embedded
    ///     dictionary, the project dictionary file, or <see cref="Disallow"/>.
    /// </summary>
    public List<string>? Allow { get; set; }

    /// <summary>
    ///     Terms to exclude from the dictionary check entirely (for example, product names). Applied
    ///     identically to <see cref="Allow"/> during merge; kept as a separate list purely for
    ///     documentation clarity in the configuration file.
    /// </summary>
    public List<string>? Ignore { get; set; }

    /// <summary>
    ///     When <see langword="false"/>, the embedded illustrative default dictionary is not loaded
    ///     and only <see cref="File"/> and <see cref="Disallow"/> entries are checked. Default
    ///     <see langword="true"/>. This flag is an addition beyond the schema in the original
    ///     feature request, added to satisfy the "embedded dictionary, unless disabled" requirement.
    /// </summary>
    public bool UseEmbedded { get; set; } = true;
}

/// <summary>
///     Root configuration model bound from <c>.ste100mark.yaml</c> (or the file supplied via
///     <c>--config</c>), controlling which files are linted, the default writing mode, rule tuning,
///     and dictionary sources.
/// </summary>
internal sealed class LintConfig
{
    /// <summary>
    ///     Glob patterns identifying Markdown files to lint when no positional glob arguments are
    ///     supplied on the command line. Defaults to an empty list, which <see cref="Linter"/>
    ///     treats as <c>**/*.md</c>.
    /// </summary>
    public List<string> Include { get; set; } = [];

    /// <summary>
    ///     Glob patterns identifying files to exclude from <see cref="Include"/> matches.
    /// </summary>
    public List<string> Exclude { get; set; } = [];

    /// <summary>
    ///     Writing mode applied to files that do not match any pattern in <see cref="Overrides"/>.
    ///     Default <see cref="LintMode.Descriptive"/>.
    /// </summary>
    public LintMode DefaultMode { get; set; } = LintMode.Descriptive;

    /// <summary>
    ///     Glob-to-mode overrides evaluated in declaration order; the first matching pattern wins.
    /// </summary>
    public List<ModeOverride> Overrides { get; set; } = [];

    /// <summary>
    ///     Mechanical and advisory rule tuning. Defaults to <see cref="RulesConfig"/>'s own defaults
    ///     when not specified in the configuration file.
    /// </summary>
    public RulesConfig Rules { get; set; } = new();

    /// <summary>
    ///     Dictionary/vocabulary configuration, or <see langword="null"/> to use only the embedded
    ///     default dictionary with no project overrides.
    /// </summary>
    public DictionaryConfig? Dictionary { get; set; }

    /// <summary>
    ///     Loads and parses a lint configuration file, or returns an all-defaults configuration when
    ///     <paramref name="path"/> is <see langword="null"/> (no configuration file resolved).
    /// </summary>
    /// <remarks>
    ///     A <see langword="null"/> path represents "no configuration file was found" rather than an
    ///     error — the tool is usable with zero configuration, falling back to
    ///     <c>**/*.md</c> and all rule defaults. A non-null path that does not exist on disk is
    ///     always an error, whether it came from the default <c>.ste100mark.yaml</c> lookup (which
    ///     the caller only passes through when the file exists) or an explicit <c>--config</c> value.
    /// </remarks>
    /// <param name="path">Resolved configuration file path, or <see langword="null"/>.</param>
    /// <returns>A fully populated <see cref="LintConfig"/>.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <paramref name="path"/> is non-null but the file does not exist, or its
    ///     contents cannot be parsed as valid YAML matching the expected schema.
    /// </exception>
    public static LintConfig Load(string? path)
    {
        // No configuration file resolved: the tool still works with built-in defaults so that a
        // first-time user can lint Markdown without writing any configuration.
        if (path is null)
        {
            return new LintConfig();
        }

        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Lint configuration file '{path}' not found.");
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(HyphenatedNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var yaml = File.ReadAllText(path);

            // An empty or comment-only file deserializes to null; treat it as all-defaults rather
            // than a null-reference failure downstream.
            return deserializer.Deserialize<LintConfig>(yaml) ?? new LintConfig();
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            // YamlDotNet throws its own exception hierarchy (YamlException and friends) for parse
            // errors; wrapping in InvalidOperationException gives Program.Main a single exception
            // type to catch for all expected configuration failures.
            throw new InvalidOperationException($"Failed to parse lint configuration file '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Resolves the <see cref="LintMode"/> that applies to a specific file, per the glob-mapping
    ///     rules in <see cref="Overrides"/>.
    /// </summary>
    /// <remarks>
    ///     Only <see cref="Overrides"/> glob mapping is used for v1 mode resolution — there is no
    ///     per-file Markdown front-matter override, per the feature's explicit scope decision.
    /// </remarks>
    /// <param name="relativeFilePath">
    ///     File path, relative to the same base directory the override globs are written against
    ///     (typically the current working directory), using forward slashes.
    /// </param>
    /// <returns>
    ///     The <see cref="LintMode"/> of the first matching entry in <see cref="Overrides"/>, or
    ///     <see cref="DefaultMode"/> when no override matches.
    /// </returns>
    public LintMode ResolveMode(string relativeFilePath)
    {
        foreach (var over in Overrides)
        {
            var matcher = new Matcher();
            matcher.AddInclude(over.Glob);
            if (matcher.Match(relativeFilePath).HasMatches)
            {
                return over.Mode;
            }
        }

        return DefaultMode;
    }
}
