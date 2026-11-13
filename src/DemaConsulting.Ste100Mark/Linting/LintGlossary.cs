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

using System.Text.RegularExpressions;
using DemaConsulting.PartOfSpeech;
using YamlDotNet.Serialization;
using PackageGlossary = DemaConsulting.PartOfSpeech.Glossary;

namespace DemaConsulting.Ste100Mark.Linting;

/// <summary>
///     One approved project term from a glossary file or the inline <c>glossary:</c> section.
/// </summary>
/// <param name="Term">The term, whitespace-normalized (trimmed, internal runs collapsed to one space).</param>
/// <param name="Definition">The definition, or <see langword="null"/> when none was given.</param>
/// <param name="Pos">The parts of speech the term's head word may take.</param>
/// <param name="Variants">Variant spellings of the whole term, whitespace-normalized.</param>
/// <param name="Visible">
///     The shared-format <c>visible</c> flag. Ste100Mark reads invisible entries exactly like visible
///     ones; the flag only matters to the tool that publishes the human-readable glossary.
/// </param>
/// <param name="Source">Full path of the file (or inline label) that supplied the term.</param>
internal sealed record GlossaryTerm(
    string Term,
    string? Definition,
    UposSet Pos,
    IReadOnlyList<string> Variants,
    bool Visible,
    string Source);

/// <summary>
///     One deprecated project term and its replacement.
/// </summary>
/// <param name="Term">The deprecated term, whitespace-normalized.</param>
/// <param name="Use">The replacement to suggest.</param>
/// <param name="Where">Optional scope note describing where the term is used, or <see langword="null"/>.</param>
/// <param name="Source">Full path of the file that supplied the term.</param>
internal sealed record DeprecatedTerm(string Term, string Use, string? Where, string Source);

/// <summary>
///     Loads, merges, and validates the project glossary: approved terms (inline and from
///     <c>glossary-file</c> patterns) and deprecated terms (<c>deprecated-file</c> patterns), and
///     builds the package <see cref="PackageGlossary"/> used for part-of-speech tagging.
/// </summary>
/// <remarks>
///     Conflicts are errors, never overrides: the same term (case-insensitive) with any differing
///     field, a term that is both approved and deprecated, and an approved term or variant that is
///     also an effective dictionary key. Identical duplicates are accepted and order never matters.
///     A missing file, an unreachable location, or a glob that matches nothing is an error, never an
///     empty glossary. Unknown entry keys and invalid <c>pos</c> values are errors.
/// </remarks>
internal sealed partial class LintGlossary
{
    /// <summary>Entry keys accepted in an approved-term object.</summary>
    private static readonly string[] EntryKeys = ["definition", "pos", "variants", "visible"];

    /// <summary>Entry keys accepted in a deprecated-term object.</summary>
    private static readonly string[] DeprecatedKeys = ["use", "where"];

    /// <summary>Approved terms, one per distinct term, sorted by term.</summary>
    private readonly List<GlossaryTerm> _terms;

    /// <summary>Deprecated terms, one per distinct term, sorted by term.</summary>
    private readonly List<DeprecatedTerm> _deprecated;

    /// <summary>
    ///     Private constructor - use <see cref="Load"/> instead.
    /// </summary>
    /// <param name="terms">Merged approved terms.</param>
    /// <param name="deprecated">Merged deprecated terms.</param>
    /// <param name="tagging">The built package glossary, or <see langword="null"/> when there are no approved terms.</param>
    private LintGlossary(List<GlossaryTerm> terms, List<DeprecatedTerm> deprecated, PackageGlossary? tagging)
    {
        _terms = terms;
        _deprecated = deprecated;
        Tagging = tagging;
    }

    /// <summary>
    ///     Gets the package glossary for the part-of-speech tagger, or <see langword="null"/> when no
    ///     approved terms are configured.
    /// </summary>
    public PackageGlossary? Tagging { get; }

    /// <summary>Gets the merged approved terms.</summary>
    public IReadOnlyCollection<GlossaryTerm> Terms => _terms;

    /// <summary>Gets the merged deprecated terms.</summary>
    public IReadOnlyCollection<DeprecatedTerm> Deprecated => _deprecated;

    /// <summary>
    ///     Gets every approved term and variant; these are allowed (not flagged) by the dictionary check.
    /// </summary>
    public IReadOnlyCollection<string> GetAllowedTerms() =>
        _terms.SelectMany(t => t.Variants.Prepend(t.Term)).ToList();

    /// <summary>
    ///     Loads the project glossary described by <paramref name="config"/>.
    /// </summary>
    /// <param name="config">The resolved lint configuration.</param>
    /// <param name="configPath">Path of the configuration file, or <see langword="null"/> when none was loaded.</param>
    /// <param name="configDirectory">Directory that relative glossary paths resolve against.</param>
    /// <returns>The merged, validated glossary; empty when no glossary keys are configured.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown for missing or unreachable files, unmatched globs, undefined environment variables,
    ///     invalid entries, and conflicts.
    /// </exception>
    public static LintGlossary Load(LintConfig config, string? configPath, string configDirectory)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(configDirectory);

        var dictionaryConfig = config.Dictionary;
        var approved = new List<GlossaryTerm>();
        var deprecated = new List<DeprecatedTerm>();

        if (dictionaryConfig?.Glossary is { } inline)
        {
            var label = $"{Path.GetFullPath(configPath ?? configDirectory)} (inline glossary)";
            foreach (var (term, value) in inline)
            {
                approved.Add(ParseGlossaryEntry(term, value, label));
            }
        }

        foreach (var path in ResolveFiles(
                     DictionaryConfig.NormalizePathList(dictionaryConfig?.GlossaryFile, "glossary-file"),
                     configDirectory,
                     "Glossary file"))
        {
            foreach (var (term, value) in ReadMap(path))
            {
                approved.Add(ParseGlossaryEntry(term, value, path));
            }
        }

        foreach (var path in ResolveFiles(
                     DictionaryConfig.NormalizePathList(dictionaryConfig?.DeprecatedFile, "deprecated-file"),
                     configDirectory,
                     "Deprecated-terms file"))
        {
            foreach (var (term, value) in ReadMap(path))
            {
                deprecated.Add(ParseDeprecatedEntry(term, value, path));
            }
        }

        var conflicts = new List<string>();
        var mergedTerms = MergeApproved(approved, conflicts);
        var mergedDeprecated = MergeDeprecated(deprecated, conflicts);
        FindApprovedDeprecatedConflicts(mergedTerms, mergedDeprecated, conflicts);

        if (conflicts.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, conflicts.Order(StringComparer.Ordinal)));
        }

        var tagging = mergedTerms.Count == 0
            ? null
            : BuildTagging(mergedTerms, dictionaryConfig?.GlossaryMode ?? GlossaryModeSetting.Hard);

        return new LintGlossary(mergedTerms, mergedDeprecated, tagging);
    }

    /// <summary>
    ///     Verifies that no approved term or variant is also an effective dictionary key.
    /// </summary>
    /// <param name="dictionary">The effective merged dictionary.</param>
    /// <exception cref="InvalidOperationException">Thrown when an approved term or variant is a dictionary key.</exception>
    public void ValidateAgainst(LintDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        var conflicts = new List<string>();
        foreach (var term in _terms)
        {
            if (dictionary.TryGetSource(term.Term, out var source))
            {
                conflicts.Add(
                    $"Conflict: term '{term.Term}' is approved ({term.Source}) but is also a dictionary entry ({source}).");
            }

            foreach (var variant in term.Variants)
            {
                if (dictionary.TryGetSource(variant, out var variantSource))
                {
                    conflicts.Add(
                        $"Conflict: variant '{variant}' of term '{term.Term}' is approved ({term.Source}) but is also a dictionary entry ({variantSource}).");
                }
            }
        }

        if (conflicts.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, conflicts.Order(StringComparer.Ordinal)));
        }
    }

    /// <summary>
    ///     Expands <c>${VAR}</c> references from the environment.
    /// </summary>
    /// <param name="pattern">The configured pattern.</param>
    /// <returns>The pattern with every reference replaced.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a referenced variable is undefined or empty.</exception>
    internal static string ExpandEnvironmentVariables(string pattern)
    {
        return EnvironmentVariableRegex().Replace(pattern, match =>
        {
            var name = match.Groups[1].Value;
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException(
                    $"Environment variable '{name}' referenced by glossary path '{pattern}' is not defined.");
            }

            return value;
        });
    }

    /// <summary>
    ///     Resolves configured patterns to a sorted, de-duplicated list of existing file paths.
    /// </summary>
    /// <param name="patterns">Configured paths or globs.</param>
    /// <param name="configDirectory">Base directory for relative patterns.</param>
    /// <param name="kind">Description used in error messages.</param>
    /// <returns>Full file paths.</returns>
    private static List<string> ResolveFiles(IReadOnlyList<string> patterns, string configDirectory, string kind)
    {
        var files = new HashSet<string>(StringComparer.Ordinal);
        foreach (var configured in patterns)
        {
            var pattern = ExpandEnvironmentVariables(configured);
            if (pattern.IndexOfAny(['*', '?', '[']) >= 0)
            {
                var matches = Linter.ResolvePatterns([pattern], configDirectory);
                if (matches.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"{kind} pattern '{pattern}' (base directory '{configDirectory}') matched no files.");
                }

                foreach (var match in matches)
                {
                    files.Add(match);
                }

                continue;
            }

            var full = Path.GetFullPath(pattern, configDirectory);
            var directory = Path.GetDirectoryName(full);
            if (directory is not null && !Directory.Exists(directory))
            {
                throw new InvalidOperationException(
                    $"{kind} '{full}' is unreachable (directory '{directory}' does not exist or cannot be accessed).");
            }

            try
            {
                // File.Exists hides access failures as false, so probe by opening the file.
                using var probe = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                throw new InvalidOperationException($"{kind} '{full}' was not found.", ex);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                throw new InvalidOperationException($"{kind} '{full}' is unreachable: {ex.Message}", ex);
            }

            files.Add(full);
        }

        return files.Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    ///     Reads a glossary or deprecated-terms file as a term-to-value map.
    /// </summary>
    /// <param name="path">Full path of the file.</param>
    /// <returns>The raw map; empty for an empty or comment-only file.</returns>
    private static Dictionary<string, object?> ReadMap(string path)
    {
        string yaml;
        try
        {
            yaml = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"File '{path}' is unreachable: {ex.Message}", ex);
        }

        try
        {
            return new DeserializerBuilder().Build().Deserialize<Dictionary<string, object?>>(yaml) ?? [];
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to parse glossary file '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Parses one approved-term entry (a definition string or an object).
    /// </summary>
    /// <param name="rawTerm">The term as written.</param>
    /// <param name="value">The bound YAML value.</param>
    /// <param name="source">Source label for messages.</param>
    /// <returns>The parsed term.</returns>
    private static GlossaryTerm ParseGlossaryEntry(string rawTerm, object? value, string source)
    {
        var term = NormalizeTerm(rawTerm);
        if (term.Length == 0)
        {
            throw new InvalidOperationException($"Glossary in '{source}' contains an empty term.");
        }

        switch (value)
        {
            case null:
                return new GlossaryTerm(term, null, UposSet.Of(Upos.Noun), [], true, source);
            case string definition:
                return new GlossaryTerm(term, definition, UposSet.Of(Upos.Noun), [], true, source);
        }

        var map = AsMap(value, term, source, "an entry must be a definition string or an object");
        RejectUnknownKeys(map, EntryKeys, term, source);

        string? definitionText = null;
        var pos = UposSet.Of(Upos.Noun);
        var variants = new List<string>();
        var visible = true;

        if (map.TryGetValue("definition", out var definitionValue) && definitionValue is not null)
        {
            definitionText = definitionValue as string
                             ?? throw FieldError(term, source, "definition", "must be a string");
        }

        if (map.TryGetValue("pos", out var posValue))
        {
            pos = ParsePos(posValue, term, source);
        }

        if (map.TryGetValue("variants", out var variantsValue) && variantsValue is not null)
        {
            var items = variantsValue is string single ? [single] : AsStringList(variantsValue);
            variants = (items ?? throw FieldError(term, source, "variants", "must be a list of strings"))
                .Select(NormalizeTerm)
                .Where(v => v.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (map.TryGetValue("visible", out var visibleValue) && visibleValue is not null)
        {
            visible = visibleValue switch
            {
                bool flag => flag,
                string text when bool.TryParse(text, out var parsed) => parsed,
                _ => throw FieldError(term, source, "visible", "must be true or false")
            };
        }

        return new GlossaryTerm(term, definitionText, pos, variants, visible, source);
    }

    /// <summary>
    ///     Parses one deprecated-term entry (<c>use</c> required, <c>where</c> optional).
    /// </summary>
    /// <param name="rawTerm">The term as written.</param>
    /// <param name="value">The bound YAML value.</param>
    /// <param name="source">Full path of the file.</param>
    /// <returns>The parsed deprecated term.</returns>
    private static DeprecatedTerm ParseDeprecatedEntry(string rawTerm, object? value, string source)
    {
        var term = NormalizeTerm(rawTerm);
        if (term.Length == 0)
        {
            throw new InvalidOperationException($"Deprecated-terms file '{source}' contains an empty term.");
        }

        var map = AsMap(value, term, source, "a deprecated entry must be an object with 'use' and optional 'where'");
        RejectUnknownKeys(map, DeprecatedKeys, term, source);

        if (!map.TryGetValue("use", out var useValue) || useValue is not string use || string.IsNullOrWhiteSpace(use))
        {
            throw new InvalidOperationException(
                $"Deprecated term '{term}' in '{source}' must have a non-empty 'use' value.");
        }

        string? where = null;
        if (map.TryGetValue("where", out var whereValue) && whereValue is not null)
        {
            where = whereValue as string ?? throw FieldError(term, source, "where", "must be a string");
        }

        return new DeprecatedTerm(term, use.Trim(), where, source);
    }

    /// <summary>
    ///     Parses a <c>pos</c> value (one name or a list of names) into a tag set.
    /// </summary>
    /// <param name="value">The bound value.</param>
    /// <param name="term">Term for messages.</param>
    /// <param name="source">Source for messages.</param>
    /// <returns>The tag set.</returns>
    private static UposSet ParsePos(object? value, string term, string source)
    {
        var names = value switch
        {
            string single => [single],
            null => null,
            _ => AsStringList(value)
        };

        if (names is null || names.Count == 0)
        {
            throw FieldError(term, source, "pos", "must be one or a list of: noun, verb, adjective, adverb, proper-noun");
        }

        var set = UposSet.Empty;
        foreach (var name in names)
        {
            var tag = name.Trim().ToLowerInvariant() switch
            {
                "noun" => Upos.Noun,
                "verb" => Upos.Verb,
                "adjective" => Upos.Adj,
                "adverb" => Upos.Adv,
                "proper-noun" => Upos.Propn,
                _ => throw FieldError(
                    term,
                    source,
                    "pos",
                    $"has unsupported value '{name}'; use noun, verb, adjective, adverb, or proper-noun")
            };
            set = set.Add(tag);
        }

        return set;
    }

    /// <summary>
    ///     Converts a bound YAML sequence to a list of strings, or <see langword="null"/> when it is not one.
    /// </summary>
    /// <param name="value">The bound value.</param>
    /// <returns>The strings, or <see langword="null"/>.</returns>
    private static List<string>? AsStringList(object value)
    {
        if (value is not IEnumerable<object?> items || value is string)
        {
            return null;
        }

        var result = new List<string>();
        foreach (var item in items)
        {
            if (item is not string text)
            {
                return null;
            }

            result.Add(text);
        }

        return result;
    }

    /// <summary>
    ///     Converts a bound YAML mapping to a string-keyed dictionary, or throws.
    /// </summary>
    /// <param name="value">The bound value.</param>
    /// <param name="term">Term for messages.</param>
    /// <param name="source">Source for messages.</param>
    /// <param name="problem">Description of what was expected.</param>
    /// <returns>The mapping.</returns>
    private static Dictionary<string, object?> AsMap(object? value, string term, string source, string problem)
    {
        if (value is not IDictionary<object, object?> map)
        {
            throw new InvalidOperationException($"Term '{term}' in '{source}' is invalid: {problem}.");
        }

        return map.ToDictionary(kv => kv.Key.ToString() ?? string.Empty, kv => kv.Value, StringComparer.Ordinal);
    }

    /// <summary>
    ///     Throws when a mapping has a key outside the accepted set.
    /// </summary>
    /// <param name="map">The mapping.</param>
    /// <param name="allowed">Accepted keys.</param>
    /// <param name="term">Term for messages.</param>
    /// <param name="source">Source for messages.</param>
    private static void RejectUnknownKeys(Dictionary<string, object?> map, string[] allowed, string term, string source)
    {
        var unknown = map.Keys.Where(k => !allowed.Contains(k, StringComparer.Ordinal)).Order(StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
        {
            throw new InvalidOperationException(
                $"Term '{term}' in '{source}' has unknown key '{unknown[0]}'; allowed keys are {string.Join(", ", allowed)}.");
        }
    }

    /// <summary>
    ///     Creates the error for an invalid field value.
    /// </summary>
    /// <param name="term">Term for messages.</param>
    /// <param name="source">Source for messages.</param>
    /// <param name="field">Field name.</param>
    /// <param name="problem">Problem description.</param>
    /// <returns>The exception to throw.</returns>
    private static InvalidOperationException FieldError(string term, string source, string field, string problem) =>
        new($"Term '{term}' in '{source}' is invalid: '{field}' {problem}.");

    /// <summary>
    ///     Trims a term and collapses internal whitespace runs to one space.
    /// </summary>
    /// <param name="term">The term as written.</param>
    /// <returns>The normalized term.</returns>
    private static string NormalizeTerm(string term) => WhitespaceRegex().Replace(term.Trim(), " ");

    /// <summary>
    ///     Merges approved terms by term, accepting identical duplicates and recording conflicts.
    /// </summary>
    /// <param name="approved">Every parsed approved term.</param>
    /// <param name="conflicts">Receives conflict messages.</param>
    /// <returns>One term per distinct term, sorted by term.</returns>
    private static List<GlossaryTerm> MergeApproved(List<GlossaryTerm> approved, List<string> conflicts)
    {
        var merged = new List<GlossaryTerm>();
        foreach (var group in approved.GroupBy(t => t.Term, StringComparer.OrdinalIgnoreCase))
        {
            // Sort by source so the reported pair does not depend on file enumeration order.
            var ordered = group.OrderBy(t => t.Source, StringComparer.Ordinal).ToList();
            var first = ordered[0];
            var other = ordered.Skip(1).FirstOrDefault(t => DifferingFields(first, t).Count > 0);
            if (other is not null)
            {
                conflicts.Add(
                    $"Conflict: term '{first.Term}' differs in {string.Join(", ", DifferingFields(first, other))} between {first.Source} and {other.Source}.");
                continue;
            }

            merged.Add(first);
        }

        return merged.OrderBy(t => t.Term, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    ///     Merges deprecated terms by term, accepting identical duplicates and recording conflicts.
    /// </summary>
    /// <param name="deprecated">Every parsed deprecated term.</param>
    /// <param name="conflicts">Receives conflict messages.</param>
    /// <returns>One term per distinct term, sorted by term.</returns>
    private static List<DeprecatedTerm> MergeDeprecated(List<DeprecatedTerm> deprecated, List<string> conflicts)
    {
        var merged = new List<DeprecatedTerm>();
        foreach (var group in deprecated.GroupBy(t => t.Term, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(t => t.Source, StringComparer.Ordinal).ToList();
            var first = ordered[0];
            var other = ordered.Skip(1).FirstOrDefault(t => t.Use != first.Use || t.Where != first.Where);
            if (other is not null)
            {
                var fields = new List<string>();
                if (other.Use != first.Use)
                {
                    fields.Add("use");
                }

                if (other.Where != first.Where)
                {
                    fields.Add("where");
                }

                conflicts.Add(
                    $"Conflict: deprecated term '{first.Term}' differs in {string.Join(", ", fields)} between {first.Source} and {other.Source}.");
                continue;
            }

            merged.Add(first);
        }

        return merged.OrderBy(t => t.Term, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    ///     Records a conflict for every term (or variant) that is both approved and deprecated.
    /// </summary>
    /// <param name="approved">Merged approved terms.</param>
    /// <param name="deprecated">Merged deprecated terms.</param>
    /// <param name="conflicts">Receives conflict messages.</param>
    private static void FindApprovedDeprecatedConflicts(
        List<GlossaryTerm> approved,
        List<DeprecatedTerm> deprecated,
        List<string> conflicts)
    {
        var byTerm = deprecated.ToDictionary(d => d.Term, StringComparer.OrdinalIgnoreCase);
        foreach (var term in approved)
        {
            if (byTerm.TryGetValue(term.Term, out var match))
            {
                conflicts.Add(
                    $"Conflict: term '{term.Term}' is both approved ({term.Source}) and deprecated ({match.Source}).");
            }

            foreach (var variant in term.Variants.Where(byTerm.ContainsKey))
            {
                conflicts.Add(
                    $"Conflict: variant '{variant}' of term '{term.Term}' is approved ({term.Source}) but also deprecated ({byTerm[variant].Source}).");
            }
        }
    }

    /// <summary>
    ///     Lists the fields in which two approved entries for the same term differ.
    /// </summary>
    /// <param name="a">First entry.</param>
    /// <param name="b">Second entry.</param>
    /// <returns>Names of the differing fields; empty when identical.</returns>
    private static List<string> DifferingFields(GlossaryTerm a, GlossaryTerm b)
    {
        var fields = new List<string>();
        if (!string.Equals(a.Definition, b.Definition, StringComparison.Ordinal))
        {
            fields.Add("definition");
        }

        if (!a.Pos.Equals(b.Pos))
        {
            fields.Add("pos");
        }

        if (!a.Variants.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(b.Variants))
        {
            fields.Add("variants");
        }

        if (a.Visible != b.Visible)
        {
            fields.Add("visible");
        }

        return fields;
    }

    /// <summary>
    ///     Builds the package glossary from the merged terms.
    /// </summary>
    /// <param name="terms">Merged approved terms.</param>
    /// <param name="mode">Hard or soft tagging mode.</param>
    /// <returns>The package glossary.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the package rejects a term or the combination.</exception>
    private static PackageGlossary BuildTagging(List<GlossaryTerm> terms, GlossaryModeSetting mode)
    {
        // Detect surface forms claimed by two different entries so the error names the terms.
        var owners = new Dictionary<string, GlossaryTerm>(StringComparer.OrdinalIgnoreCase);
        foreach (var term in terms)
        {
            foreach (var form in new[] { term.Term }.Concat(term.Variants))
            {
                var key = WhitespaceRegex().Replace(form.Trim(), " ");
                if (owners.TryGetValue(key, out var owner) && !ReferenceEquals(owner, term))
                {
                    throw new InvalidOperationException(
                        $"The glossary could not be built: '{form}' of term '{term.Term}' ({term.Source}) duplicates '{owner.Term}' ({owner.Source}).");
                }

                owners[key] = term;
            }
        }

        var entries = new List<GlossaryEntry>();
        foreach (var term in terms)
        {
            try
            {
                var entry = new GlossaryEntry(term.Term, term.Pos, term.Definition);
                entries.Add(term.Variants.Count == 0 ? entry : entry.WithVariants([.. term.Variants]));
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException(
                    $"Glossary term '{term.Term}' in '{term.Source}' was rejected: {ex.Message}", ex);
            }
        }

        try
        {
            return PackageGlossary.Create(
                entries,
                new GlossaryOptions { Mode = mode == GlossaryModeSetting.Soft ? GlossaryMode.Soft : GlossaryMode.Hard });
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"The glossary could not be built: {ex.Message}", ex);
        }
    }

    /// <summary>Matches a <c>${NAME}</c> environment variable reference.</summary>
    [GeneratedRegex(@"\$\{([A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex EnvironmentVariableRegex();

    /// <summary>Matches a run of whitespace.</summary>
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
