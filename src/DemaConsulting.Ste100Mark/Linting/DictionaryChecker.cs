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
    /// <returns>One diagnostic per matched occurrence, in segment order.</returns>
    public static IReadOnlyList<Diagnostic> Evaluate(
        string file,
        IReadOnlyList<ProseSegment> segments,
        LintDictionary dictionary,
        LintMode mode,
        IReadOnlyCollection<string>? extraAllowedTerms = null)
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

            foreach (var entry in entries)
            {
                var pattern = $@"(?<![\w-]){Regex.Escape(entry.Term).Replace(@"\ ", @"\s+", StringComparison.Ordinal)}(?![\w-])";
                foreach (Match match in Regex.Matches(segment.Text, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
                {
                    if (MarkdownProseExtractor.OverlapsInlineCodeSpan(match.Index, match.Length, codeSpans))
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

        return candidates.Count == 1
            ? ConfidentDiagnostic(file, segment, match, candidates[0], labelPos: entry.Senses.Count > 1)
            : AmbiguousDiagnostic(file, segment, match, candidates);
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
            segment.LineNumber,
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
            segment.LineNumber,
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
