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

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DemaConsulting.Ste100Mark.Linting;

/// <summary>
///     Grammatical role a disallowed term is being used in, used to select the correct sense's
///     alternative(s) at a given match site. <see cref="Any"/> marks a sense that applies
///     regardless of grammatical role (for example, connecting phrases with no verb/noun
///     distinction).
/// </summary>
internal enum PartOfSpeech
{
    /// <summary>The sense applies regardless of grammatical role.</summary>
    Any,

    /// <summary>The sense applies when the term is used as a noun.</summary>
    Noun,

    /// <summary>The sense applies when the term is used as a verb.</summary>
    Verb,

    /// <summary>The sense applies when the term is used as an adjective.</summary>
    Adjective,

    /// <summary>The sense applies when the term is used as an adverb.</summary>
    Adverb
}

/// <summary>
///     One grammatical sense of a disallowed term: the part of speech it applies to, its
///     suggested alternative(s) for that sense, and an optional rationale.
/// </summary>
/// <param name="Pos">Grammatical role this sense applies to.</param>
/// <param name="Alternatives">One or more suggested replacement words or phrases for this sense.</param>
/// <param name="Note">Optional free-text explanation, or <see langword="null"/> when not provided.</param>
internal sealed record DictionarySense(PartOfSpeech Pos, IReadOnlyList<string> Alternatives, string? Note);

/// <summary>
///     A single disallowed-term entry: the term itself and one or more POS-tagged senses. A term
///     with exactly one sense is reported unconditionally, regardless of its
///     <see cref="DictionarySense.Pos"/> value, because no ambiguity is possible when only one
///     sense exists.
/// </summary>
/// <param name="Term">
///     The disallowed word or phrase, matched case-insensitively as a whole word/phrase by
///     <see cref="DictionaryChecker"/>.
/// </param>
/// <param name="Senses">One or more POS-tagged senses, in declaration order.</param>
internal sealed record DictionaryEntry(string Term, IReadOnlyList<DictionarySense> Senses);

/// <summary>
///     Merged vocabulary used by <see cref="DictionaryChecker"/>, combining the embedded
///     illustrative default dictionary, an optional project-supplied dictionary file, and inline
///     allow/disallow/ignore lists from <see cref="LintConfig"/>.
/// </summary>
/// <remarks>
///     <para>
///     <b>Copyright notice</b>: the embedded default dictionary
///     (<c>Linting/DefaultDictionary.yaml</c>) is a small, originally-authored set of
///     representative word substitutions written for this project. It is <b>not</b> the official
///     ASD-STE100 Part 2 Dictionary, which is commercially licensed by ASD. Projects that require
///     true ASD-STE100 Issue 9 compliance must supply their own licensed dictionary via the
///     <c>dictionary.file</c> configuration option.
///     </para>
///     <para>
///     Merge order (later sources override earlier ones by term, case-insensitively): embedded
///     default dictionary (unless <see cref="DictionaryConfig.UseEmbedded"/> is <see langword="false"/>),
///     then the project <see cref="DictionaryConfig.File"/>, then inline
///     <see cref="DictionaryConfig.Disallow"/>. Finally, every term in
///     <see cref="DictionaryConfig.Allow"/> or <see cref="DictionaryConfig.Ignore"/> is removed from
///     the merged set regardless of which source it came from.
///     </para>
/// </remarks>
internal sealed class LintDictionary
{
    /// <summary>
    ///     Logical name of the embedded default dictionary resource, derived from the project's
    ///     root namespace and the file's folder path.
    /// </summary>
    private const string EmbeddedResourceName = "DemaConsulting.Ste100Mark.Linting.DefaultDictionary.yaml";

    /// <summary>
    ///     Merged entries, keyed by term (case-insensitive).
    /// </summary>
    private readonly Dictionary<string, DictionaryEntry> _entries;

    /// <summary>
    ///     Private constructor - use <see cref="Load"/> instead.
    /// </summary>
    /// <param name="entries">Merged entries, keyed by term (case-insensitive).</param>
    private LintDictionary(Dictionary<string, DictionaryEntry> entries)
    {
        _entries = entries;
    }

    /// <summary>
    ///     Gets the merged dictionary entries, in no particular order.
    /// </summary>
    public IReadOnlyCollection<DictionaryEntry> Entries => _entries.Values;

    /// <summary>
    ///     Builds the effective dictionary for a lint run by merging the embedded default
    ///     dictionary, an optional project dictionary file, and the inline lists from
    ///     <paramref name="config"/>.
    /// </summary>
    /// <param name="config">The resolved lint configuration.</param>
    /// <param name="configDirectory">
    ///     Directory used to resolve a relative <see cref="DictionaryConfig.File"/> path. Pass the
    ///     current working directory when no configuration file was loaded from disk.
    /// </param>
    /// <returns>A merged, ready-to-query <see cref="LintDictionary"/>.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the embedded resource is missing (indicates a packaging defect), or when
    ///     <see cref="DictionaryConfig.File"/> is set but the file cannot be found or parsed.
    /// </exception>
    public static LintDictionary Load(LintConfig config, string configDirectory)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(configDirectory);

        var entries = new Dictionary<string, DictionaryEntry>(StringComparer.OrdinalIgnoreCase);
        var dictionaryConfig = config.Dictionary;

        // Layer 1: the embedded, originally-authored illustrative default dictionary (baseline),
        // unless explicitly disabled.
        if (dictionaryConfig?.UseEmbedded != false)
        {
            foreach (var entry in LoadEmbeddedDictionary())
            {
                entries[entry.Term] = entry;
            }
        }

        // Layer 2: an optional project-supplied dictionary file, in the same YAML schema, which is
        // where projects needing true ASD-STE100 compliance supply their own licensed content.
        if (!string.IsNullOrWhiteSpace(dictionaryConfig?.File))
        {
            var path = Path.IsPathRooted(dictionaryConfig.File)
                ? dictionaryConfig.File
                : Path.Combine(configDirectory, dictionaryConfig.File);

            foreach (var entry in LoadDictionaryFile(path))
            {
                entries[entry.Term] = entry;
            }
        }

        // Layer 3: inline disallow entries directly in the main configuration file. This is a
        // full per-term replace (not a per-sense merge), matching the same "last writer wins by
        // term" semantics as every other layer.
        if (dictionaryConfig?.Disallow != null)
        {
            foreach (var (term, senses) in dictionaryConfig.Disallow)
            {
                entries[term] = new DictionaryEntry(term, ConvertSenses(senses));
            }
        }

        // Layer 4: allow/ignore lists remove terms regardless of which layer introduced them.
        RemoveTerms(entries, dictionaryConfig?.Allow);
        RemoveTerms(entries, dictionaryConfig?.Ignore);

        return new LintDictionary(entries);
    }

    /// <summary>
    ///     Attempts to find a dictionary entry by exact term (case-insensitive).
    /// </summary>
    /// <param name="term">The term to look up.</param>
    /// <param name="entry">The matching entry, when found.</param>
    /// <returns><see langword="true"/> if a matching entry was found.</returns>
    public bool TryGetEntry(string term, out DictionaryEntry? entry) => _entries.TryGetValue(term, out entry);

    /// <summary>
    ///     Removes each term in <paramref name="terms"/> from <paramref name="entries"/>, if present.
    /// </summary>
    /// <param name="entries">Entry map to remove from.</param>
    /// <param name="terms">Terms to remove, or <see langword="null"/> for no-op.</param>
    private static void RemoveTerms(Dictionary<string, DictionaryEntry> entries, IEnumerable<string>? terms)
    {
        if (terms is null)
        {
            return;
        }

        foreach (var term in terms)
        {
            entries.Remove(term);
        }
    }

    /// <summary>
    ///     Loads the embedded illustrative default dictionary from the assembly's manifest
    ///     resources.
    /// </summary>
    /// <returns>Parsed dictionary entries.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the embedded resource cannot be located, which would indicate the assembly
    ///     was built without the expected <c>EmbeddedResource</c> item.
    /// </exception>
    private static IEnumerable<DictionaryEntry> LoadEmbeddedDictionary()
    {
        var assembly = typeof(LintDictionary).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
                            ?? throw new InvalidOperationException(
                                $"Embedded default dictionary resource '{EmbeddedResourceName}' was not found.");

        using var reader = new StreamReader(stream);
        return ParseDictionaryYaml(reader.ReadToEnd(), EmbeddedResourceName);
    }

    /// <summary>
    ///     Loads a project-supplied dictionary file from disk.
    /// </summary>
    /// <param name="path">Resolved absolute or relative file path.</param>
    /// <returns>Parsed dictionary entries.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the file does not exist or cannot be parsed as valid YAML.
    /// </exception>
    private static IEnumerable<DictionaryEntry> LoadDictionaryFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Dictionary file '{path}' was not found.");
        }

        return ParseDictionaryYaml(File.ReadAllText(path), path);
    }

    /// <summary>
    ///     Parses dictionary YAML text (term to sense-list mapping) into entries.
    /// </summary>
    /// <param name="yaml">Raw YAML text.</param>
    /// <param name="source">Source description used in error messages.</param>
    /// <returns>Parsed dictionary entries.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the YAML cannot be parsed.</exception>
    private static List<DictionaryEntry> ParseDictionaryYaml(string yaml, string source)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(HyphenatedNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var raw = deserializer.Deserialize<Dictionary<string, List<DictionarySenseYaml>>>(yaml)
                      ?? [];

            return raw
                .Select(kv => new DictionaryEntry(kv.Key, ConvertSenses(kv.Value)))
                .ToList();
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to parse dictionary file '{source}': {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Converts raw YAML-bound senses into immutable <see cref="DictionarySense"/> records.
    /// </summary>
    /// <param name="raw">Raw sense list bound by YamlDotNet, or <see langword="null"/>.</param>
    /// <returns>
    ///     Converted senses in declaration order, or an empty list when <paramref name="raw"/> is
    ///     <see langword="null"/> or empty (defensive; a well-formed dictionary source always
    ///     supplies at least one sense per term).
    /// </returns>
    private static IReadOnlyList<DictionarySense> ConvertSenses(List<DictionarySenseYaml>? raw)
    {
        if (raw is null || raw.Count == 0)
        {
            return [];
        }

        return raw
            .Select(sense => new DictionarySense(sense.Pos, (sense.Alternatives ?? []).AsReadOnly(), sense.Note))
            .ToList();
    }
}

/// <summary>
///     YAML binding shape for one sense within a dictionary entry's sense list:
///     <c>- pos: noun|verb|adjective|adverb|any</c>, <c>alternatives: [..]</c>, <c>note: "..."</c>.
/// </summary>
/// <remarks>
///     Promoted to internal (not a nested-private class) because <see cref="DictionaryConfig.Disallow"/>
///     in <c>LintConfig.cs</c> binds this identical per-term sense-list shape for inline entries, and
///     a single shared binding type avoids duplicating the schema. Properties are populated by
///     YamlDotNet via reflection, not by any code in this assembly, so static analysis cannot see
///     the assignment; suppressed accordingly.
/// </remarks>
#pragma warning disable S3459, S1144
internal sealed class DictionarySenseYaml
{
    /// <summary>
    ///     Grammatical role this sense applies to. Default <see cref="PartOfSpeech.Any"/>.
    /// </summary>
    public PartOfSpeech Pos { get; set; } = PartOfSpeech.Any;

    /// <summary>
    ///     Suggested replacement word(s) or phrase(s) for this sense.
    /// </summary>
    public List<string>? Alternatives { get; set; }

    /// <summary>
    ///     Optional free-text explanation of why the term is disallowed in this sense.
    /// </summary>
    public string? Note { get; set; }
}
#pragma warning restore S3459, S1144
