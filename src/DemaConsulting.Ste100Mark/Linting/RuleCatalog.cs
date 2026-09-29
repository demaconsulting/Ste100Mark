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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace DemaConsulting.Ste100Mark.Linting;

/// <summary>
///     Central, machine-readable catalog of every rule code the linter can emit, consumed by
///     <see cref="Program"/>'s <c>--list-rules</c> dispatch path.
/// </summary>
/// <remarks>
///     This is the single source of truth for rule <em>metadata</em> (title, official/advisory
///     classification, suggestion kind, applicable modes) documented in <see cref="StructuralRules"/>
///     and <see cref="DictionaryChecker"/>; <see cref="RuleCodes"/> is the single source of truth for
///     the code <em>strings</em> themselves, shared by this catalog and both emitters. Every rule
///     code emitted by either of those units must have a corresponding <see cref="Entries"/> entry,
///     or the <c>--list-rules</c> output silently drifts out of sync with the actual linter behavior.
/// </remarks>
internal static partial class RuleCatalog
{
    /// <summary>
    ///     Gets the full list of rule catalog entries, one per rule code the linter can emit.
    /// </summary>
    public static IReadOnlyList<RuleCatalogEntry> Entries { get; } =
    [
        new RuleCatalogEntry(
            RuleCodes.SentenceWordLimit,
            "Sentence word-count limit (Rules 4.1, 8.4-8.7).",
            Official: true,
            SuggestionKind: "advice",
            Modes: ["procedure", "descriptive"]),
        new RuleCatalogEntry(
            RuleCodes.NoSemicolons,
            "No semicolons (Rule 8.1).",
            Official: true,
            SuggestionKind: "advice",
            Modes: ["procedure", "descriptive"]),
        new RuleCatalogEntry(
            RuleCodes.NoContractions,
            "No contractions (Rule 4.2).",
            Official: true,
            SuggestionKind: "advice",
            Modes: ["procedure", "descriptive"]),
        new RuleCatalogEntry(
            RuleCodes.Dictionary,
            "Dictionary/vocabulary enforcement against the effective (default or configured) dictionary.",
            Official: false,
            SuggestionKind: "citationForm",
            Modes: ["procedure", "descriptive"]),
        new RuleCatalogEntry(
            RuleCodes.AdvisoryParagraphLength,
            "Paragraph sentence-count cap (advisory heuristic, not an official STE100 rule).",
            Official: false,
            SuggestionKind: "advice",
            Modes: ["procedure", "descriptive"]),
        new RuleCatalogEntry(
            RuleCodes.AdvisoryPassiveVoice,
            "Passive-voice detection (advisory heuristic, not an official STE100 rule).",
            Official: false,
            SuggestionKind: "advice",
            Modes: ["procedure", "descriptive"]),
        new RuleCatalogEntry(
            RuleCodes.AdvisoryComplexVerb,
            "Perfect/modal-perfect tense detection (advisory heuristic, not an official STE100 rule).",
            Official: false,
            SuggestionKind: "advice",
            Modes: ["procedure", "descriptive"]),
        new RuleCatalogEntry(
            RuleCodes.AdvisoryIngForm,
            "-ing form detection (advisory heuristic, not an official STE100 rule).",
            Official: false,
            SuggestionKind: "advice",
            Modes: ["procedure", "descriptive"])
    ];

    /// <summary>
    ///     Serializes <see cref="Entries"/> as a single, stable-schema JSON array.
    /// </summary>
    /// <returns>The JSON document text.</returns>
    public static string ToJson()
    {
        return JsonSerializer.Serialize(Entries, RuleCatalogJsonContext.Default.IReadOnlyListRuleCatalogEntry);
    }

    /// <summary>
    ///     Source-generated JSON serialization context, required for reflection-free, trimming- and
    ///     AOT-safe <see cref="JsonSerializer"/> usage.
    /// </summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
    [JsonSerializable(typeof(IReadOnlyList<RuleCatalogEntry>))]
    private sealed partial class RuleCatalogJsonContext : JsonSerializerContext;
}

/// <summary>
///     A single rule catalog entry describing one rule code the linter can emit.
/// </summary>
/// <param name="Code">The stable rule identifier, for example <c>STE100-4.1</c>.</param>
/// <param name="Title">Short human-readable description of what the rule checks.</param>
/// <param name="Official">
///     <see langword="true"/> for an actual ASD-STE100 numbered rule; <see langword="false"/> for
///     any rule that is not itself a numbered ASD-STE100 rule, whether an advisory heuristic or a
///     tool-defined mechanical check (for example <c>STE100-DICT</c>).
/// </param>
/// <param name="SuggestionKind">
///     <c>"advice"</c> when the rule's suggestion is free prose advice, or <c>"citationForm"</c>
///     when the rule's suggestion is an ASD-STE100 dictionary citation-form replacement.
/// </param>
/// <param name="Modes">The linting mode(s) (<c>"procedure"</c>, <c>"descriptive"</c>) the rule applies in.</param>
internal sealed record RuleCatalogEntry(
    string Code,
    string Title,
    bool Official,
    string SuggestionKind,
    IReadOnlyList<string> Modes);
