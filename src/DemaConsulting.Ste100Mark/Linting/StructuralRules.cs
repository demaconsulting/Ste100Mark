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
///     Evaluates the structural/mechanical ASD-STE100 Issue 9 rules and the two advisory
///     heuristics against extracted <see cref="ProseSegment"/>s.
/// </summary>
/// <remarks>
///     Rule codes and their official/advisory status:
///     <list type="bullet">
///         <item><c>STE100-4.1</c> - sentence word-count limit (official Rules 4.1/8.4-8.7).</item>
///         <item><c>STE100-8.1</c> - no semicolons (official Rule 8.1).</item>
///         <item><c>STE100-4.2</c> - no contractions (official Rule 4.2).</item>
///         <item><c>STE100-ADV-PARA</c> - paragraph sentence-count cap (advisory heuristic, not an official STE100 rule).</item>
///         <item><c>STE100-ADV-PASSIVE</c> - passive-voice detection (advisory heuristic, not an official STE100 rule).</item>
///         <item><c>STE100-ADV-COMPLEXVERB</c> - perfect/modal-perfect tense detection (advisory heuristic, not an official STE100 rule).</item>
///         <item><c>STE100-ADV-INGFORM</c> - <c>-ing</c> form detection (advisory heuristic, not an official STE100 rule).</item>
///     </list>
///     The semicolon, contraction, and passive-voice checks are grammar-sensitive and exclude
///     content that falls only inside an inline code span (using
///     <see cref="MarkdownProseExtractor.MaskInlineCodeSpans"/>/
///     <see cref="MarkdownProseExtractor.OverlapsInlineCodeSpan"/>), while still displaying the
///     verbatim segment/sentence text (including the code span) in diagnostic messages. The
///     sentence word-count check (<c>STE100-4.1</c>) is unaffected: it counts each inline code span
///     as a single word via <see cref="SentenceAnalyzer.CountWords"/>.
/// </remarks>
internal static class StructuralRules
{
    /// <summary>
    ///     Timeout applied to every regular expression in this class, bounding worst-case matching
    ///     time against pathological input rather than allowing unbounded backtracking.
    /// </summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Matches common English contractions (Rule 4.2). The <c>'s</c> suffix is ambiguous between
    ///     a contraction ("it's" = "it is") and a possessive ("project's"), so it is matched here and
    ///     disambiguated afterwards in <see cref="EvaluateContractions"/> using
    ///     <see cref="ApostropheSContractionWords"/>.
    /// </summary>
    private static readonly Regex ContractionRegex =
        new(@"\b\w+'(t|s|re|ve|ll|d|m)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout);

    /// <summary>
    ///     Words for which a trailing <c>'s</c> is (almost) always the contraction "is"/"has" rather
    ///     than a possessive, e.g. "it's", "that's", "who's". Any other <c>word's</c> match is treated
    ///     as a possessive and is not flagged, since ASD-STE100 Rule 4.2 prohibits contractions, not
    ///     possessives.
    /// </summary>
    private static readonly HashSet<string> ApostropheSContractionWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "it", "that", "this", "there", "here", "what", "who", "how", "when", "where", "why",
        "he", "she", "one", "everybody", "somebody", "nobody", "everyone", "someone", "no one",
        "let", "such",
    };

    /// <summary>
    ///     Heuristic passive-voice pattern: a form of "to be" followed by a past-participle-looking
    ///     word (ending in <c>-ed</c> or <c>-en</c>). The <c>been</c> alternative excludes a match
    ///     immediately preceded by <c>has</c>/<c>have</c>/<c>had</c> via a negative lookbehind, since
    ///     that construction ("has/have/had been washed") is a perfect-tense passive that
    ///     <see cref="ComplexVerbRegex"/> (evaluated first, in <see cref="EvaluateComplexVerb"/>)
    ///     already owns; this precedence decision avoids double-counting the same span as both
    ///     <c>STE100-ADV-COMPLEXVERB</c> and <c>STE100-ADV-PASSIVE</c>. The other "to be" forms
    ///     (<c>is</c>/<c>are</c>/<c>was</c>/<c>were</c>/<c>be</c>/<c>being</c>) are unaffected,
    ///     since a modal-perfect like "will have written" does not use any of those forms.
    /// </summary>
    private static readonly Regex PassiveVoiceRegex =
        new(@"\b(is|are|was|were|be|being|(?<!(?:has|have|had)\s+)been)\s+\w+(ed|en)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout);

    /// <summary>
    ///     Heuristic perfect/modal-perfect tense pattern: <c>has</c>/<c>have</c>/<c>had</c>
    ///     (optionally followed by <c>been</c>) plus a past-participle-looking word, or a modal verb
    ///     plus <c>have</c> plus a past-participle-looking word. See the precedence note on
    ///     <see cref="PassiveVoiceRegex"/> for why this pattern takes ownership of "has/have/had
    ///     been X" instead of the passive-voice heuristic.
    /// </summary>
    private static readonly Regex ComplexVerbRegex =
        new(@"\b((has|have|had)\s+(been\s+)?\w+(ed|en)|(will|would|could|should|may|might|must)\s+have\s+\w+(ed|en))\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout);

    /// <summary>
    ///     Heuristic <c>-ing</c> form pattern: a word of at least five letters ending in <c>ing</c>.
    ///     ASD-STE100 restricts <c>-ing</c> forms to technical nouns/adjectives, not present-
    ///     participle verb forms, so this is a broad, high-recall heuristic that intentionally
    ///     flags many common gerunds/participles (see design doc for expected noise level).
    /// </summary>
    private static readonly Regex IngFormRegex =
        new(@"\b[a-z][a-z-]{2,}ing\b", RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout);

    /// <summary>
    ///     Evaluates every structural/mechanical rule and advisory heuristic against the prose
    ///     segments of a single Markdown file.
    /// </summary>
    /// <param name="file">File path used to populate <see cref="Diagnostic.File"/>.</param>
    /// <param name="segments">Prose segments produced by <see cref="MarkdownProseExtractor"/>.</param>
    /// <param name="mode">Resolved writing mode, determining the sentence word-count limit.</param>
    /// <param name="rules">Rule tuning from the effective <see cref="LintConfig"/>.</param>
    /// <returns>All diagnostics produced for the file, in segment order.</returns>
    public static IReadOnlyList<Diagnostic> Evaluate(
        string file,
        IReadOnlyList<ProseSegment> segments,
        LintMode mode,
        RulesConfig rules)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(rules);

        var diagnostics = new List<Diagnostic>();
        var maxWords = mode == LintMode.Procedure ? rules.MaxWordsProcedure : rules.MaxWordsDescriptive;

        foreach (var segment in segments)
        {
            var sentences = SentenceAnalyzer.Split(segment.Text);

            EvaluateWordLimit(file, segment, sentences, maxWords, mode, diagnostics);
            EvaluateSemicolons(file, segment, rules, diagnostics);
            EvaluateContractions(file, segment, rules, diagnostics);
            EvaluateComplexVerb(file, segment, sentences, rules, diagnostics);
            EvaluatePassiveVoice(file, segment, sentences, rules, diagnostics);
            EvaluateIngForm(file, segment, rules, diagnostics);

            if (segment.Role == SegmentRole.Paragraph)
            {
                EvaluateParagraphLength(file, segment, sentences, rules, diagnostics);
            }
        }

        return diagnostics;
    }

    /// <summary>
    ///     Rule 4.1/8.4-8.7 (official, <see cref="Severity.Error"/>): flags any sentence exceeding
    ///     the mode-dependent word-count limit.
    /// </summary>
    private static void EvaluateWordLimit(
        string file,
        ProseSegment segment,
        IReadOnlyList<Sentence> sentences,
        int maxWords,
        LintMode mode,
        List<Diagnostic> diagnostics)
    {
        foreach (var sentence in sentences)
        {
            if (sentence.WordCount <= maxWords)
            {
                continue;
            }

            diagnostics.Add(new Diagnostic(
                file,
                segment.ResolveLine(sentence.StartOffset),
                null,
                "STE100-4.1",
                Severity.Error,
                $"Sentence has {sentence.WordCount} words, exceeding the {maxWords}-word limit for " +
                $"{mode.ToString().ToLowerInvariant()} mode: \"{Truncate(sentence.Text)}\"",
                "Split into shorter sentences."));
        }
    }

    /// <summary>
    ///     Rule 8.1 (official, <see cref="Severity.Error"/>): flags any semicolon in prose, unless
    ///     disabled via <see cref="RulesConfig.AllowSemicolons"/>. A semicolon that appears only
    ///     inside an inline code span is not flagged, since inline code content is excluded from
    ///     grammar-sensitive checks.
    /// </summary>
    private static void EvaluateSemicolons(string file, ProseSegment segment, RulesConfig rules, List<Diagnostic> diagnostics)
    {
        if (rules.AllowSemicolons)
        {
            return;
        }

        var codeSpans = MarkdownProseExtractor.FindInlineCodeSpans(segment.Text);
        for (var i = 0; i < segment.Text.Length; i++)
        {
            if (segment.Text[i] != ';' || MarkdownProseExtractor.OverlapsInlineCodeSpan(i, 1, codeSpans))
            {
                continue;
            }

            diagnostics.Add(new Diagnostic(
                file,
                segment.ResolveLine(i),
                null,
                "STE100-8.1",
                Severity.Error,
                "Semicolons are not permitted in ASD-STE100 prose.",
                "Split into two separate sentences."));
            return;
        }
    }

    /// <summary>
    ///     Rule 4.2 (official, <see cref="Severity.Error"/>): flags every contraction found in the
    ///     segment, unless disabled via <see cref="RulesConfig.AllowContractions"/>. A contraction
    ///     appearing only inside an inline code span is not flagged, since inline code content is
    ///     excluded from grammar-sensitive checks.
    /// </summary>
    private static void EvaluateContractions(string file, ProseSegment segment, RulesConfig rules, List<Diagnostic> diagnostics)
    {
        if (rules.AllowContractions)
        {
            return;
        }

        var codeSpans = MarkdownProseExtractor.FindInlineCodeSpans(segment.Text);
        foreach (Match match in ContractionRegex.Matches(segment.Text))
        {
            if (MarkdownProseExtractor.OverlapsInlineCodeSpan(match.Index, match.Length, codeSpans))
            {
                continue;
            }

            if (IsLikelyPossessive(match))
            {
                continue;
            }

            diagnostics.Add(new Diagnostic(
                file,
                segment.ResolveLine(match.Index),
                null,
                "STE100-4.2",
                Severity.Error,
                $"Contraction '{match.Value}' is not permitted in ASD-STE100 prose.",
                "Write the words in full."));
        }
    }

    /// <summary>
    ///     Determines whether a matched <c>word's</c> apostrophe should be treated as a possessive
    ///     (not flagged) rather than a contraction (flagged). Only the <c>'s</c> suffix is ambiguous;
    ///     every other contraction suffix (<c>'t</c>, <c>'re</c>, <c>'ve</c>, <c>'ll</c>, <c>'d</c>,
    ///     <c>'m</c>) is unambiguous and always flagged.
    /// </summary>
    private static bool IsLikelyPossessive(Match match)
    {
        if (!match.Groups[1].Value.Equals("s", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var word = match.Value[..match.Value.IndexOf('\'')];
        return !ApostropheSContractionWords.Contains(word);
    }

    /// <summary>
    ///     Advisory heuristic (not an official STE100 rule), <see cref="RulesConfig.PassiveVoice"/>
    ///     severity (default <see cref="Severity.Warn"/>): flags sentences matching a simple
    ///     "to be + past participle" pattern. Inline code span content is masked before testing, so
    ///     the passive-voice heuristic does not analyze inline-code content as prose grammar; the
    ///     diagnostic message still shows the sentence's verbatim text.
    /// </summary>
    private static void EvaluatePassiveVoice(
        string file,
        ProseSegment segment,
        IReadOnlyList<Sentence> sentences,
        RulesConfig rules,
        List<Diagnostic> diagnostics)
    {
        if (rules.PassiveVoice == Severity.Off)
        {
            return;
        }

        foreach (var sentence in sentences.Where(
                     sentence => PassiveVoiceRegex.IsMatch(MarkdownProseExtractor.MaskInlineCodeSpans(sentence.Text))))
        {
            diagnostics.Add(new Diagnostic(
                file,
                segment.ResolveLine(sentence.StartOffset),
                null,
                "STE100-ADV-PASSIVE",
                rules.PassiveVoice,
                $"Possible passive voice (advisory heuristic, not an official STE100 rule): \"{Truncate(sentence.Text)}\"",
                "Consider rewriting in active voice."));
        }
    }

    /// <summary>
    ///     Advisory heuristic (not an official STE100 rule), <see cref="RulesConfig.ComplexVerb"/>
    ///     severity (default <see cref="Severity.Warn"/>): flags sentences matching a perfect-tense
    ///     ("has/have/had [been] verb-ed/en") or modal-perfect-tense ("will/would/could/should/may/
    ///     might/must have verb-ed/en") pattern. Inline code span content is masked before testing,
    ///     so the heuristic does not analyze inline-code content as prose grammar. Evaluated before
    ///     <see cref="EvaluatePassiveVoice"/> so that "has/have/had been X" is owned by this check
    ///     only (see the precedence note on <see cref="PassiveVoiceRegex"/>).
    /// </summary>
    private static void EvaluateComplexVerb(
        string file,
        ProseSegment segment,
        IReadOnlyList<Sentence> sentences,
        RulesConfig rules,
        List<Diagnostic> diagnostics)
    {
        if (rules.ComplexVerb == Severity.Off)
        {
            return;
        }

        foreach (var sentence in sentences.Where(
                     sentence => ComplexVerbRegex.IsMatch(MarkdownProseExtractor.MaskInlineCodeSpans(sentence.Text))))
        {
            diagnostics.Add(new Diagnostic(
                file,
                segment.ResolveLine(sentence.StartOffset),
                null,
                "STE100-ADV-COMPLEXVERB",
                rules.ComplexVerb,
                "Possible complex verb construction (perfect/modal-perfect tense) — ASD-STE100 prefers simple " +
                $"present, past, or future tense verbs: \"{Truncate(sentence.Text)}\"",
                "Rewrite using a simple tense verb."));
        }
    }

    /// <summary>
    ///     Advisory heuristic (not an official STE100 rule), <see cref="RulesConfig.IngForm"/>
    ///     severity (default <see cref="Severity.Warn"/>): flags <c>-ing</c> words, since
    ///     ASD-STE100 restricts <c>-ing</c> forms to technical nouns/adjectives, not verb forms. A
    ///     match that touches a sentence-ending period immediately before or after (i.e. the
    ///     character immediately preceding or following the match is <c>.</c>) is skipped, since
    ///     such a match is unlikely to be a present-participle verb form embedded mid-sentence. A
    ///     match appearing only inside an inline code span is not flagged, since inline code
    ///     content is excluded from grammar-sensitive checks.
    /// </summary>
    private static void EvaluateIngForm(string file, ProseSegment segment, RulesConfig rules, List<Diagnostic> diagnostics)
    {
        if (rules.IngForm == Severity.Off)
        {
            return;
        }

        var codeSpans = MarkdownProseExtractor.FindInlineCodeSpans(segment.Text);
        foreach (Match match in IngFormRegex.Matches(segment.Text))
        {
            if (MarkdownProseExtractor.OverlapsInlineCodeSpan(match.Index, match.Length, codeSpans))
            {
                continue;
            }

            var precedingIndex = match.Index - 1;
            var followingIndex = match.Index + match.Length;
            if ((precedingIndex >= 0 && segment.Text[precedingIndex] == '.') ||
                (followingIndex < segment.Text.Length && segment.Text[followingIndex] == '.'))
            {
                continue;
            }

            diagnostics.Add(new Diagnostic(
                file,
                segment.ResolveLine(match.Index),
                null,
                "STE100-ADV-INGFORM",
                rules.IngForm,
                $"The '-ing' form '{match.Value}' may need review — ASD-STE100 restricts '-ing' forms to " +
                "technical nouns/adjectives, not verb forms.",
                "Confirm this is a technical noun or adjective, not a present-participle verb; otherwise " +
                "rewrite using a simple tense verb."));
        }
    }

    /// <summary>
    ///     Advisory heuristic (not an official STE100 rule), fixed <see cref="Severity.Warn"/>:
    ///     flags paragraphs exceeding <see cref="RulesConfig.MaxSentencesParagraph"/> sentences. A
    ///     value of 0 disables the check.
    /// </summary>
    private static void EvaluateParagraphLength(
        string file,
        ProseSegment segment,
        IReadOnlyList<Sentence> sentences,
        RulesConfig rules,
        List<Diagnostic> diagnostics)
    {
        if (rules.MaxSentencesParagraph <= 0 || sentences.Count <= rules.MaxSentencesParagraph)
        {
            return;
        }

        diagnostics.Add(new Diagnostic(
            file,
            segment.LineNumber,
            null,
            "STE100-ADV-PARA",
            Severity.Warn,
            $"Paragraph has {sentences.Count} sentences, exceeding the advisory limit of " +
            $"{rules.MaxSentencesParagraph} (not an official STE100 rule).",
            "Split into shorter paragraphs."));
    }

    /// <summary>
    ///     Truncates long sentence text for inclusion in a diagnostic message, so messages stay
    ///     readable in console/CI output.
    /// </summary>
    /// <param name="text">Text to truncate.</param>
    /// <returns><paramref name="text"/>, truncated to 80 characters with an ellipsis if longer.</returns>
    private static string Truncate(string text) => text.Length <= 80 ? text : string.Concat(text.AsSpan(0, 77), "...");
}
