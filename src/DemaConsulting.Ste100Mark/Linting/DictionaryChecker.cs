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

namespace DemaConsulting.Ste100Mark.Linting;

/// <summary>
///     Scans prose text for disallowed vocabulary from a merged <see cref="LintDictionary"/> and
///     reports the matched term with its suggested alternative(s), using
///     <see cref="PartOfSpeechGuesser"/> to select the applicable sense(s) when a term has more
///     than one.
/// </summary>
/// <remarks>
///     The dictionary check is a mechanical, non-advisory rule: any match is reported at
///     <see cref="Severity.Error"/>, with rule code <c>STE100-DICT</c>. Matching is case-insensitive
///     and whole-word/whole-phrase (a match cannot start or end mid-word), so that, for example,
///     "utilized" is not incorrectly flagged by a "utilize" entry unless "utilized" is itself
///     matched by the word-boundary regex (which it is not, since the boundary requires the exact
///     term). Multi-word terms match across the segment's normalized single-space-separated text.
///     A term is always evaluated through <see cref="PartOfSpeechGuesser.Guess"/>, even when it
///     has only one sense: when the guesser confidently resolves a grammatical role (noun or
///     verb) that none of the entry's senses restrict, the term is not being used in a
///     disallowed role at this location and no diagnostic is reported. When the guess matches
///     one sense, that sense's alternative(s) are reported (labeled with the resolved part of
///     speech only when the entry has more than one sense, so a single-sense entry's message
///     stays unqualified). When the guess is inconclusive (or the entry has multiple senses none
///     of which the guess narrows to exactly one), every remaining candidate sense is reported,
///     labeled as ambiguous.
///
///     A match that falls entirely inside an inline code span (for example, a disallowed term that
///     only appears as part of `` `some-cli-flag` ``) is ignored: inline code spans are technical
///     literals, not grammar-checkable prose, so they are excluded from the dictionary check the
///     same way they are excluded from the contraction and semicolon checks in
///     <see cref="StructuralRules"/>. The same term appearing outside a code span in the same
///     segment is still flagged normally.
///
///     A match that falls entirely inside an occurrence of a project-supplied allowed phrase (see
///     <see cref="DictionaryConfig.AllowInPhrase"/>, resolved per-file into
///     <c>allowedPhrases</c>) is also ignored, using the same "falls entirely inside" containment
///     test as the inline-code-span exclusion. This lets a project declare that a specific phrase
///     (for example "swish mix") is the approved name of a thing, without also silently permitting
///     the disallowed word ("mix") everywhere else it appears - unlike
///     <see cref="LintConfig.ResolveAllowedTerms"/>, which suppresses a term unconditionally.
/// </remarks>
internal static class DictionaryChecker
{
    /// <summary>
    ///     Scans every prose segment of a Markdown file against the merged dictionary.
    /// </summary>
    /// <param name="file">File path used to populate <see cref="Diagnostic.File"/>.</param>
    /// <param name="segments">Prose segments produced by <see cref="MarkdownProseExtractor"/>.</param>
    /// <param name="dictionary">Merged dictionary to check against.</param>
    /// <param name="mode">
    ///     The file's resolved <see cref="LintMode"/>, forwarded to <see cref="PartOfSpeechGuesser.Guess"/>
    ///     for the imperative-sentence-start signal.
    /// </param>
    /// <param name="extraAllowedTerms">
    ///     Additional terms to treat as allowed for this file only, on top of
    ///     <paramref name="dictionary"/>'s own global allow/ignore lists - typically the file's
    ///     resolved <see cref="LintConfig.ResolveAllowedTerms"/> profile deltas (for example,
    ///     permitting "shall" only for a requirements-documents profile). Pass <see langword="null"/>
    ///     or an empty collection when no per-file allowance applies.
    /// </param>
    /// <param name="allowedPhrases">
    ///     Phrases (see <see cref="DictionaryConfig.AllowInPhrase"/>, resolved per-file
    ///     via <see cref="LintConfig.ResolveAllowedPhrases"/>) within which a disallowed term match
    ///     is suppressed, without suppressing the same term elsewhere in the segment. Pass
    ///     <see langword="null"/> or an empty collection when no phrase-scoped allowance applies.
    /// </param>
    /// <returns>One diagnostic per matched occurrence, in segment order.</returns>
    public static IReadOnlyList<Diagnostic> Evaluate(
        string file,
        IReadOnlyList<ProseSegment> segments,
        LintDictionary dictionary,
        LintMode mode,
        IReadOnlyCollection<string>? extraAllowedTerms = null,
        IReadOnlyCollection<string>? allowedPhrases = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(dictionary);

        var allowedTerms = extraAllowedTerms is { Count: > 0 }
            ? new HashSet<string>(extraAllowedTerms, StringComparer.OrdinalIgnoreCase)
            : null;

        // Longer (more specific) terms are matched first so that a multi-word phrase like
        // "prior to" is reported once, rather than also triggering a shorter, unrelated
        // single-word entry that happens to overlap.
        var entries = dictionary.Entries
            .Where(e => allowedTerms is null || !allowedTerms.Contains(e.Term))
            .OrderByDescending(e => e.Term.Length)
            .ToList();


        var diagnostics = new List<Diagnostic>();
        foreach (var segment in segments)
        {
            var codeSpans = MarkdownProseExtractor.FindInlineCodeSpans(segment.Text);
            var phraseSpans = FindAllowedPhraseSpans(segment.Text, allowedPhrases);

            foreach (var entry in entries)
            {
                var pattern = $@"(?<![\w-]){Regex.Escape(entry.Term).Replace(@"\ ", @"\s+", StringComparison.Ordinal)}(?![\w-])";
                foreach (Match match in Regex.Matches(segment.Text, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
                {
                    if (MarkdownProseExtractor.OverlapsInlineCodeSpan(match.Index, match.Length, codeSpans))
                    {
                        continue;
                    }

                    if (MarkdownProseExtractor.OverlapsInlineCodeSpan(match.Index, match.Length, phraseSpans))
                    {
                        continue;
                    }

                    var diagnostic = BuildDiagnostic(file, segment, entry, match, mode);
                    if (diagnostic is not null)
                    {
                        diagnostics.Add(diagnostic);
                    }
                }
            }
        }

        return diagnostics;
    }

    /// <summary>
    ///     Locates every occurrence of every phrase in <paramref name="allowedPhrases"/> within
    ///     <paramref name="segmentText"/>, using the same case-insensitive, whitespace-tolerant
    ///     matching as a multi-word <see cref="DictionaryConfig.Disallow"/> term, so
    ///     callers can test whether a dictionary-term match falls entirely inside an approved
    ///     phrase (see <see cref="DictionaryConfig.AllowInPhrase"/>).
    /// </summary>
    private static IReadOnlyList<(int Start, int Length)> FindAllowedPhraseSpans(
        string segmentText,
        IReadOnlyCollection<string>? allowedPhrases)
    {
        if (allowedPhrases is not { Count: > 0 })
        {
            return [];
        }

        var spans = new List<(int Start, int Length)>();
        foreach (var phrase in allowedPhrases)
        {
            var pattern = $@"(?<![\w-]){Regex.Escape(phrase).Replace(@"\ ", @"\s+", StringComparison.Ordinal)}(?![\w-])";
            foreach (Match match in Regex.Matches(segmentText, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
            {
                spans.Add((match.Index, match.Length));
            }
        }

        return spans;
    }

    /// <summary>
    ///     Builds the diagnostic for one matched occurrence, selecting the applicable sense(s) and
    ///     the corresponding confident-or-ambiguous message/suggestion wording, or
    ///     <see langword="null"/> when a confident guess rules out every sense (the term is not
    ///     disallowed in the grammatical role it is being used in here).
    /// </summary>
    private static Diagnostic? BuildDiagnostic(
        string file,
        ProseSegment segment,
        DictionaryEntry entry,
        Match match,
        LintMode mode)
    {
        var guess = PartOfSpeechGuesser.Guess(segment.Text, match.Index, match.Length, mode);
        var candidates = guess is null
            ? entry.Senses
            : entry.Senses.Where(s => s.Pos == guess || s.Pos == PartOfSpeech.Any).ToList();

        if (guess is not null && candidates.Count == 0)
        {
            // The guesser confidently resolved a grammatical role that none of the entry's
            // senses restrict (for example, a verb-only entry matched where the term is used
            // as a noun): the word is not being used in a role ASD-STE100 disallows here, so no
            // diagnostic is reported.
            return null;
        }

        if (guess is null && entry.Senses.Count == 1 && IsSelfReferential(entry))
        {
            // A self-referential entry (its alternatives list includes its own headword) is
            // ASD-STE100's convention for "this word is approved, but only in the other part of
            // speech" (for example "test (v) -> TEST", meaning the noun "test" is fine). When the
            // guesser could not confidently resolve a role at all, an inconclusive match against a
            // self-referential, single-sense entry is more likely a missed noun-compound/other
            // signal than a genuine disallowed usage, so it is not reported. This is deliberately
            // restricted to single-sense entries: a multi-sense entry that happens to include its
            // own headword among one sense's alternatives may still have a genuinely ambiguous
            // finding worth reporting under its other sense(s), so it is not suppressed here. A
            // confident guess of the disallowed part of speech is still reported (handled by the
            // candidates check above); this only relaxes the previously-ambiguous case.
            return null;
        }

        return candidates.Count == 1
            ? ConfidentDiagnostic(file, segment, match, candidates[0], labelPos: entry.Senses.Count > 1)
            : AmbiguousDiagnostic(file, segment, match, candidates);
    }

    /// <summary>
    ///     Determines whether an entry is "self-referential": at least one sense's
    ///     <see cref="DictionarySense.Alternatives"/> list includes the entry's own headword
    ///     (case-insensitive). This is ASD-STE100's convention for a dual-role word where the
    ///     correction is a change of grammatical role rather than a different word (for example
    ///     <c>test (v) -&gt; TEST</c>, meaning the noun "test" is the approved form).
    /// </summary>
    private static bool IsSelfReferential(DictionaryEntry entry)
    {
        return entry.Senses.Any(s =>
            s.Alternatives.Any(a => string.Equals(a, entry.Term, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    ///     Builds a diagnostic for a confidently-resolved sense (either the sole sense of a
    ///     single-sense term, or the single surviving candidate of a multi-sense term).
    /// </summary>
    private static Diagnostic ConfidentDiagnostic(
        string file,
        ProseSegment segment,
        Match match,
        DictionarySense sense,
        bool labelPos)
    {
        string message;
        if (sense.Alternatives.Count > 0)
        {
            message = labelPos
                ? $"Avoid '{match.Value}'; use {JoinAlternatives(sense.Alternatives)} instead (used as a {PosLabel(sense.Pos)})."
                : $"Avoid '{match.Value}'; use {JoinAlternatives(sense.Alternatives)} instead.";
        }
        else
        {
            message = labelPos
                ? $"Avoid '{match.Value}'; it is not an approved ASD-STE100-style term (used as a {PosLabel(sense.Pos)})."
                : $"Avoid '{match.Value}'; it is not an approved ASD-STE100-style term.";
        }

        return new Diagnostic(
            file,
            segment.ResolveLine(match.Index),
            null,
            "STE100-DICT",
            Severity.Error,
            message,
            sense.Alternatives.Count > 0 ? string.Join(", ", sense.Alternatives) : null);
    }

    /// <summary>
    ///     Builds a diagnostic listing every candidate sense for a term whose grammatical role
    ///     could not be confidently resolved.
    /// </summary>
    private static Diagnostic AmbiguousDiagnostic(
        string file,
        ProseSegment segment,
        Match match,
        IReadOnlyList<DictionarySense> candidates)
    {
        var corrections = string.Join("; ", candidates.Select(s =>
            s.Alternatives.Count > 0
                ? $"as a {PosLabel(s.Pos)}, use {JoinAlternatives(s.Alternatives)}"
                : $"as a {PosLabel(s.Pos)}"));
        var suggestion = string.Join("; ", candidates.Select(s =>
            $"{string.Join(", ", s.Alternatives)} ({PosLabel(s.Pos)})"));

        return new Diagnostic(
            file,
            segment.ResolveLine(match.Index),
            null,
            "STE100-DICT",
            Severity.Error,
            $"Ambiguous part of speech for '{match.Value}' \u2014 possible corrections: {corrections}.",
            suggestion.Length > 0 ? suggestion : null);
    }

    /// <summary>
    ///     Renders a <see cref="PartOfSpeech"/> value for diagnostic message/suggestion text,
    ///     rendering <see cref="PartOfSpeech.Any"/> as "general" to avoid the awkward
    ///     "used as a any" phrasing.
    /// </summary>
    private static string PosLabel(PartOfSpeech pos) => pos switch
    {
        PartOfSpeech.Any => "general",
        PartOfSpeech.Noun => "noun",
        PartOfSpeech.Verb => "verb",
        PartOfSpeech.Adjective => "adjective",
        PartOfSpeech.Adverb => "adverb",
        _ => pos.ToString().ToLowerInvariant()
    };

    /// <summary>
    ///     Joins a sense's alternatives into natural "or" phrasing for embedding in a diagnostic
    ///     <see cref="Diagnostic.Message"/>: a single alternative is quoted alone, two alternatives
    ///     are joined with "or" (no Oxford comma), and three or more are joined with commas plus an
    ///     Oxford comma before the final "or".
    /// </summary>
    private static string JoinAlternatives(IReadOnlyList<string> alternatives)
    {
        var quoted = alternatives.Select(a => $"'{a}'").ToList();
        return quoted.Count switch
        {
            0 => string.Empty,
            1 => quoted[0],
            2 => $"{quoted[0]} or {quoted[1]}",
            _ => string.Join(", ", quoted.Take(quoted.Count - 1)) + ", or " + quoted[^1]
        };
    }
}
