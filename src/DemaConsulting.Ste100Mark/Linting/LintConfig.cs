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

    /// <summary>
    ///     Returns a copy of this configuration with every non-null field of
    ///     <paramref name="overrideValues"/> applied on top, used to layer a matching
    ///     <see cref="Profile"/>'s <c>rules</c> deltas over the global defaults. Fields left
    ///     <see langword="null"/> in <paramref name="overrideValues"/> keep this instance's value.
    /// </summary>
    /// <param name="overrideValues">Partial rule overrides from a matching profile, or <see langword="null"/>.</param>
    /// <returns>A new <see cref="RulesConfig"/> with the overrides layered on top of this instance.</returns>
    public RulesConfig WithOverrides(RulesOverride? overrideValues)
    {
        if (overrideValues is null)
        {
            return this;
        }

        return new RulesConfig
        {
            MaxWordsProcedure = overrideValues.MaxWordsProcedure ?? MaxWordsProcedure,
            MaxWordsDescriptive = overrideValues.MaxWordsDescriptive ?? MaxWordsDescriptive,
            AllowSemicolons = overrideValues.AllowSemicolons ?? AllowSemicolons,
            AllowContractions = overrideValues.AllowContractions ?? AllowContractions,
            MaxSentencesParagraph = overrideValues.MaxSentencesParagraph ?? MaxSentencesParagraph,
            PassiveVoice = overrideValues.PassiveVoice ?? PassiveVoice,
            ComplexVerb = overrideValues.ComplexVerb ?? ComplexVerb,
            IngForm = overrideValues.IngForm ?? IngForm
        };
    }
}

/// <summary>
///     Partial rule tuning deltas bound from a <see cref="Profile"/>'s <c>rules:</c> section.
///     Every property is nullable so that a profile only needs to state the knobs it changes;
///     unset (<see langword="null"/>) properties fall through to the global
///     <see cref="LintConfig.Rules"/> value via <see cref="RulesConfig.WithOverrides"/>.
/// </summary>
internal sealed class RulesOverride
{
    /// <summary>Overrides <see cref="RulesConfig.MaxWordsProcedure"/> for matching files, when set.</summary>
    public int? MaxWordsProcedure { get; set; }

    /// <summary>Overrides <see cref="RulesConfig.MaxWordsDescriptive"/> for matching files, when set.</summary>
    public int? MaxWordsDescriptive { get; set; }

    /// <summary>Overrides <see cref="RulesConfig.AllowSemicolons"/> for matching files, when set.</summary>
    public bool? AllowSemicolons { get; set; }

    /// <summary>Overrides <see cref="RulesConfig.AllowContractions"/> for matching files, when set.</summary>
    public bool? AllowContractions { get; set; }

    /// <summary>Overrides <see cref="RulesConfig.MaxSentencesParagraph"/> for matching files, when set.</summary>
    public int? MaxSentencesParagraph { get; set; }

    /// <summary>Overrides <see cref="RulesConfig.PassiveVoice"/> for matching files, when set.</summary>
    public Severity? PassiveVoice { get; set; }

    /// <summary>Overrides <see cref="RulesConfig.ComplexVerb"/> for matching files, when set.</summary>
    public Severity? ComplexVerb { get; set; }

    /// <summary>Overrides <see cref="RulesConfig.IngForm"/> for matching files, when set.</summary>
    public Severity? IngForm { get; set; }
}

/// <summary>
///     Partial dictionary deltas bound from a <see cref="Profile"/>'s <c>dictionary:</c> section.
///     Unlike the top-level <see cref="DictionaryConfig"/>, a profile cannot supply a
///     <c>file</c>/<c>disallow</c>/<c>use-embedded</c> source (those remain global so that the
///     merged term-to-sense mapping is identical everywhere); it can only layer additional
///     <see cref="Allow"/>/<see cref="Ignore"/> terms on top of the global dictionary for files
///     it matches, for example to permit "shall" in requirements documents.
/// </summary>
internal sealed class DictionaryOverride
{
    /// <summary>
    ///     Additional terms to allow (not report) for files matching this profile, unioned with
    ///     the global <see cref="DictionaryConfig.Allow"/> list.
    /// </summary>
    public List<string>? Allow { get; set; }

    /// <summary>
    ///     Additional terms to ignore for files matching this profile, unioned with the global
    ///     <see cref="DictionaryConfig.Ignore"/> list. Applied identically to <see cref="Allow"/>.
    /// </summary>
    public List<string>? Ignore { get; set; }

    /// <summary>
    ///     Additional phrase-scoped allowances applied for files matching this profile, unioned
    ///     with the global <see cref="DictionaryConfig.AllowInPhrase"/> list.
    /// </summary>
    public List<string>? AllowInPhrase { get; set; }
}

/// <summary>
///     Maps a glob pattern to a <see cref="LintMode"/> and/or a set of rule/dictionary deltas that
///     files matching it should be checked against, overriding <see cref="LintConfig.DefaultMode"/>,
///     <see cref="LintConfig.Rules"/>, and <see cref="LintConfig.Dictionary"/> for those files.
/// </summary>
/// <remarks>
///     A profile lets a project vary linting behavior by document type or location without
///     duplicating the whole configuration - for example, procedural documents needing the
///     shorter Rule 4.1 sentence limit, or a requirements folder that legitimately uses the word
///     "shall" and would otherwise be flagged by a project's dictionary. <see cref="Mode"/> is
///     resolved by first-declared-match-wins (see <see cref="LintConfig.ResolveMode"/>), while
///     <see cref="Rules"/> and <see cref="Dictionary"/> deltas from every matching profile are
///     layered together (see <see cref="LintConfig.ResolveRules"/> and
///     <see cref="LintConfig.ResolveAllowedTerms"/>), so a file can pick up a mode from one
///     profile and a dictionary allowance from another.
/// </remarks>
internal sealed class Profile
{
    /// <summary>
    ///     Glob pattern (relative to the configuration file's directory) identifying the files this
    ///     profile applies to, for example <c>docs/user_guide/procedures/**/*.md</c>.
    /// </summary>
    public string Glob { get; set; } = string.Empty;

    /// <summary>
    ///     The mode to apply to files matching <see cref="Glob"/>, or <see langword="null"/> to
    ///     leave the mode resolution to the default mode or another matching profile.
    /// </summary>
    public LintMode? Mode { get; set; }

    /// <summary>
    ///     Partial rule tuning deltas applied on top of <see cref="LintConfig.Rules"/> for files
    ///     matching <see cref="Glob"/>, or <see langword="null"/> for no rule changes.
    /// </summary>
    public RulesOverride? Rules { get; set; }

    /// <summary>
    ///     Additional dictionary allow/ignore terms applied for files matching <see cref="Glob"/>,
    ///     or <see langword="null"/> for no dictionary changes.
    /// </summary>
    public DictionaryOverride? Dictionary { get; set; }
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
    ///     Multi-word phrases (for example <c>"swish mix"</c>) whose disallowed sub-terms (for
    ///     example <c>"mix"</c>) are suppressed only when the match falls entirely inside an
    ///     occurrence of one of these phrases, leaving the same term still flagged everywhere else
    ///     it appears. Unlike <see cref="Allow"/>/<see cref="Ignore"/>, which suppress a term
    ///     project-wide regardless of context, this expresses "this exact phrase is the approved
    ///     name of a thing" without also silently permitting the disallowed word on its own.
    ///     Matching is case-insensitive and whitespace-tolerant, identical to a multi-word
    ///     <see cref="Disallow"/> term.
    /// </summary>
    public List<string>? AllowInPhrase { get; set; }

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
    ///     Writing mode applied to files that do not match a <see cref="Profile.Mode"/> in
    ///     <see cref="Profiles"/>. Default <see cref="LintMode.Descriptive"/>.
    /// </summary>
    public LintMode DefaultMode { get; set; } = LintMode.Descriptive;

    /// <summary>
    ///     Glob-scoped profiles evaluated in declaration order. Each profile may set a
    ///     <see cref="Profile.Mode"/> (first matching profile with a non-null mode wins) and/or
    ///     <see cref="Profile.Rules"/>/<see cref="Profile.Dictionary"/> deltas (every matching
    ///     profile's deltas are layered together; see <see cref="ResolveMode"/>,
    ///     <see cref="ResolveRules"/>, and <see cref="ResolveAllowedTerms"/>).
    /// </summary>
    public List<Profile> Profiles { get; set; } = [];

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
    ///     Resolves the <see cref="LintMode"/> that applies to a specific file: the first
    ///     <see cref="Profiles"/> entry (in declaration order) that both matches the file's glob
    ///     and specifies a non-null <see cref="Profile.Mode"/> wins; profiles that only carry
    ///     <see cref="Profile.Rules"/>/<see cref="Profile.Dictionary"/> deltas are skipped for mode
    ///     resolution and considered separately by <see cref="ResolveRules"/>/
    ///     <see cref="ResolveAllowedTerms"/>.
    /// </summary>
    /// <param name="relativeFilePath">
    ///     File path, relative to the same base directory the profile globs are written against
    ///     (typically the current working directory), using forward slashes.
    /// </param>
    /// <returns>
    ///     The <see cref="LintMode"/> of the first matching profile that specifies one, or
    ///     <see cref="DefaultMode"/> when no matching profile specifies a mode.
    /// </returns>
    public LintMode ResolveMode(string relativeFilePath)
    {
        foreach (var profile in Profiles)
        {
            if (profile.Mode is not { } mode || !MatchesGlob(profile.Glob, relativeFilePath))
            {
                continue;
            }

            return mode;
        }

        return DefaultMode;
    }

    /// <summary>
    ///     Resolves the effective <see cref="RulesConfig"/> for a specific file by layering every
    ///     matching <see cref="Profiles"/> entry's <see cref="Profile.Rules"/> delta, in
    ///     declaration order, on top of the global <see cref="Rules"/>.
    /// </summary>
    /// <remarks>
    ///     Unlike <see cref="ResolveMode"/> (first match wins), every matching profile contributes:
    ///     a file can match a "procedures" profile that sets <see cref="RulesOverride.MaxWordsProcedure"/>
    ///     and, separately, a "requirements" profile that sets
    ///     <see cref="RulesOverride.PassiveVoice"/>, and both deltas apply together. Later matching
    ///     profiles win over earlier ones for any single knob both set.
    /// </remarks>
    /// <param name="relativeFilePath">
    ///     File path, relative to the same base directory the profile globs are written against.
    /// </param>
    /// <returns>The effective rules for this file.</returns>
    public RulesConfig ResolveRules(string relativeFilePath)
    {
        var resolved = Rules;
        foreach (var profile in Profiles)
        {
            if (profile.Rules is null || !MatchesGlob(profile.Glob, relativeFilePath))
            {
                continue;
            }

            resolved = resolved.WithOverrides(profile.Rules);
        }

        return resolved;
    }

    /// <summary>
    ///     Resolves the additional dictionary allow/ignore terms that apply to a specific file by
    ///     unioning every matching <see cref="Profiles"/> entry's <see cref="Profile.Dictionary"/>
    ///     delta with the global <see cref="DictionaryConfig.Allow"/>/<see cref="DictionaryConfig.Ignore"/>
    ///     lists.
    /// </summary>
    /// <param name="relativeFilePath">
    ///     File path, relative to the same base directory the profile globs are written against.
    /// </param>
    /// <returns>
    ///     Every term to treat as allowed for this file: the global <see cref="Dictionary"/>'s
    ///     <see cref="DictionaryConfig.Allow"/>/<see cref="DictionaryConfig.Ignore"/> entries plus
    ///     the <see cref="DictionaryOverride.Allow"/>/<see cref="DictionaryOverride.Ignore"/>
    ///     entries of every matching profile, case-insensitively de-duplicated.
    /// </returns>
    public IReadOnlyCollection<string> ResolveAllowedTerms(string relativeFilePath)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddTerms(allowed, Dictionary?.Allow);
        AddTerms(allowed, Dictionary?.Ignore);

        foreach (var profile in Profiles)
        {
            if (profile.Dictionary is null || !MatchesGlob(profile.Glob, relativeFilePath))
            {
                continue;
            }

            AddTerms(allowed, profile.Dictionary.Allow);
            AddTerms(allowed, profile.Dictionary.Ignore);
        }

        return allowed;
    }

    /// <summary>
    ///     Resolves the phrase-scoped dictionary allowances that apply to a specific file, unioning
    ///     the global <see cref="DictionaryConfig.AllowInPhrase"/> list with every matching
    ///     <see cref="Profiles"/> entry's <see cref="Profile.Dictionary"/>
    ///     <see cref="DictionaryOverride.AllowInPhrase"/> delta.
    /// </summary>
    /// <param name="relativeFilePath">
    ///     File path, relative to the same base directory the profile globs are written against.
    /// </param>
    /// <returns>
    ///     Every phrase within which a disallowed term match should be suppressed for this file,
    ///     case-insensitively de-duplicated. See <see cref="DictionaryConfig.AllowInPhrase"/> for
    ///     matching semantics.
    /// </returns>
    public IReadOnlyCollection<string> ResolveAllowedPhrases(string relativeFilePath)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddTerms(allowed, Dictionary?.AllowInPhrase);

        foreach (var profile in Profiles)
        {
            if (profile.Dictionary is null || !MatchesGlob(profile.Glob, relativeFilePath))
            {
                continue;
            }

            AddTerms(allowed, profile.Dictionary.AllowInPhrase);
        }

        return allowed;
    }

    /// <summary>
    ///     Adds every term in <paramref name="terms"/> to <paramref name="target"/>, if any.
    /// </summary>
    /// <param name="target">Set to add to.</param>
    /// <param name="terms">Terms to add, or <see langword="null"/> for no-op.</param>
    private static void AddTerms(HashSet<string> target, IEnumerable<string>? terms)
    {
        if (terms is null)
        {
            return;
        }

        foreach (var term in terms)
        {
            target.Add(term);
        }
    }

    /// <summary>
    ///     Tests whether <paramref name="relativeFilePath"/> matches <paramref name="glob"/>.
    /// </summary>
    /// <param name="glob">Glob pattern to match against.</param>
    /// <param name="relativeFilePath">File path to test.</param>
    /// <returns><see langword="true"/> if the glob matches.</returns>
    private static bool MatchesGlob(string glob, string relativeFilePath)
    {
        var matcher = new Matcher();
        matcher.AddInclude(glob);
        return matcher.Match(relativeFilePath).HasMatches;
    }
}

