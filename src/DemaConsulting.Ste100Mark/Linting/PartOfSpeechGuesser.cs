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
            "of", "in", "on", "for", "with", "by", "from", "about", "at", "into", "onto", "through", "during"
        };

    /// <summary>
    ///     Closed-class, third-person-singular-present finite verb forms that signal the
    ///     preceding match is the subject of a clause (for example "probe moves", "probe has").
    ///     Deliberately excludes any "-s" content word, which would be indistinguishable from the
    ///     plural noun suffix; see <see cref="HasNounSignal"/>'s separate <c>PluralNounSuffix</c>
    ///     signal for that case.
    /// </summary>
    private static readonly HashSet<string> FiniteVerbForms =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "moves", "has", "does", "goes", "runs", "opens", "closes", "starts", "stops",
            "operates", "requires", "indicates", "shows", "displays", "connects", "controls"
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
            "analog", "external", "internal", "optional", "additional", "main", "backup"
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
        ArgumentNullException.ThrowIfNull(segmentText);

        var matchText = segmentText.Substring(matchIndex, matchLength);
        var precedingWord = PrecedingWord(segmentText, matchIndex);
        var governingWord = GoverningDeterminer(segmentText, matchIndex);
        var followingWord = FollowingWord(segmentText, matchIndex + matchLength);
        var isSentenceStart = IsSentenceStart(segmentText, matchIndex);

        var isImperative = isSentenceStart && mode == LintMode.Procedure;
        var hasOtherVerbSignal = HasOtherVerbSignal(matchText, precedingWord, followingWord);
        var hasVerb = isImperative || hasOtherVerbSignal;
        var hasNoun = HasNounSignal(matchText, precedingWord, governingWord, followingWord, segmentText, hasVerb);

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
    ///     Evaluates every verb-leaning signal in the rule set that is anchored to the match's own
    ///     local context (infinitive marker, modal auxiliary, progressive auxiliary, verb
    ///     inflection suffix) - excluding the mode-dependent imperative-sentence-start signal.
    ///     These signals are strong enough that the whole-segment
    ///     <c>VerblessSegment</c> noun signal (see <see cref="HasNounSignal"/>) must not be allowed
    ///     to out-vote them; the weaker imperative signal, by contrast, may still conflict with it
    ///     (see <see cref="Guess"/>).
    /// </summary>
    private static bool HasOtherVerbSignal(string matchText, string? precedingWord, string? followingWord)
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

        if (followingWord is not null && LooksLikeNumber(followingWord))
        {
            return true; // FollowedByNumber (match takes a numeric direct object, e.g. "use 0.12 ohms")
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
    ///     Evaluates every noun-leaning signal in the rule set as a set (any one hit is
    ///     sufficient).
    /// </summary>
    private static bool HasNounSignal(
        string matchText,
        string? precedingWord,
        string? governingWord,
        string? followingWord,
        string segmentText,
        bool hasOtherVerbSignal)
    {
        if (precedingWord is not null && Articles.Contains(precedingWord))
        {
            return true; // Article
        }

        if (precedingWord is not null
            && (PossessivePronouns.Contains(precedingWord)
                || precedingWord.EndsWith("'s", StringComparison.OrdinalIgnoreCase)))
        {
            return true; // Possessive
        }

        if (precedingWord is not null && QuantifiersOrDemonstratives.Contains(precedingWord))
        {
            return true; // QuantifierOrDemonstrative
        }

        if (precedingWord is not null && Prepositions.Contains(precedingWord))
        {
            return true; // Preposition
        }

        if (governingWord is not null
            && (Articles.Contains(governingWord)
                || PossessivePronouns.Contains(governingWord)
                || QuantifiersOrDemonstratives.Contains(governingWord)
                || Prepositions.Contains(governingWord)))
        {
            return true; // DeterminerGovernsThroughModifiers
        }

        if (matchText.EndsWith('s') && !matchText.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
        {
            return true; // PluralNounSuffix
        }

        if (string.Equals(followingWord, "of", StringComparison.OrdinalIgnoreCase))
        {
            return true; // NounPhraseContinuation
        }

        if (followingWord is not null && IsFiniteVerbForm(followingWord))
        {
            return true; // FollowedByFiniteVerb (match is the subject of the sentence)
        }

        if (followingWord is not null && LooksLikeCompoundNoun(followingWord) && !hasOtherVerbSignal)
        {
            return true; // NounCompoundModifier (match modifies a following noun, e.g. "test fixture")
        }

        if (!hasOtherVerbSignal && !HasAnyFiniteVerb(segmentText))
        {
            return true; // VerblessSegment (no finite verb anywhere: cannot be verb-only usage)
        }

        return false;
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

        if (Articles.Contains(followingWord)
            || PossessivePronouns.Contains(followingWord)
            || QuantifiersOrDemonstratives.Contains(followingWord)
            || Prepositions.Contains(followingWord)
            || ClauseBreakingWords.Contains(followingWord)
            || ModalAuxiliaries.Contains(followingWord)
            || BeAuxiliaries.Contains(followingWord)
            || NonCompoundHeadWords.Contains(followingWord))
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
    ///     Extracts the first whitespace-delimited token strictly after <paramref name="afterIndex"/>,
    ///     lower-cased and stripped of leading/trailing punctuation.
    /// </summary>
    private static string? FollowingWord(string segmentText, int afterIndex)
    {
        if (afterIndex >= segmentText.Length)
        {
            return null;
        }

        var after = segmentText[afterIndex..];
        var tokens = after.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length == 0 ? null : Normalize(tokens[0]);
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
}
