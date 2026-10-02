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
    ///     Maximum number of words <see cref="IsDeclarativeSubjectContinuation"/> scans forward past
    ///     a captured imperative-lead verb while looking for a "to be" copula before giving up and
    ///     treating the sentence as a genuine imperative. A technical noun-phrase subject can
    ///     legitimately run to many words before its copula (for example "Open high pressure
    ///     hydraulic control system components are inspected monthly.", where the copula is the
    ///     seventh word after the lead verb), and the scan already stops as soon as it reaches a
    ///     <see cref="SubjectContinuationStopWords"/> entry or runs out of sentence text, so this
    ///     cap is not meant to limit realistic sentence/noun-phrase length - it is only a safety
    ///     valve bounding the scan against pathological/adversarial input.
    /// </summary>
    private const int MaxSubjectContinuationLookaheadWords = 60;

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
        new(@"\b(?<aux>is|are|was|were|be|being|(?<!(?:has|have|had)\s+)been)\s+(?<word>\w+(?:ed|en))\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout);

    /// <summary>
    ///     Common stative/adjectival past participles that, when following <c>is</c>/<c>are</c>, are
    ///     almost always a predicate adjective describing a current state (for example "the valve
    ///     is <c>closed</c>") rather than a true passive construction naming an action done to the
    ///     subject by an implied agent. <see cref="EvaluatePassiveVoice"/> does not flag these unless
    ///     an explicit "by &lt;agent&gt;" phrase immediately follows, since an explicit agent (for
    ///     example "is closed <c>by</c> the latch") makes the construction a genuine passive again.
    /// </summary>
    private static readonly HashSet<string> StativeParticiples = new(StringComparer.OrdinalIgnoreCase)
    {
        "energized", "de-energized", "deenergized", "powered", "unpowered", "closed", "open",
        "seated", "unseated", "filled", "initialized", "uninitialized", "connected",
        "disconnected", "unobstructed", "obstructed", "locked", "unlocked", "secured", "engaged",
        "disengaged", "enabled", "disabled", "grounded", "isolated", "pressurized",
        "depressurized", "charged", "discharged", "installed", "attached", "mounted",
    };

    /// <summary>
    ///     Common imperative-mood lead verbs for Procedure-mode instruction sentences (for example
    ///     "Keep the panel closed while it is energized."). When one of these starts a sentence,
    ///     <see cref="EvaluatePassiveVoice"/> treats a later "is"/"are" + past-participle match in
    ///     that same sentence as a predicate-adjective state the instruction asks the reader to
    ///     verify or maintain, not a passive construction, as long as no explicit "by &lt;agent&gt;"
    ///     phrase follows the participle. "do" is included here but is additionally constrained by
    ///     <see cref="IsImperativeLeadSentence"/> to only count as an imperative lead when
    ///     immediately followed by "not" - see that method's remarks for why ("do" alone is also
    ///     the yes/no-question auxiliary, e.g. "Do the covers remain closed...?").
    /// </summary>
    private static readonly HashSet<string> CommonImperativeLeadVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "keep", "confirm", "stop", "open", "close", "check", "verify", "ensure", "wait",
        "continue", "hold", "maintain", "leave", "make", "start", "do",
    };

    /// <summary>
    ///     "To be" auxiliary/copula forms used by <see cref="IsImperativeLeadSentence"/> to detect
    ///     when the word immediately following the captured lead-verb-plus-next-word span is a
    ///     main verb rather than part of an imperative verb phrase - see that method's remarks for
    ///     why this disambiguates a genuine imperative from a declarative sentence whose subject
    ///     noun phrase happens to start with the same word (for example "Open systems are used for
    ///     ventilation.").
    /// </summary>
    private static readonly HashSet<string> DeclarativeBeAuxiliaries = new(StringComparer.OrdinalIgnoreCase)
    {
        "is", "are", "was", "were", "be", "being", "been",
    };

    /// <summary>
    ///     Words that, if encountered while scanning forward from a captured lead verb (see
    ///     <see cref="IsImperativeLeadSentence"/>) before any <see cref="DeclarativeBeAuxiliaries"/>
    ///     entry is found, mean the scan has reached a determiner/complement boundary that starts
    ///     the imperative's own direct object or a subordinate clause (for example "the" in "Keep
    ///     the panel closed..." or "while" in "...while it is energized."), rather than continuing
    ///     a bare declarative subject noun phrase (for example "systems" in "Open systems are
    ///     used..."). Reaching one of these words therefore means the sentence is a genuine
    ///     imperative, not a declarative sentence.
    ///     </summary>
    ///     <remarks>
    ///     "and"/"or" are deliberately <em>not</em> included in this unconditional set, even though
    ///     they can also begin a subordinate/coordinate clause after an imperative's object (see
    ///     <see cref="CoordinatingConjunctionAfterCommaRegex"/>'s remarks). Unlike the words in this
    ///     set, "and"/"or" are also routinely used to coordinate two bare nouns within a single
    ///     declarative subject noun phrase (for example "systems and components" in "Open systems
    ///     and components are inspected."), with no determiner or other unambiguous boundary marker
    ///     in between. Treating them as an unconditional stop word would make the scan give up
    ///     immediately after "and"/"or" and misclassify the sentence as imperative before ever
    ///     reaching the real copula, wrongly exempting a genuine passive-voice finding. They are
    ///     instead handled by <see cref="CoordinatingConjunctionsWithinSubject"/>, which lets the
    ///     scan continue past them only when doing so cannot mask a second coordinated verb
    ///     phrase/clause - see that set's remarks.
    /// </remarks>
    private static readonly HashSet<string> SubjectContinuationStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "that", "which", "who", "whom", "whose", "while", "when", "if",
        "because", "before", "after", "since", "unless", "although", "but", "so",
    };

    /// <summary>
    ///     Coordinating conjunctions given special handling by
    ///     <see cref="IsDeclarativeSubjectContinuation"/> rather than being treated as an
    ///     immediate stop word (see <see cref="SubjectContinuationStopWords"/>'s remarks for why
    ///     they are excluded from that set). The word immediately following the conjunction decides
    ///     the outcome: (a) if it is itself a prenominal-adjective candidate (see
    ///     <see cref="AmbiguousAdjectivalLeadVerbs"/>), it is at least as likely to be heading the
    ///     first instruction's own coordinated object adjectivally (for example "open" in "Confirm
    ///     gauges and open valves are calibrated.", or "check" in "Open covers and check seals are
    ///     calibrated.") as starting a genuinely separate second instruction, so the scan
    ///     conservatively treats it as the former and remains imperative rather than risk losing a
    ///     real exemption; otherwise (b) if it is a recognized verb with no adjectival reading (see
    ///     <see cref="CommonImperativeLeadVerbs"/>), the conjunction joins the imperative's own
    ///     governed clause to a second, separately governed instruction (for example "and verify" in
    ///     "Confirm gauges and verify valves are calibrated."), so the scan reports a declarative
    ///     continuation to end the first instruction's exemption before that second clause's own
    ///     "is"/"are"; otherwise (c) the conjunction is read as coordinating two bare nouns, and
    ///     whether the scan may continue looking for a copula past them depends on whether the
    ///     sentence's own lead word is in <see cref="AmbiguousAdjectivalLeadVerbs"/> - see that
    ///     set's remarks.
    /// </summary>
    private static readonly HashSet<string> CoordinatingConjunctionsWithinSubject = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or",
    };

    /// <summary>
    ///     The subset of <see cref="CommonImperativeLeadVerbs"/> that are also routinely used as
    ///     prenominal adjectives describing equipment/system types in STE100 technical prose (for
    ///     example "open systems", "check valves"), rather than purely as action verbs. Used by
    ///     <see cref="IsDeclarativeSubjectContinuation"/> to decide whether a coordinated bare-noun
    ///     continuation past "and"/"or" (see <see cref="CoordinatingConjunctionsWithinSubject"/>'s
    ///     remarks) is plausible for a given sentence's lead word - restricting that continuation to
    ///     only these verbs avoids misreading a coordinated direct object of an ordinary action verb
    ///     (for example "gauges and valves" in "Confirm gauges and valves are calibrated.") as a
    ///     declarative subject.
    /// </summary>
    /// <remarks>
    ///     Known, accepted limitation: when both the sentence's lead word and the word immediately
    ///     following a coordinating conjunction are in this set, the heuristic cannot distinguish a
    ///     genuine imperative with a coordinated object (for example "Open covers and check seals
    ///     are calibrated.") from a genuine declarative sentence with a coordinated,
    ///     adjectivally-modified subject (for example "Open systems and open valves are
    ///     inspected.") - both have the identical "[lead] N1 and [ambiguous-word] N2 are participle"
    ///     shape. The heuristic conservatively treats this shape as imperative (remaining exempt) in
    ///     both cases, accepting a false negative on the declarative reading as the lesser cost -
    ///     see the design document's rationale for this advisory's general bias toward avoiding
    ///     false-positive noise.
    /// </remarks>
    private static readonly HashSet<string> AmbiguousAdjectivalLeadVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "open", "check",
    };

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
    ///     Common words that end in <c>-ing</c> but are never a present-participle verb form (they
    ///     are prepositions, pronouns, or plain nouns with no corresponding base verb), so
    ///     <see cref="EvaluateIngForm"/> excludes them unconditionally rather than relying on the
    ///     project-supplied allow list to cover every one of them. Deliberately excludes words
    ///     that have a genuine, if uncommon, present-participle verb use in technical writing
    ///     (for example "evening" in "evening out the load", or "ceiling" in "ceiling the price")
    ///     - those must instead rely on the project-supplied allow list, since unconditionally
    ///     excluding them here would cause the advisory to miss real occurrences.
    /// </summary>
    private static readonly HashSet<string> IngFormExclusions = new(StringComparer.OrdinalIgnoreCase)
    {
        "during", "morning", "something", "anything", "nothing", "everything",
        "spring", "king", "ring", "thing", "string",
    };

    /// <summary>
    ///     Evaluates every structural/mechanical rule and advisory heuristic against the prose
    ///     segments of a single Markdown file.
    /// </summary>
    /// <param name="file">File path used to populate <see cref="Diagnostic.File"/>.</param>
    /// <param name="segments">Prose segments produced by <see cref="MarkdownProseExtractor"/>.</param>
    /// <param name="mode">Resolved writing mode, determining the sentence word-count limit.</param>
    /// <param name="rules">Rule tuning from the effective <see cref="LintConfig"/>.</param>
    /// <param name="allowedTerms">
    ///     The file's resolved <see cref="LintConfig.ResolveAllowedTerms"/> vocabulary. A term a
    ///     project has approved via the dictionary allow/ignore lists is also excluded from the
    ///     <c>-ing</c>-form advisory (see <see cref="EvaluateIngForm"/>), so approving a word once
    ///     suppresses it from both the <c>-ing</c>-form advisory and <see cref="DictionaryChecker"/>.
    ///     This does not extend to the passive-voice or complex-verb advisories, since those match
    ///     whole-sentence tense patterns rather than individual terms. Pass
    ///     <see langword="null"/> or an empty collection when no per-file allowance applies.
    /// </param>
    /// <returns>All diagnostics produced for the file, in segment order.</returns>
    public static IReadOnlyList<Diagnostic> Evaluate(
        string file,
        IReadOnlyList<ProseSegment> segments,
        LintMode mode,
        RulesConfig rules,
        IReadOnlyCollection<string>? allowedTerms = null)
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
            EvaluateIngForm(file, segment, rules, allowedTerms, diagnostics);

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
                RuleCodes.SentenceWordLimit,
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
                RuleCodes.NoSemicolons,
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
                RuleCodes.NoContractions,
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
                     sentence => HasGenuinePassiveMatch(MarkdownProseExtractor.MaskInlineCodeSpans(sentence.Text))))
        {
            diagnostics.Add(new Diagnostic(
                file,
                segment.ResolveLine(sentence.StartOffset),
                null,
                RuleCodes.AdvisoryPassiveVoice,
                rules.PassiveVoice,
                $"Possible passive voice (advisory heuristic, not an official STE100 rule): \"{Truncate(sentence.Text)}\"",
                "Consider rewriting in active voice."));
        }
    }

    /// <summary>
    ///     Determines whether a sentence (masked text) contains at least one "to be" + past-
    ///     participle match that is a genuine passive construction rather than a predicate
    ///     adjective. A "is"/"are" + participle match is treated as adjectival, not passive - and
    ///     so does not count - when either (a) the participle is in <see cref="StativeParticiples"/>
    ///     (a common state-describing word, e.g. "closed", "energized"), or (b) the match falls
    ///     within the clause governed by a Procedure-style imperative lead (the sentence starts with
    ///     a word in <see cref="CommonImperativeLeadVerbs"/>, e.g. "Keep", "Confirm") so that
    ///     clause's "is"/"are" reads as a state the instruction asks the reader to verify or
    ///     maintain, not a passive construction - see <see cref="ImperativeLeadClauseEnd"/> for how
    ///     the governed clause's boundary is determined, so that a later, independent clause after a
    ///     bare semicolon or a coordinating conjunction (for example the second clause in "Keep the
    ///     cover closed; the report is reviewed weekly." or the "but"-clause in "Keep the cover
    ///     closed, but the report is reviewed weekly.") is still evaluated as an ordinary,
    ///     non-exempted passive construction; either exemption is itself overridden - the match
    ///     still counts as passive -
    ///     when an explicit "by &lt;agent&gt;" phrase immediately follows the participle, since
    ///     naming the agent makes the construction unambiguously passive again. "was"/"were"/
    ///     "be"/"being"/"been" matches are never exempted by either rule, since the
    ///     imperative/stative readings above only apply to present-tense "is"/"are".
    /// </summary>
    private static bool HasGenuinePassiveMatch(string sentenceText)
    {
        var isImperativeLead = IsImperativeLeadSentence(sentenceText);
        var imperativeLeadClauseEnd = isImperativeLead ? ImperativeLeadClauseEnd(sentenceText) : 0;

        foreach (Match match in PassiveVoiceRegex.Matches(sentenceText))
        {
            var aux = match.Groups["aux"].Value;
            var participle = match.Groups["word"].Value;
            var isPresentTenseAux =
                aux.Equals("is", StringComparison.OrdinalIgnoreCase) ||
                aux.Equals("are", StringComparison.OrdinalIgnoreCase);
            var isWithinImperativeLeadClause = isImperativeLead && match.Index < imperativeLeadClauseEnd;

            if (isPresentTenseAux &&
                (StativeParticiples.Contains(participle) || isWithinImperativeLeadClause) &&
                !HasAgentPhraseAfter(sentenceText, match.Index + match.Length))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    ///     Determines whether a sentence opens with (after optionally skipping a Markdown list-item
    ///     marker or leading ordinal) a word in <see cref="CommonImperativeLeadVerbs"/>, indicating
    ///     an imperative-mood instruction sentence (for example "Keep the panel closed while it is
    ///     energized." or "Confirm the valve is closed...").
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     "do" is special-cased: unlike the other entries in <see cref="CommonImperativeLeadVerbs"/>,
    ///     "do" is not an unambiguous imperative lead on its own, because it is also the auxiliary
    ///     that opens a yes/no question (for example "Do the covers remain closed while they are
    ///     inspected?"). Treating every sentence-initial "do" as imperative would wrongly exempt
    ///     that question's genuine passive/state construction. "do" therefore only counts as an
    ///     imperative lead when immediately followed by "not" (for example "Do not open the cover
    ///     while it is energized."), which is unambiguously the negative-imperative construction and
    ///     never the start of a question. "do" remains in <see cref="CommonImperativeLeadVerbs"/>
    ///     itself (rather than being removed) because other logic/doc comments in this class
    ///     reference that set as the full list of recognized imperative leads; this method is the
    ///     sole place the extra condition is enforced.
    ///     </para>
    ///     <para>
    ///     A lead word in <see cref="CommonImperativeLeadVerbs"/> is also ambiguous with a
    ///     declarative sentence whose subject noun phrase happens to be headed by that same word
    ///     used adjectivally (for example "Open systems are used for ventilation.", "Check valves
    ///     are inspected monthly.", or "Open system components are inspected monthly."), where the
    ///     real main verb is a "to be" copula some number of words in, not the lead word itself. To
    ///     disambiguate, <see cref="IsDeclarativeSubjectContinuation"/> scans forward word-by-word
    ///     after the lead verb: if a <see cref="DeclarativeBeAuxiliaries"/> entry (the copula) is
    ///     reached before any <see cref="SubjectContinuationStopWords"/> entry, the lead word and
    ///     the scanned words are read as the declarative subject noun phrase rather than an
    ///     imperative verb + its object, so the sentence is not treated as an imperative lead. A
    ///     genuine imperative's object/complement begins with an ordinary determiner or triggers a
    ///     subordinate/coordinate clause first (for example "the" in "Keep the panel closed..." or
    ///     "while" in "...while it is energized."), so this check does not affect those cases.
    ///     </para>
    ///     <para>
    ///     The lead-verb capture also rejects a match that is immediately followed by a hyphen (no
    ///     intervening space), since that means the matched letters are only a prefix of a
    ///     hyphenated compound word (for example "Open" in "Open-loop systems are inspected
    ///     monthly."), which is an adjective, not the standalone lead verb "Open".
    ///     </para>
    /// </remarks>
    private static bool IsImperativeLeadSentence(string sentenceText)
    {
        var match = Regex.Match(
            sentenceText,
            @"^\s*(?:[-*+]\s+|\d+[.)]\s+)?(?<verb>[A-Za-z]+)(?!-)(?:\s+(?<next>[A-Za-z]+))?",
            RegexOptions.None,
            RegexTimeout);

        if (!match.Success || !CommonImperativeLeadVerbs.Contains(match.Groups["verb"].Value))
        {
            return false;
        }

        // "do" alone is ambiguous with a yes/no question's auxiliary "do" (see remarks); only
        // "do not" is unambiguously imperative.
        if (match.Groups["verb"].Value.Equals("do", StringComparison.OrdinalIgnoreCase))
        {
            return match.Groups["next"].Success
                && match.Groups["next"].Value.Equals("not", StringComparison.OrdinalIgnoreCase);
        }

        // Slice from the end of the captured "verb" group only, not match.Length - the regex also
        // captures an optional "next" word (used only by the "do not" check above), and including
        // it here would skip that word entirely, hiding a determiner/stop-word it contains (e.g.
        // "the" in "Confirm the gasket is inspected.") from the subject-continuation scan.
        var verbGroup = match.Groups["verb"];
        return !IsDeclarativeSubjectContinuation(
            sentenceText[(verbGroup.Index + verbGroup.Length)..], verbGroup.Value);
    }

    /// <summary>
    ///     Scans forward word-by-word through <paramref name="textAfterLeadVerb"/> (the sentence
    ///     text immediately after a captured <see cref="CommonImperativeLeadVerbs"/> lead word,
    ///     <paramref name="leadVerb"/>), bounded to <see cref="MaxSubjectContinuationLookaheadWords"/>
    ///     words, to determine whether the lead word is actually heading a declarative subject noun
    ///     phrase rather than being an imperative verb - see <see cref="IsImperativeLeadSentence"/>'s
    ///     remarks. Returns <see langword="true"/> (declarative) as soon as a
    ///     <see cref="DeclarativeBeAuxiliaries"/> entry is found; returns <see langword="false"/>
    ///     (imperative) as soon as a <see cref="SubjectContinuationStopWords"/> entry is found, or if
    ///     the lookahead is exhausted (words run out or the cap is reached) without finding either.
    ///     A <see cref="CoordinatingConjunctionsWithinSubject"/> entry (for example "and") is handled
    ///     separately: if the following word is in <see cref="AmbiguousAdjectivalLeadVerbs"/>, this
    ///     returns <see langword="false"/> (remains imperative) rather than risk losing a real
    ///     exemption; otherwise, if it is a recognized <see cref="CommonImperativeLeadVerbs"/> verb,
    ///     this returns <see langword="true"/> immediately (the conjunction starts a second,
    ///     separately governed instruction); otherwise the scan continues past the coordinated bare
    ///     noun only when <paramref name="leadVerb"/> is in
    ///     <see cref="AmbiguousAdjectivalLeadVerbs"/>, and stops (returning <see langword="false"/>)
    ///     otherwise - see those sets' remarks for why.
    /// </summary>
    private static bool IsDeclarativeSubjectContinuation(string textAfterLeadVerb, string leadVerb)
    {
        var remaining = textAfterLeadVerb;
        var allowCoordinatedSubjectContinuation = AmbiguousAdjectivalLeadVerbs.Contains(leadVerb);

        for (var i = 0; i < MaxSubjectContinuationLookaheadWords; i++)
        {
            var wordMatch = Regex.Match(remaining, @"^\s*(?<word>[A-Za-z]+)", RegexOptions.None, RegexTimeout);
            if (!wordMatch.Success)
            {
                return false;
            }

            var word = wordMatch.Groups["word"].Value;

            if (DeclarativeBeAuxiliaries.Contains(word))
            {
                return true;
            }

            if (SubjectContinuationStopWords.Contains(word))
            {
                return false;
            }

            if (CoordinatingConjunctionsWithinSubject.Contains(word))
            {
                remaining = remaining[(wordMatch.Index + wordMatch.Length)..];

                var nextWordMatch =
                    Regex.Match(remaining, @"^\s*(?<word>[A-Za-z]+)", RegexOptions.None, RegexTimeout);
                var nextWord = nextWordMatch.Success ? nextWordMatch.Groups["word"].Value : null;
                if (nextWord != null && AmbiguousAdjectivalLeadVerbs.Contains(nextWord))
                {
                    // The word coordinated by "and"/"or" is itself a prenominal-adjective
                    // candidate (for example "open" in "Confirm gauges and open valves are
                    // calibrated.", or "check" in "Open covers and check seals are calibrated."),
                    // so it is at least as likely to be heading the first instruction's own
                    // coordinated object adjectivally as starting a genuinely separate second
                    // instruction. Conservatively treat it as the former (remain imperative)
                    // rather than risk losing the exemption on a real coordinated-object match.
                    return false;
                }

                if (nextWord != null && CommonImperativeLeadVerbs.Contains(nextWord))
                {
                    // The word coordinated by "and"/"or" is a recognized verb with no
                    // adjectival reading, so it cannot be an adjective modifying the next noun -
                    // this is a second coordinated instruction/clause with its own, separately
                    // governed copula, so the imperative-lead exemption must not extend across it.
                    return true;
                }

                if (!allowCoordinatedSubjectContinuation)
                {
                    // A bare noun coordinated by "and"/"or" after a non-adjectival lead verb is
                    // read as (another part of) that verb's own direct object, not a declarative
                    // subject - stop here and remain imperative.
                    return false;
                }

                continue;
            }

            remaining = remaining[(wordMatch.Index + wordMatch.Length)..];
        }

        return false;
    }

    /// <summary>
    ///     Matches a comma followed by a coordinating conjunction ("but"/"and"/"or"/"so"), used by
    ///     <see cref="ImperativeLeadClauseEnd"/> (together with a bare semicolon boundary - see that
    ///     method's remarks) to find where an independent clause begins after an imperative-lead
    ///     clause. Deliberately only matches these four coordinating conjunctions, not subordinating
    ///     conjunctions such as "while"/"when"/"if"/"because": a subordinating conjunction
    ///     introduces a dependent clause that is still part of the same imperative instruction (for
    ///     example the "while"-clause in "Keep the panel closed while it is energized."), whereas a
    ///     coordinating conjunction after a comma joins a separate, independent clause that the
    ///     imperative no longer governs (for example the "but"-clause in "Keep the cover closed, but
    ///     the report is reviewed weekly.").
    /// </summary>
    private static readonly Regex CoordinatingConjunctionAfterCommaRegex =
        new(@",\s*(?:but|and|or|so)\b", RegexOptions.IgnoreCase, RegexTimeout);

    /// <summary>
    ///     Determines the end boundary (exclusive character index) of the clause governed by an
    ///     imperative-lead sentence's leading instruction, for use by
    ///     <see cref="HasGenuinePassiveMatch"/> to limit its imperative-lead passive-voice exemption
    ///     to that clause rather than applying it to the whole sentence. The governed clause ends at
    ///     whichever comes first: a bare semicolon, or a comma immediately followed by a
    ///     coordinating conjunction ("but"/"and"/"or"/"so") - see
    ///     <see cref="CoordinatingConjunctionAfterCommaRegex"/>; when neither boundary exists, the
    ///     governed clause is the entire sentence.
    /// </summary>
    /// <remarks>
    ///     A semicolon is always treated as an independent-clause boundary, with no "governed"
    ///     exception analogous to a subordinating conjunction: unlike "while"/"when"/"if"/"because",
    ///     which can introduce a dependent clause the imperative still governs, there is no
    ///     subordinating use of a semicolon in English - a semicolon always joins two independent
    ///     clauses (see <see cref="SentenceAnalyzer"/>, which deliberately keeps semicolon-separated
    ///     clauses within one sentence rather than splitting on them). Without this boundary, the
    ///     imperative-lead exemption would wrongly extend across the semicolon and exempt a genuine
    ///     passive construction in the second, unrelated independent clause (for example "the report
    ///     is reviewed weekly" in "Keep the cover closed; the report is reviewed weekly.").
    /// </remarks>
    private static int ImperativeLeadClauseEnd(string sentenceText)
    {
        var semicolonIndex = sentenceText.IndexOf(';');
        var conjunctionMatch = CoordinatingConjunctionAfterCommaRegex.Match(sentenceText);

        var boundary = sentenceText.Length;
        if (semicolonIndex >= 0)
        {
            boundary = Math.Min(boundary, semicolonIndex);
        }

        if (conjunctionMatch.Success)
        {
            boundary = Math.Min(boundary, conjunctionMatch.Index);
        }

        return boundary;
    }

    /// <summary>
    ///     Determines whether the first word following <paramref name="index"/> in
    ///     <paramref name="text"/> is "by", indicating an explicit agent phrase (for example "is
    ///     closed <c>by</c> the latch") that makes a "to be" + participle match an unambiguous
    ///     passive construction regardless of any adjectival/imperative exemption.
    /// </summary>
    private static bool HasAgentPhraseAfter(string text, int index)
    {
        var match = Regex.Match(
            text[index..],
            @"^\s*(?<word>[A-Za-z]+)",
            RegexOptions.None,
            RegexTimeout);

        return match.Success && match.Groups["word"].Value.Equals("by", StringComparison.OrdinalIgnoreCase);
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
                RuleCodes.AdvisoryComplexVerb,
                rules.ComplexVerb,
                "Possible complex verb construction (perfect/modal-perfect tense) — ASD-STE100 prefers simple " +
                $"present, past, or future tense verbs: \"{Truncate(sentence.Text)}\"",
                "Rewrite using a simple tense verb."));
        }
    }

    /// <summary>
    ///     Advisory heuristic (not an official STE100 rule), <see cref="RulesConfig.IngForm"/>
    ///     severity (default <see cref="Severity.Warn"/>): flags <c>-ing</c> words that
    ///     <see cref="PartOfSpeechGuesser.GuessIngFormRole"/> resolves as a genuine present-
    ///     participle verb use, since ASD-STE100 restricts <c>-ing</c> forms to technical
    ///     nouns/adjectives, not verb forms; a gerund/participial-adjective use (for example a
    ///     noun-phrase subject, object of a preposition, list item, or noun modifier) is not
    ///     flagged. A match appearing only inside an inline code span, an admonition label (for
    ///     example <c>**Caution.**</c>), or a quoted/emphasized span (a cited title or mention) is
    ///     also skipped, as is any match in a <see cref="SegmentRole.Heading"/> or
    ///     <see cref="SegmentRole.TableHeader"/> segment - headings and table column headers are
    ///     short labels, not prose sentences. A match whose exact word is either in
    ///     <see cref="IngFormExclusions"/> (never a verb form, e.g. "during") or in
    ///     <paramref name="allowedTerms"/> (a project-approved dictionary term, e.g. "metering") is
    ///     also skipped, so approving a term once suppresses it from this advisory too, not only
    ///     <see cref="DictionaryChecker"/>.
    /// </summary>
    private static void EvaluateIngForm(
        string file,
        ProseSegment segment,
        RulesConfig rules,
        IReadOnlyCollection<string>? allowedTerms,
        List<Diagnostic> diagnostics)
    {
        if (rules.IngForm == Severity.Off || segment.Role is SegmentRole.Heading or SegmentRole.TableHeader)
        {
            return;
        }

        var codeSpans = MarkdownProseExtractor.FindInlineCodeSpans(segment.Text);
        var admonitionSpans = MarkdownProseExtractor.FindAdmonitionLabelSpans(segment.Text);
        var quotedSpans = MarkdownProseExtractor.FindQuotedOrEmphasisSpans(segment.Text);

        foreach (Match match in IngFormRegex.Matches(segment.Text))
        {
            if (MarkdownProseExtractor.OverlapsInlineCodeSpan(match.Index, match.Length, codeSpans)
                || MarkdownProseExtractor.OverlapsInlineCodeSpan(match.Index, match.Length, admonitionSpans)
                || MarkdownProseExtractor.OverlapsInlineCodeSpan(match.Index, match.Length, quotedSpans))
            {
                continue;
            }

            if (IngFormExclusions.Contains(match.Value) ||
                (allowedTerms is { Count: > 0 } && allowedTerms.Contains(match.Value)))
            {
                continue;
            }

            if (PartOfSpeechGuesser.GuessIngFormRole(segment.Text, match.Index, match.Length) != PartOfSpeech.Verb)
            {
                continue;
            }

            diagnostics.Add(new Diagnostic(
                file,
                segment.ResolveLine(match.Index),
                null,
                RuleCodes.AdvisoryIngForm,
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
            RuleCodes.AdvisoryParagraphLength,
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
