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
///     Best-effort, deterministic, regex/rule-based heuristic that guesses whether a matched
///     dictionary term is being used as a noun or a verb at a specific point in prose, so that
///     <see cref="DictionaryChecker"/> can select the correct POS-tagged sense's alternative(s).
/// </summary>
/// <remarks>
///     <para>
///     <b>This is not a grammatical guarantee.</b> It is a lightweight signal-counting heuristic,
///     consistent with this codebase's dependency-light approach (compare
///     <see cref="SentenceAnalyzer"/>'s own regex-based, non-NLP sentence splitting). It only ever
///     returns <see cref="PartOfSpeech.Noun"/>, <see cref="PartOfSpeech.Verb"/>, or
///     <see langword="null"/> (inconclusive) - it does not attempt to positively detect
///     <see cref="PartOfSpeech.Adjective"/> or <see cref="PartOfSpeech.Adverb"/>, because no
///     reliable lightweight signal for those roles exists; entries with only adjective/adverb
///     senses always resolve as ambiguous via the "no signals fired" path, which is the correct
///     conservative behavior for those cases.
///     </para>
///     <para>
///     Multi-word terms (for example "in order to") are expected to be single-sense
///     <c>pos: any</c> entries, since no POS distinction is detectable or needed for connector
///     phrases. This heuristic is only meaningfully exercised for single-word multi-sense terms;
///     a multi-word multi-sense entry will simply tend to resolve as ambiguous, which is safe.
///     </para>
/// </remarks>
internal static class PartOfSpeechGuesser
{
    /// <summary>Regex timeout applied to every pattern used by this class.</summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Same sentence-boundary concept as <see cref="SentenceAnalyzer"/>'s own
    ///     <c>SentenceSplitRegex</c>, narrowly scoped here to only answer "does a new sentence
    ///     start immediately after this point in the text".
    /// </summary>
    private static readonly Regex SentenceBoundaryRegex =
        new(@"[.!?:]\s+$", RegexOptions.Compiled, RegexTimeout);

    /// <summary>Matches one leading run of non-word characters, used to strip punctuation.</summary>
    private static readonly Regex LeadingPunctuationRegex =
        new(@"^[^\w]+", RegexOptions.Compiled, RegexTimeout);

    /// <summary>Matches one trailing run of non-word characters, used to strip punctuation.</summary>
    private static readonly Regex TrailingPunctuationRegex =
        new(@"[^\w]+$", RegexOptions.Compiled, RegexTimeout);

    /// <summary>Modal auxiliary verbs signaling an upcoming verb.</summary>
    private static readonly HashSet<string> ModalAuxiliaries =
        new(StringComparer.OrdinalIgnoreCase) { "can", "could", "must", "will", "would", "should", "shall", "may", "might" };

    /// <summary>Forms of "to be" signaling a following progressive verb.</summary>
    private static readonly HashSet<string> BeAuxiliaries =
        new(StringComparer.OrdinalIgnoreCase) { "is", "are", "was", "were", "be", "been", "being" };

    /// <summary>Articles signaling an upcoming noun.</summary>
    private static readonly HashSet<string> Articles =
        new(StringComparer.OrdinalIgnoreCase) { "a", "an", "the" };

    /// <summary>Possessive pronouns signaling an upcoming noun.</summary>
    private static readonly HashSet<string> PossessivePronouns =
        new(StringComparer.OrdinalIgnoreCase) { "my", "your", "his", "her", "its", "our", "their" };

    /// <summary>Quantifiers/demonstratives signaling an upcoming noun.</summary>
    private static readonly HashSet<string> QuantifiersOrDemonstratives =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "this", "these", "that", "those", "each", "every", "some", "any", "all", "no", "both"
        };

    /// <summary>Prepositions signaling an upcoming noun.</summary>
    private static readonly HashSet<string> Prepositions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "of", "in", "on", "for", "with", "by", "from", "about", "at", "into", "onto", "through", "during",
            "after", "before", "without", "upon", "against", "between", "among", "over", "under", "above",
            "below", "near"
        };

    /// <summary>
    ///     Personal/object pronouns: never the head noun of a noun-noun compound (for example the
    ///     "it" in "...closing it fully" is a pronoun object, not a compound-noun head).
    /// </summary>
    private static readonly HashSet<string> ObjectPronouns =
        new(StringComparer.OrdinalIgnoreCase)
        { "it", "them", "him", "her", "us", "me", "you", "they", "we", "he", "she", "one" };

    /// <summary>
    ///     "do"/"have"-family auxiliary verb forms. Combined with <see cref="ModalAuxiliaries"/> and
    ///     <see cref="BeAuxiliaries"/>, used to exclude the matched word itself from
    ///     <see cref="HasNounSignal"/>'s "FollowedByNegation" signal: an auxiliary verb immediately
    ///     followed by "not"/"never" (for example "shall <b>not</b>", "does <b>not</b>", "has
    ///     <b>never</b>") is unambiguously still a verb use, not a noun, so the negation signal must
    ///     not fire for the auxiliary word itself - only for an ordinary word in that position (for
    ///     example the noun "Trigger" in "Trigger not issued").
    /// </summary>
    private static readonly HashSet<string> OtherAuxiliaryVerbs =
        new(StringComparer.OrdinalIgnoreCase) { "do", "does", "did", "has", "have", "had" };

    /// <summary>
    ///     Common, unambiguous technical-writing plural nouns that are safe to recognize as the
    ///     head of a noun-noun compound (for example "units" in "calibration units") even though
    ///     their spelling also ends in a bare "-s", which is otherwise deliberately excluded by
    ///     <see cref="LooksLikeCompoundNoun"/> as too ambiguous with 3rd-person-singular verb
    ///     forms (see that method's remarks). This is a closed, curated allow-list rather than a
    ///     blanket relaxation, to avoid reintroducing that ambiguity. A second exclusion criterion
    ///     applies on top of that general "-s" ambiguity, mirroring the rationale already
    ///     documented on <see cref="FiniteVerbForms"/>: this list also deliberately omits any word
    ///     that is also a common, ordinary finite verb in technical/procedural English (for
    ///     example "blocks", "channels", "levels", "limits", "stages", "phases", "ports",
    ///     "frames", "records", "pumps", "gauges", "displays", "switches", "relays", "cycles") -
    ///     including one of those would let the strong noun-compound signal wrongly out-rank a
    ///     genuine imperative/finite-verb use of the preceding match (for example misreading the
    ///     imperative "Test" as a noun in "Test blocks daily.").
    /// </summary>
    private static readonly HashSet<string> CompoundNounHeadAllowList =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "units", "modules", "sensors", "components", "parameters", "thresholds", "intervals",
            "nodes", "cells", "entries", "items", "elements", "devices", "assemblies", "fittings",
            "connectors", "terminals", "circuits", "valves", "motors", "panels", "actuators"
        };

    /// <summary>
    ///     Closed-class, third-person-singular-present finite verb forms that signal the
    ///     preceding match is the subject of a clause (for example "probe moves", "probe has").
    ///     Every entry necessarily ends in "-s" (that is how this verb form is spelled), but the
    ///     list deliberately omits any such word that is also a common technical-writing plural
    ///     noun (for example "results", "checks", "tests", "supports", "measures", "monitors",
    ///     "returns", "changes", "turns", "sets", "reports", "triggers", "causes", "means",
    ///     "remains", "increases", "decreases", "moves", "runs", "starts", "stops", "shows",
    ///     "displays", "controls", "reads", "writes") - including one of those would let a
    ///     following plural-noun object make the guesser misread the preceding match as a noun
    ///     subject instead of, for example, the imperative verb of a procedure step (see
    ///     <see cref="HasNounSignal"/>'s separate <c>PluralNounSuffix</c> signal, which already
    ///     covers plural nouns on their own terms).
    /// </summary>
    private static readonly HashSet<string> FiniteVerbForms =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "has", "does", "goes", "opens", "closes",
            "operates", "requires", "indicates", "connects",
            "occurs", "applies", "appears", "provides", "allows", "produces", "contains",
            "includes", "represents", "reduces", "affects", "detects", "enables", "disables",
            "activates", "sends", "receives", "verifies", "confirms",
            "adjusts", "prevents", "generates"
        };

    /// <summary>Words that end a determiner's reach through modifiers (conjunctions, clause markers).</summary>
    private static readonly HashSet<string> ClauseBreakingWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "and", "or", "but", "so", "because", "if", "when", "while", "that", "which", "who",
            "than", "as"
        };

    /// <summary>
    ///     Closed-class adjectives commonly used as noun-phrase modifiers in technical writing
    ///     that do not carry a recognizable participle suffix (compare <c>metering</c>/<c>coated</c>,
    ///     detected separately by their <c>-ing</c>/<c>-ed</c> ending).
    /// </summary>
    private static readonly HashSet<string> CommonAdjectiveModifiers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "custom", "manual", "automatic", "primary", "secondary", "standard", "digital",
            "analog", "external", "internal", "optional", "additional", "main", "backup",
            "electrical", "mechanical", "electronic", "hydraulic", "pneumatic", "thermal",
            "structural", "operational", "functional", "physical", "remote", "local",
            "upper", "lower", "front", "rear", "single", "dual", "final", "initial",
            "current", "previous", "required", "recommended"
        };

    /// <summary>Negation/adverbial words that cannot be the head noun of a compound.</summary>
    private static readonly HashSet<string> NonCompoundHeadWords =
        new(StringComparer.OrdinalIgnoreCase) { "not", "never", "also", "already", "still", "just", "often" };

    /// <summary>Maximum number of modifier tokens to scan past when looking for a governing determiner.</summary>
    private const int MaxModifierScanDistance = 3;

    /// <summary>
    ///     Guesses the grammatical role of one dictionary-term match within its surrounding
    ///     segment text.
    /// </summary>
    /// <param name="segmentText">Full prose text of the segment containing the match.</param>
    /// <param name="matchIndex">0-based character offset of the match within <paramref name="segmentText"/>.</param>
    /// <param name="matchLength">Length of the matched span.</param>
    /// <param name="mode">
    ///     The file's resolved <see cref="LintMode"/>; used only for the imperative-sentence-start
    ///     signal, which applies exclusively in <see cref="LintMode.Procedure"/> writing.
    /// </param>
    /// <returns>
    ///     <see cref="PartOfSpeech.Noun"/> or <see cref="PartOfSpeech.Verb"/> when exactly one
    ///     category of signal fired; <see langword="null"/> when signals conflict (both fired) or
    ///     none fired.
    /// </returns>
    public static PartOfSpeech? Guess(string segmentText, int matchIndex, int matchLength, LintMode mode)
    {
        var signals = ComputeSignals(segmentText, matchIndex, matchLength, mode);

        var hasVerb = signals.WeakVerb || signals.StrongVerb;
        var hasNoun = signals.StrongNoun || signals.WeakNoun;

        if (hasVerb && !hasNoun)
        {
            return PartOfSpeech.Verb;
        }

        if (hasNoun && !hasVerb)
        {
            return PartOfSpeech.Noun;
        }

        // Conflicting (both fired) or absent (neither fired): ambiguous.
        return null;
    }

    /// <summary>
    ///     An additive refinement of <see cref="Guess"/> that only ever narrows an otherwise
    ///     ambiguous (<see langword="null"/>) result: when <see cref="Guess"/> itself resolves a
    ///     role, that result is returned unchanged. Otherwise, this promotes to a role when exactly
    ///     one of the two <i>strong</i> signal categories (<see cref="GuessSignals.StrongVerb"/>,
    ///     <see cref="GuessSignals.StrongNoun"/>) fired, ignoring the two weaker, more easily
    ///     out-voted categories (<see cref="GuessSignals.WeakVerb"/>, the mode-dependent
    ///     imperative-sentence-start signal, and <see cref="GuessSignals.WeakNoun"/>, the plural-
    ///     suffix/verbless-segment signals) that would otherwise have produced the conflict.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Use this overload (rather than <see cref="Guess"/>) wherever a confident role is
    ///     actionable even from a single strong signal category alone, for example
    ///     <see cref="DictionaryChecker"/>'s per-occurrence sense selection.
    ///     </para>
    ///     <para>
    ///     <b>NounCompoundModifier-only precedence exception:</b> the
    ///     <see cref="GuessSignals.StrongNoun"/>-wins tie-break below is deliberately narrowed by
    ///     one case: when the weak, mode-dependent imperative-sentence-start signal
    ///     (<see cref="GuessSignals.WeakVerb"/>) fired, and the <i>only</i> reason
    ///     <see cref="GuessSignals.StrongNoun"/> fired is the <c>NounCompoundModifier</c> signal
    ///     (a following word that merely looks like a plural technical noun - see
    ///     <see cref="GuessSignals.StrongNounExcludingCompoundModifier"/>), the conflict is left
    ///     inconclusive instead of resolving to <see cref="PartOfSpeech.Noun"/>. This is because a
    ///     word that looks like a plural noun immediately after a sentence-initial word is just as
    ///     consistent with "imperative verb + plural direct object" (for example "Check modules")
    ///     as it is with "noun-compound modifier + noun head" (for example "check valve" as a
    ///     two-word noun) - unlike the other <see cref="GuessSignals.StrongNoun"/> sources
    ///     (article, possessive, preposition, etc.), which are never consistent with an imperative
    ///     reading of the preceding word and so must continue to override the weak imperative
    ///     signal exactly as before.
    ///     </para>
    /// </remarks>
    public static PartOfSpeech? GuessEffective(string segmentText, int matchIndex, int matchLength, LintMode mode)
    {
        var signals = ComputeSignals(segmentText, matchIndex, matchLength, mode);

        var hasVerb = signals.WeakVerb || signals.StrongVerb;
        var hasNoun = signals.StrongNoun || signals.WeakNoun;

        if (hasVerb && !hasNoun)
        {
            return PartOfSpeech.Verb;
        }

        if (hasNoun && !hasVerb)
        {
            return PartOfSpeech.Noun;
        }

        if (signals.StrongVerb && !signals.StrongNoun)
        {
            return PartOfSpeech.Verb;
        }

        if (signals.StrongNoun && !signals.StrongVerb)
        {
            // NounCompoundModifier-only exception (see remarks above): a weak imperative lead is
            // just as consistent with "imperative + plural object" as with "noun-compound
            // modifier", so do not let that single ambiguous signal override it.
            if (signals.WeakVerb && !signals.StrongNounExcludingCompoundModifier)
            {
                return null;
            }

            return PartOfSpeech.Noun;
        }

        return null;
    }

    /// <summary>
    ///     The individual, un-aggregated signal categories computed for one match, split into
    ///     <i>strong</i> (specific, local, hard-to-confuse evidence) and <i>weak</i> (broad,
    ///     easily out-voted fallback evidence) tiers for each part of speech. See
    ///     <see cref="Guess"/> (which treats all four as a simple two-way OR) and
    ///     <see cref="GuessEffective"/> (which can additionally resolve on strong-only evidence).
    /// </summary>
    /// <param name="StrongVerb">See <see cref="HasOtherVerbSignal"/>.</param>
    /// <param name="StrongNoun">
    ///     The full strong-noun evidence, including the <c>NounCompoundModifier</c> signal (see
    ///     <see cref="HasNounSignal"/>).
    /// </param>
    /// <param name="StrongNounExcludingCompoundModifier">
    ///     The same strong-noun evidence as <paramref name="StrongNoun"/>, but with the
    ///     <c>NounCompoundModifier</c> signal excluded. <see cref="GuessEffective"/> uses this to
    ///     detect when <paramref name="StrongNoun"/>'s only contributing signal was
    ///     <c>NounCompoundModifier</c>, which is the one strong-noun source genuinely ambiguous
    ///     with an imperative-sentence-start reading (see <see cref="GuessEffective"/>'s remarks).
    /// </param>
    /// <param name="WeakVerb">The mode-dependent imperative-sentence-start signal.</param>
    /// <param name="WeakNoun">The plural-suffix/verbless-segment fallback signals.</param>
    private readonly record struct GuessSignals(
        bool StrongVerb,
        bool StrongNoun,
        bool StrongNounExcludingCompoundModifier,
        bool WeakVerb,
        bool WeakNoun);

    /// <summary>Computes every individual signal category for one match (see <see cref="GuessSignals"/>).</summary>
    private static GuessSignals ComputeSignals(string segmentText, int matchIndex, int matchLength, LintMode mode)
    {
        ArgumentNullException.ThrowIfNull(segmentText);

        var matchText = segmentText.Substring(matchIndex, matchLength);
        var precedingWord = PrecedingWord(segmentText, matchIndex);
        var governingWord = GoverningDeterminer(segmentText, matchIndex);
        var followingWords = FollowingWords(segmentText, matchIndex + matchLength, 2);
        var followingWord = followingWords.Length > 0 ? followingWords[0] : null;
        var secondFollowingWord = followingWords.Length > 1 ? followingWords[1] : null;
        var isSentenceStart = IsSentenceStart(segmentText, matchIndex);

        var weakVerb = isSentenceStart && mode == LintMode.Procedure;
        var strongVerb = HasOtherVerbSignal(matchText, precedingWord, followingWord, secondFollowingWord);
        var (strongNoun, strongNounExcludingCompoundModifier, weakNoun) =
            HasNounSignal(matchText, precedingWord, governingWord, followingWord, segmentText, strongVerb);

        return new GuessSignals(strongVerb, strongNoun, strongNounExcludingCompoundModifier, weakVerb, weakNoun);
    }

    /// <summary>
    ///     Evaluates every verb-leaning signal in the rule set that is anchored to the match's own
    ///     local context (infinitive marker, modal auxiliary, progressive auxiliary, verb
    ///     inflection suffix) - excluding the mode-dependent imperative-sentence-start signal.
    ///     These signals are strong enough that the whole-segment
    ///     <c>VerblessSegment</c> noun signal (see <see cref="HasNounSignal"/>) must not be allowed
    ///     to out-vote them; the weaker imperative signal, by contrast, may still conflict with it
    ///     (see <see cref="Guess"/>).
    /// </summary>
    private static bool HasOtherVerbSignal(
        string matchText, string? precedingWord, string? followingWord, string? secondFollowingWord)
    {
        if (string.Equals(precedingWord, "to", StringComparison.OrdinalIgnoreCase))
        {
            return true; // InfinitiveMarker
        }

        if (precedingWord is not null && ModalAuxiliaries.Contains(precedingWord))
        {
            return true; // ModalAuxiliary
        }

        if (precedingWord is not null && BeAuxiliaries.Contains(precedingWord)
                                       && matchText.EndsWith("ing", StringComparison.OrdinalIgnoreCase))
        {
            return true; // ProgressiveAuxiliary
        }

        if (matchText.EndsWith("ed", StringComparison.OrdinalIgnoreCase)
            || matchText.EndsWith("ing", StringComparison.OrdinalIgnoreCase))
        {
            return true; // VerbInflectionSuffix
        }

        if (followingWord is not null && Articles.Contains(followingWord))
        {
            return true; // FollowedByArticle (match takes a direct object noun phrase, e.g. "utilize the tool")
        }

        if (followingWord is not null && LooksLikeNumber(followingWord)
                                       && IsVerbQualifiedNumber(followingWord, secondFollowingWord))
        {
            return true; // FollowedByQualifiedNumber (match takes a numeric direct object, e.g. "use 0.12 ohms")
        }

        return false;
    }

    /// <summary>
    ///     Determines whether a word looks like a numeral (an optional sign followed by digits,
    ///     with an optional decimal point), used as a verb signal: a word immediately followed by
    ///     a number is very likely a transitive verb taking that number as (part of) its direct
    ///     object, for example "use 0.12" or "set 5".
    /// </summary>
    private static bool LooksLikeNumber(string word)
    {
        var candidate = word.TrimStart('+', '-');
        return candidate.Length > 0 && candidate.All(c => char.IsDigit(c) || c == '.');
    }

    /// <summary>
    ///     Determines whether a following bare number is "qualified" in a way that still implies a
    ///     transitive-verb reading: either the number itself carries a decimal point (a measured
    ///     value, e.g. "0.12"), or the word immediately after it looks like a unit-of-measure
    ///     abbreviation (e.g. "5 volts"). A bare, unqualified integer (for example "0" in
    ///     "Block 0") is far more likely to be a trailing identifier/label for a preceding noun
    ///     than a verb's numeric direct object, and is instead handled as a noun signal (see
    ///     <see cref="HasNounSignal"/>'s <c>FollowedByBareIdentifier</c> signal).
    /// </summary>
    private static bool IsVerbQualifiedNumber(string number, string? secondFollowingWord)
    {
        if (number.Contains('.'))
        {
            return true;
        }

        return secondFollowingWord is not null && LooksLikeUnitWord(secondFollowingWord);
    }

    /// <summary>
    ///     Named units of measure that are recognized by <see cref="LooksLikeUnitWord"/>
    ///     regardless of length (for example "volts" and "watts" are five letters), kept as an
    ///     explicit list rather than widening the short-abbreviation length cap below, so that
    ///     adding a genuine unit name never also admits an unrelated longer word. This mirrors the
    ///     units already documented for this heuristic (see <see cref="IsVerbQualifiedNumber"/>'s
    ///     and this method's remarks, and the design doc's "set 5 volts" example).
    /// </summary>
    private static readonly HashSet<string> NamedUnitWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ohms", "amps", "volts", "watts"
        };

    /// <summary>
    ///     Short unit-of-measure abbreviations recognized by <see cref="LooksLikeUnitWord"/>, kept
    ///     as a closed, curated list rather than accepting any short alphabetic word, so that an
    ///     unrelated short word immediately after a qualified number (for example "test" in
    ///     "Block 0 test failed", or "pins"/"unit") is never mistaken for a unit abbreviation -
    ///     which would otherwise wrongly treat the bare-identifier number as "qualified" and select
    ///     a verb reading over the correct noun-label reading.
    /// </summary>
    private static readonly HashSet<string> ShortUnitAbbreviations =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ms", "kg", "psi", "hz", "khz", "mhz", "ghz", "kv", "mv", "ma", "kw", "mw",
            "pa", "kpa", "mpa", "db", "ft", "cm", "mm", "km", "lb", "oz", "hr", "min",
            "sec", "rpm", "bar", "atm",
            // Single-letter SI base/derived unit symbols (e.g. "5 V", "5 A"); matched
            // case-insensitively like every other entry in this set, so both the
            // conventional uppercase symbol and a lowercase typing of it are recognized.
            "v", "a", "w", "n", "j", "k", "s", "m", "g", "l",
        };

    /// <summary>
    ///     Determines whether a word looks like a unit-of-measure abbreviation or name (for
    ///     example "ohms", "volts", "watts", "ms", "kg", "psi") rather than an ordinary function
    ///     word or unrelated short word. A word in <see cref="NamedUnitWords"/> is recognized
    ///     regardless of length; otherwise the word must be an entry in the closed
    ///     <see cref="ShortUnitAbbreviations"/> list.
    /// </summary>
    private static bool LooksLikeUnitWord(string word)
    {
        if (word.Length == 0 || !word.All(char.IsLetter))
        {
            return false;
        }

        return NamedUnitWords.Contains(word) || ShortUnitAbbreviations.Contains(word);
    }

    /// <summary>
    ///     Evaluates every noun-leaning signal in the rule set, split into a <i>strong</i> tier
    ///     (specific, local, hard-to-confuse evidence) and a <i>weak</i> tier (the broad
    ///     plural-suffix and whole-segment-verbless fallbacks), for use by <see cref="GuessEffective"/>
    ///     (see <see cref="GuessSignals"/>). <see cref="Guess"/> treats both tiers identically (a
    ///     plain OR), preserving this method's previous, unsplit behavior exactly.
    /// </summary>
    /// <remarks>
    ///     The strong-noun tier is additionally returned with the <c>NounCompoundModifier</c> term
    ///     excluded (<c>StrongNounExcludingCompoundModifier</c>), because that single term is not
    ///     as unambiguous as the others: a following word that merely looks like a plural technical
    ///     noun is equally consistent with "imperative verb + plural direct object" (for example
    ///     "Check modules") as with "noun-compound modifier + noun head" (for example "check
    ///     valve"). <see cref="GuessEffective"/> uses the excluding variant to decide whether that
    ///     ambiguity alone should be allowed to override a weak imperative-sentence-start signal;
    ///     every other strong-noun source (article, possessive, preposition, etc.) is never
    ///     consistent with an imperative reading, so it is included in both return values and
    ///     always overrides that weak signal exactly as before.
    /// </remarks>
    private static (bool StrongNoun, bool StrongNounExcludingCompoundModifier, bool WeakNoun) HasNounSignal(
        string matchText,
        string? precedingWord,
        string? governingWord,
        string? followingWord,
        string segmentText,
        bool hasOtherVerbSignal)
    {
        var strongNounExcludingCompoundModifier =
            (precedingWord is not null && Articles.Contains(precedingWord)) // Article
            || (precedingWord is not null
                && (PossessivePronouns.Contains(precedingWord) // Possessive
                    || precedingWord.EndsWith("'s", StringComparison.OrdinalIgnoreCase)))
            || (precedingWord is not null && QuantifiersOrDemonstratives.Contains(precedingWord)) // QuantifierOrDemonstrative
            || (precedingWord is not null && Prepositions.Contains(precedingWord)) // Preposition
            || (governingWord is not null
                && (Articles.Contains(governingWord)
                    || PossessivePronouns.Contains(governingWord)
                    || QuantifiersOrDemonstratives.Contains(governingWord)
                    || Prepositions.Contains(governingWord))) // DeterminerGovernsThroughModifiers
            || string.Equals(followingWord, "of", StringComparison.OrdinalIgnoreCase) // NounPhraseContinuation
            || (followingWord is not null && IsFiniteVerbForm(followingWord)) // FollowedByFiniteVerb (subject position)
            || (followingWord is not null
                && (string.Equals(followingWord, "not", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(followingWord, "never", StringComparison.OrdinalIgnoreCase))
                && !ModalAuxiliaries.Contains(matchText)
                && !BeAuxiliaries.Contains(matchText)
                && !OtherAuxiliaryVerbs.Contains(matchText)) // FollowedByNegation
            || (followingWord is not null && LooksLikeNumber(followingWord) && !followingWord.Contains('.')
                && !hasOtherVerbSignal); // FollowedByBareIdentifier (e.g. "Block 0")

        var nounCompoundModifier =
            followingWord is not null && LooksLikeCompoundNoun(followingWord) && !hasOtherVerbSignal;

        var strongNoun = strongNounExcludingCompoundModifier || nounCompoundModifier;

        var weakNoun =
            (matchText.EndsWith('s') && !matchText.EndsWith("ss", StringComparison.OrdinalIgnoreCase)
                && !ModalAuxiliaries.Contains(matchText) && !BeAuxiliaries.Contains(matchText)
                && !OtherAuxiliaryVerbs.Contains(matchText)) // PluralNounSuffix (closed-class auxiliaries excluded,
                                                             // e.g. "does"/"has"/"is" are never nouns despite
                                                             // ending in "s")
            || (!hasOtherVerbSignal && !HasAnyFiniteVerb(segmentText)); // VerblessSegment

        return (strongNoun, strongNounExcludingCompoundModifier, weakNoun);
    }

    /// <summary>
    ///     Determines whether a following word looks like the head noun of a noun-noun compound
    ///     (for example "fixture" in "test fixture", "cycle" in "duty cycle") rather than a verb,
    ///     function word, or number. Deliberately conservative: articles, prepositions,
    ///     quantifiers, possessives, conjunctions, auxiliaries, and anything that looks like a
    ///     finite verb form or a verb inflection are excluded, as is anything that is not a plain
    ///     alphabetic word (so numbers such as "0.12" never trigger this signal).
    /// </summary>
    private static bool LooksLikeCompoundNoun(string followingWord)
    {
        if (followingWord.Length == 0 || !followingWord.All(char.IsLetter))
        {
            return false;
        }

        if (CompoundNounHeadAllowList.Contains(followingWord))
        {
            // Explicit, curated allow-list: a vetted technical plural noun that overrides the
            // general "ends in 's'" exclusion below (see that exclusion's remarks).
            return true;
        }

        if (Articles.Contains(followingWord)
            || PossessivePronouns.Contains(followingWord)
            || QuantifiersOrDemonstratives.Contains(followingWord)
            || Prepositions.Contains(followingWord)
            || ClauseBreakingWords.Contains(followingWord)
            || ModalAuxiliaries.Contains(followingWord)
            || BeAuxiliaries.Contains(followingWord)
            || NonCompoundHeadWords.Contains(followingWord)
            || ObjectPronouns.Contains(followingWord))
        {
            return false;
        }

        if (IsFiniteVerbForm(followingWord)
            || followingWord.EndsWith("ing", StringComparison.OrdinalIgnoreCase)
            || followingWord.EndsWith("ed", StringComparison.OrdinalIgnoreCase)
            || followingWord.EndsWith("ly", StringComparison.OrdinalIgnoreCase)
            || followingWord.EndsWith('s'))
        {
            // Any word ending in "s" is excluded (not just recognized finite-verb forms): it is
            // ambiguously either a 3rd-person-singular verb (e.g. "happens") or a plural noun, and
            // words such as "sometimes" are adverbs, not compound-noun heads. Being conservative
            // here avoids false noun-compound signals on unrecognized verb/adverb forms.
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Determines whether any whitespace-delimited token in the segment looks like a finite
    ///     verb form (see <see cref="IsFiniteVerbForm"/>). Used as a whole-segment noun signal:
    ///     a segment such as a table cell, list item, or heading fragment that contains no finite
    ///     verb at all cannot be using any of its words as a finite verb, so a verb-only
    ///     dictionary entry cannot apply within it.
    /// </summary>
    private static bool HasAnyFiniteVerb(string segmentText)
    {
        var tokens = segmentText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(rawToken => IsFiniteVerbForm(Normalize(rawToken)));
    }

    /// <summary>
    ///     Determines whether a word looks like a finite (present-tense) verb form: a modal
    ///     auxiliary, a "to be" auxiliary, or a third-person-singular "-s" inflection distinct
    ///     from a plural noun suffix (matches "moves"/"has"/"is" style forms).
    /// </summary>
    private static bool IsFiniteVerbForm(string word)
    {
        if (ModalAuxiliaries.Contains(word) || BeAuxiliaries.Contains(word))
        {
            return true;
        }

        return FiniteVerbForms.Contains(word);
    }

    /// <summary>
    ///     Scans backward from a match past a bounded run of adjective/participle/proper-noun
    ///     modifier tokens (for example "metering", "custom", or a capitalized product name in
    ///     "the Vantage probe") to find the determiner that still governs the match as the head
    ///     noun of a compound noun phrase.
    /// </summary>
    /// <remarks>
    ///     Only tokens that look like modifiers are skipped (see <see cref="LooksLikeModifier"/>);
    ///     the scan stops, without finding a governing word, as soon as an ordinary word (a verb,
    ///     ordinary noun, or clause-boundary word) is reached, and is additionally bounded by
    ///     <see cref="MaxModifierScanDistance"/> tokens so a genuinely unrelated earlier word in
    ///     the sentence can never be mistaken for a governing determiner.
    /// </remarks>
    private static string? GoverningDeterminer(string segmentText, int matchIndex)
    {
        var before = segmentText[..matchIndex];
        var tokens = before.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        var scanned = 0;
        for (var i = tokens.Length - 1; i >= 0 && scanned < MaxModifierScanDistance; i--, scanned++)
        {
            var rawToken = tokens[i];
            var normalized = Normalize(rawToken);

            if (Articles.Contains(normalized)
                || PossessivePronouns.Contains(normalized)
                || normalized.EndsWith("'s", StringComparison.OrdinalIgnoreCase)
                || QuantifiersOrDemonstratives.Contains(normalized)
                || Prepositions.Contains(normalized))
            {
                return normalized;
            }

            if (!LooksLikeModifier(rawToken, normalized))
            {
                return null; // not a determiner and not a modifier: stop scanning
            }
        }

        return null;
    }

    /// <summary>
    ///     Determines whether a token looks like an adjective/participle/proper-noun modifier
    ///     that a determiner could still govern through (for example "custom", "metering", or a
    ///     capitalized product name), rather than an ordinary word (a verb, or ordinary noun such
    ///     as "system") that would break the determiner's reach. Deliberately conservative: an
    ///     ordinary lower-case word that is none of these is treated as breaking the chain, so a
    ///     sentence like "The system shall report..." does not let "the" reach past "system" to
    ///     govern "shall".
    /// </summary>
    private static bool LooksLikeModifier(string rawToken, string normalized)
    {
        if (normalized.Length == 0
            || ClauseBreakingWords.Contains(normalized)
            || ModalAuxiliaries.Contains(normalized)
            || BeAuxiliaries.Contains(normalized)
            || string.Equals(normalized, "to", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalized.EndsWith("ing", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("ed", StringComparison.OrdinalIgnoreCase))
        {
            return true; // participle modifier ("metering", "coated")
        }

        if (CommonAdjectiveModifiers.Contains(normalized))
        {
            return true; // closed-class adjective modifier ("custom", "manual")
        }

        // A capitalized word appearing mid-sentence (not itself sentence-initial capitalization)
        // is treated as a proper-noun modifier, for example a product name in "the Vantage probe".
        return rawToken.Length > 0 && char.IsUpper(rawToken[0]);
    }

    /// <summary>
    ///     Extracts the last whitespace-delimited token strictly before <paramref name="matchIndex"/>,
    ///     lower-cased and stripped of leading/trailing punctuation.
    /// </summary>
    private static string? PrecedingWord(string segmentText, int matchIndex)
    {
        var before = segmentText[..matchIndex];
        var tokens = before.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return null;
        }

        return Normalize(tokens[^1]);
    }

    /// <summary>
    ///     Extracts up to <paramref name="count"/> whitespace-delimited tokens strictly after
    ///     <paramref name="afterIndex"/>, each lower-cased and stripped of leading/trailing
    ///     punctuation.
    /// </summary>
    private static string[] FollowingWords(string segmentText, int afterIndex, int count)
    {
        if (afterIndex >= segmentText.Length)
        {
            return [];
        }

        var after = segmentText[afterIndex..];
        var tokens = after.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Take(count).Select(Normalize).ToArray();
    }

    /// <summary>
    ///     Lower-cases and strips leading/trailing punctuation from a token, so that surrounding
    ///     commas, periods, and quotation marks do not defeat exact keyword comparisons. A
    ///     trailing possessive <c>'s</c> is deliberately preserved (see <see cref="HasNounSignal"/>).
    /// </summary>
    private static string Normalize(string token)
    {
        if (token.EndsWith("'s", StringComparison.OrdinalIgnoreCase))
        {
            return LeadingPunctuationRegex.Replace(token, string.Empty).ToLowerInvariant();
        }

        var stripped = LeadingPunctuationRegex.Replace(token, string.Empty);
        stripped = TrailingPunctuationRegex.Replace(stripped, string.Empty);
        return stripped.ToLowerInvariant();
    }

    /// <summary>
    ///     Determines whether a match begins the segment, or immediately follows the same
    ///     sentence-boundary condition as <see cref="SentenceAnalyzer"/>'s <c>SentenceSplitRegex</c>
    ///     (<c>[.!?:]</c> followed by whitespace).
    /// </summary>
    private static bool IsSentenceStart(string segmentText, int matchIndex)
    {
        if (matchIndex == 0)
        {
            return true;
        }

        var before = segmentText[..matchIndex];
        return SentenceBoundaryRegex.IsMatch(before);
    }

    /// <summary>
    ///     Catenative verbs that commonly take a gerund (noun-like <c>-ing</c> clause) as their
    ///     complement, for example "continue" in "continue monitoring the gauge". When an
    ///     <c>-ing</c> word immediately follows one of these, it is a gerund complement, not a
    ///     finite verb in its own right.
    /// </summary>
    private static readonly HashSet<string> CatenativeVerbs =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "begin", "begins", "began", "continue", "continues", "continued", "stop", "stops",
            "stopped", "keep", "keeps", "kept", "start", "starts", "started", "finish", "finishes",
            "finished", "avoid", "avoids", "avoided", "resume", "resumes", "resumed", "delay",
            "delays", "delayed", "cease", "ceases", "ceased", "consider", "considers", "considered",
        };

    /// <summary>
    ///     Resolves whether a word matching STE100-ADV-INGFORM's <c>-ing</c> pattern is being used
    ///     as a present-participle verb (should be flagged) or as a noun/adjective (gerund,
    ///     material noun, or participial adjective - should not be flagged).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     This is deliberately a separate resolution path from <see cref="Guess"/>/
    ///     <see cref="GuessEffective"/>: those general-purpose methods treat a bare <c>-ing</c>/
    ///     <c>-ed</c> suffix as a strong verb signal in its own right (<c>VerbInflectionSuffix</c>),
    ///     which is correct for most dictionary-term lookups but is circular when the very
    ///     question being asked is "is this <c>-ing</c> word a verb" - every candidate the ing-form
    ///     check considers ends in <c>-ing</c> by construction, so that signal would fire for every
    ///     candidate and defeat noun/adjective recognition entirely.
    ///     </para>
    ///     <para>
    ///     Decision rule: a confident <see cref="PartOfSpeech.Verb"/> result requires genuine
    ///     progressive/modal evidence (preceded by a "to be" auxiliary or a modal) or - when the
    ///     match is not itself sentence-initial, does not immediately follow a
    ///     <see cref="CatenativeVerbs"/> entry, and does not immediately follow a
    ///     <see cref="Prepositions"/> entry (or "to" - see below) - a transitive-object follow-on
    ///     (followed by an article, object pronoun, or qualified number) with no conflicting noun
    ///     evidence. Everything else defaults to not-flagged (noun/adjective), matching this
    ///     feature's intent of reducing false positives on gerunds and participial adjectives.
    ///     </para>
    ///     <para>
    ///     Unlike <see cref="HasOtherVerbSignal"/> - where a preceding "to" is valid infinitive-
    ///     marker evidence for a non-<c>-ing</c> dictionary term (e.g. "to test") - "to" is never a
    ///     true infinitive marker here: a genuine infinitive is always "to" + the bare base verb
    ///     form (e.g. "to test"), never "to" + an <c>-ing</c> form (there is no infinitival "to
    ///     testing"). Every <c>-ing</c> word immediately preceded by "to" is therefore a gerund
    ///     functioning as the object of the preposition "to" (e.g. "the key <c>to testing</c> the
    ///     gauge", "object <c>to monitoring</c> the system"), so a preceding "to" is folded into
    ///     <c>precededByPreposition</c> below alongside the <see cref="Prepositions"/> set, gating
    ///     it out of the transitive-object shortcut exactly like any other preposition-object
    ///     gerund, rather than being treated as verb evidence.
    ///     </para>
    ///     <para>
    ///     The sentence-initial and catenative-complement gate on the transitive-object follow-on
    ///     is load-bearing: a gerund subject (e.g. "Metering the flow is required.") or a
    ///     catenative-verb gerund complement (e.g. "continue monitoring the gauge") is very often
    ///     itself followed by a direct object, so without this gate the transitive-object signal
    ///     alone would wrongly classify both as verbs before the sentence-initial/catenative noun
    ///     evidence below ever gets a chance to fire. The additional preposition gate exists for
    ///     the same reason: a gerund that is itself the object of a preposition (e.g. "before
    ///     <c>testing</c> the gauge", "the key to <c>testing</c> the gauge") can still take its own
    ///     direct object, so being preceded by a preposition (including "to") does not imply there
    ///     is no following word for the transitive-object follow-on to match against - without
    ///     this explicit gate, that following direct object would wrongly trigger the transitive-
    ///     object shortcut before the preposition-governed noun evidence below ever gets a chance
    ///     to fire.
    ///     </para>
    /// </remarks>
    internal static PartOfSpeech? GuessIngFormRole(string segmentText, int matchIndex, int matchLength)
    {
        ArgumentNullException.ThrowIfNull(segmentText);

        var precedingWord = PrecedingWord(segmentText, matchIndex);
        var governingWord = GoverningDeterminer(segmentText, matchIndex);
        var followingWords = FollowingWords(segmentText, matchIndex + matchLength, 1);
        var followingWord = followingWords.Length > 0 ? followingWords[0] : null;
        var isSentenceStart = IsSentenceStart(segmentText, matchIndex);
        var precededByCatenativeVerb = precedingWord is not null && CatenativeVerbs.Contains(precedingWord);

        // "to" is folded in here alongside the Prepositions set: unlike HasOtherVerbSignal's use
        // of "to" as an infinitive marker for non-ing dictionary terms, "to" + an -ing word is
        // never a true infinitive (there is no "to testing") - it is always "to" as a preposition
        // governing a gerund object (e.g. "the key to testing the gauge"). See remarks.
        var precededByPreposition = precedingWord is not null
                                     && (Prepositions.Contains(precedingWord)
                                         || string.Equals(precedingWord, "to", StringComparison.OrdinalIgnoreCase));

        var progressiveOrInfinitiveVerb =
            (precedingWord is not null && BeAuxiliaries.Contains(precedingWord)) // progressive, e.g. "is closing"
            || (precedingWord is not null && ModalAuxiliaries.Contains(precedingWord));

        if (progressiveOrInfinitiveVerb)
        {
            // This evidence is anchored to the preceding word, not the sentence-initial/catenative
            // position, so it is always safe to resolve as a verb outright.
            return PartOfSpeech.Verb;
        }

        if (!isSentenceStart && !precededByCatenativeVerb && !precededByPreposition)
        {
            var transitiveObjectVerb =
                (followingWord is not null && Articles.Contains(followingWord)) // transitive object, e.g. "closing the valve"
                || (followingWord is not null && ObjectPronouns.Contains(followingWord))
                || (followingWord is not null && LooksLikeNumber(followingWord));

            if (transitiveObjectVerb)
            {
                // Direct transitive-object evidence wins outright, but only once the
                // sentence-initial/catenative-complement/preposition-object signals above have had
                // the chance to be checked first (see remarks). A gerund that is itself the object
                // of a preposition (e.g. "before testing the gauge") can still be immediately
                // followed by its own direct object, so being preceded by a preposition does not
                // imply there is nothing left for this follow-on to match against; the explicit
                // "!precededByPreposition" gate above is what keeps that case from reaching this
                // branch at all, regardless of whether it has a following object.
                return PartOfSpeech.Verb;
            }
        }

        var strongNoun =
            (precedingWord is not null && Articles.Contains(precedingWord)) // "the fitting"
            || (precedingWord is not null
                && (PossessivePronouns.Contains(precedingWord)
                    || precedingWord.EndsWith("'s", StringComparison.OrdinalIgnoreCase)))
            || (precedingWord is not null && QuantifiersOrDemonstratives.Contains(precedingWord))
            || precededByPreposition // object of a preposition, including "to" (see remarks)
            || precededByCatenativeVerb // gerund complement
            || (governingWord is not null
                && (Articles.Contains(governingWord)
                    || PossessivePronouns.Contains(governingWord)
                    || QuantifiersOrDemonstratives.Contains(governingWord)
                    || Prepositions.Contains(governingWord)))
            || string.Equals(followingWord, "of", StringComparison.OrdinalIgnoreCase)
            || (followingWord is not null && followingWord.Length > 0
                                           && followingWord.All(char.IsLetter)
                                           && !Articles.Contains(followingWord)
                                           && !ObjectPronouns.Contains(followingWord)
                                           && !ClauseBreakingWords.Contains(followingWord)
                                           && !ModalAuxiliaries.Contains(followingWord)
                                           && !BeAuxiliaries.Contains(followingWord)
                                           && !Prepositions.Contains(followingWord)
                                           && !IsFiniteVerbForm(followingWord)
                                           && !followingWord.EndsWith("ly", StringComparison.OrdinalIgnoreCase)); // followed by a noun it modifies, e.g. "moving structure"

        var weakNoun =
            isSentenceStart // sentence/list-item/heading-initial without verb evidence
            || !HasAnyFiniteVerb(segmentText); // verbless segment (e.g. a table cell/title fragment)

        if (strongNoun || weakNoun)
        {
            return PartOfSpeech.Noun;
        }

        // Inconclusive: default to not flagging (noun/adjective), consistent with this feature's
        // goal of reducing -ing-form false positives.
        return null;
    }
}
