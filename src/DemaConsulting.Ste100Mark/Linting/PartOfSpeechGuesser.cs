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
///     Best-effort, deterministic, rule-based heuristic used only by the STE100-ADV-INGFORM
///     advisory in <see cref="StructuralRules"/> to decide whether an <c>-ing</c> word is a
///     present-participle verb (flagged) or a gerund/participial adjective/noun (not flagged).
/// </summary>
/// <remarks>
///     <para>
///     Dictionary-term part-of-speech selection is no longer done here: it is performed by
///     <see cref="IPartOfSpeechResolver"/> (see <see cref="PartOfSpeechResolver"/>). This class is
///     retained solely for the gerund-versus-participle distinction, which a word-level tagger
///     cannot make (it tags a gerund with a direct object, for example "before testing the gauge",
///     as a verb, whereas the advisory must not flag it).
///     </para>
///     <para>
///     <b>This is not a grammatical guarantee.</b> It is a lightweight signal heuristic that only
///     ever returns <see cref="PartOfSpeech.Verb"/>, <see cref="PartOfSpeech.Noun"/>, or
///     <see langword="null"/> (inconclusive, which the advisory treats as "do not flag").
///     </para>
/// </remarks>
internal static class PartOfSpeechGuesser
{
    /// <summary>Maximum number of modifier tokens to scan past when looking for a governing determiner.</summary>
    private const int MaxModifierScanDistance = 3;

    /// <summary>Regex timeout applied to every pattern used by this class.</summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Answers "does a new sentence start immediately after this point in the text".</summary>
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

    /// <summary>Personal/object pronouns: never the head noun of a following noun phrase.</summary>
    private static readonly HashSet<string> ObjectPronouns =
        new(StringComparer.OrdinalIgnoreCase)
        { "it", "them", "him", "her", "us", "me", "you", "they", "we", "he", "she", "one" };

    /// <summary>
    ///     Closed-class, third-person-singular-present finite verb forms that signal a clause has a
    ///     finite verb. The list deliberately omits any such word that is also a common
    ///     technical-writing plural noun (for example "results", "checks", "tests").
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
    ///     that do not carry a recognizable participle suffix.
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

    /// <summary>
    ///     Catenative verbs that commonly take a gerund (noun-like <c>-ing</c> clause) as their
    ///     complement, for example "continue" in "continue monitoring the gauge".
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
    ///     Decision rule: a confident <see cref="PartOfSpeech.Verb"/> result requires genuine
    ///     progressive/modal evidence (preceded by a "to be" auxiliary or a modal) or - when the
    ///     match is not itself sentence-initial, does not immediately follow a
    ///     <see cref="CatenativeVerbs"/> entry, and does not immediately follow a
    ///     <see cref="Prepositions"/> entry (or "to") - a transitive-object follow-on (followed by
    ///     an article, object pronoun, or number). Everything else defaults to not-flagged
    ///     (noun/adjective), matching this feature's intent of reducing false positives on gerunds
    ///     and participial adjectives.
    ///     </para>
    ///     <para>
    ///     A preceding "to" is never an infinitive marker here: there is no infinitival "to
    ///     testing", so every <c>-ing</c> word preceded by "to" is a gerund object of the preposition
    ///     "to" (for example "the key to testing the gauge"). The sentence-initial, catenative and
    ///     preposition gates on the transitive-object follow-on are load-bearing: a gerund subject
    ///     ("Metering the flow is required."), a catenative complement ("continue monitoring the
    ///     gauge") and a gerund that is the object of a preposition ("before testing the gauge") are
    ///     all often followed by their own direct object, which must not make them verbs.
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

        // "to" is folded in here alongside the Prepositions set (see remarks).
        var precededByPreposition = precedingWord is not null
                                     && (Prepositions.Contains(precedingWord)
                                         || string.Equals(precedingWord, "to", StringComparison.OrdinalIgnoreCase));

        var progressiveOrInfinitiveVerb =
            (precedingWord is not null && BeAuxiliaries.Contains(precedingWord)) // progressive, e.g. "is closing"
            || (precedingWord is not null && ModalAuxiliaries.Contains(precedingWord));

        if (progressiveOrInfinitiveVerb)
        {
            // Anchored to the preceding word, so always safe to resolve as a verb outright.
            return PartOfSpeech.Verb;
        }

        if (!isSentenceStart && !precededByCatenativeVerb && !precededByPreposition)
        {
            var transitiveObjectVerb =
                (followingWord is not null && Articles.Contains(followingWord)) // e.g. "closing the valve"
                || (followingWord is not null && ObjectPronouns.Contains(followingWord))
                || (followingWord is not null && LooksLikeNumber(followingWord));

            if (transitiveObjectVerb)
            {
                return PartOfSpeech.Verb;
            }
        }

        var strongNoun =
            (precedingWord is not null && Articles.Contains(precedingWord)) // "the fitting"
            || (precedingWord is not null
                && (PossessivePronouns.Contains(precedingWord)
                    || precedingWord.EndsWith("'s", StringComparison.OrdinalIgnoreCase)))
            || (precedingWord is not null && QuantifiersOrDemonstratives.Contains(precedingWord))
            || precededByPreposition // object of a preposition, including "to"
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
                                           && !followingWord.EndsWith("ly", StringComparison.OrdinalIgnoreCase)); // e.g. "moving structure"

        var weakNoun =
            isSentenceStart // sentence/list-item/heading-initial without verb evidence
            || !HasAnyFiniteVerb(segmentText); // verbless segment (e.g. a table cell/title fragment)

        if (strongNoun || weakNoun)
        {
            return PartOfSpeech.Noun;
        }

        // Inconclusive: default to not flagging (noun/adjective).
        return null;
    }

    /// <summary>
    ///     Determines whether a word looks like a numeral (an optional sign followed by digits,
    ///     with an optional decimal point).
    /// </summary>
    private static bool LooksLikeNumber(string word)
    {
        var candidate = word.TrimStart('+', '-');
        return candidate.Length > 0 && candidate.All(c => char.IsDigit(c) || c == '.');
    }

    /// <summary>
    ///     Determines whether any whitespace-delimited token in the segment looks like a finite
    ///     verb form. A segment with no finite verb at all (a table cell, list item, or heading
    ///     fragment) cannot be using any of its words as a finite verb.
    /// </summary>
    private static bool HasAnyFiniteVerb(string segmentText)
    {
        var tokens = segmentText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(rawToken => IsFiniteVerbForm(Normalize(rawToken)));
    }

    /// <summary>
    ///     Determines whether a word looks like a finite verb form: a modal auxiliary, a "to be"
    ///     auxiliary, or a recognized third-person-singular inflection.
    /// </summary>
    private static bool IsFiniteVerbForm(string word)
    {
        return ModalAuxiliaries.Contains(word) || BeAuxiliaries.Contains(word) || FiniteVerbForms.Contains(word);
    }

    /// <summary>
    ///     Scans backward from a match past a bounded run of adjective/participle/proper-noun
    ///     modifier tokens to find the determiner that still governs the match as the head noun of
    ///     a compound noun phrase.
    /// </summary>
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
    ///     Determines whether a token looks like an adjective/participle/proper-noun modifier that a
    ///     determiner could still govern through, rather than an ordinary word that would break the
    ///     determiner's reach.
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

        // A capitalized word mid-sentence is treated as a proper-noun modifier.
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
    ///     Lower-cases and strips leading/trailing punctuation from a token. A trailing possessive
    ///     <c>'s</c> is deliberately preserved.
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
    ///     Determines whether a match begins the segment, or immediately follows a sentence
    ///     boundary (<c>[.!?:]</c> followed by whitespace).
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
