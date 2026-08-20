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

using DemaConsulting.Ste100Mark.Linting;

namespace DemaConsulting.Ste100Mark.Tests.Linting;

/// <summary>
///     Unit tests for the PartOfSpeechGuesser class, exercising each signal in the documented
///     heuristic rule set.
/// </summary>
public class PartOfSpeechGuesserTests
{
    /// <summary>
    ///     Test that a term preceded by the infinitive marker "to" is guessed as a verb.
    /// </summary>
    [Fact]
    public void Guess_PrecededByTo_ReturnsVerb()
    {
        // Arrange: "to impact" - infinitive marker signal
        const string text = "We need to impact the outcome.";
        var index = text.IndexOf("impact", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impact".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Verb, result);
    }

    /// <summary>
    ///     Test that a term preceded by a modal auxiliary ("must") is guessed as a verb.
    /// </summary>
    [Fact]
    public void Guess_PrecededByModal_ReturnsVerb()
    {
        // Arrange: "must impact" - modal auxiliary signal
        const string text = "It must impact the schedule.";
        var index = text.IndexOf("impact", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impact".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Verb, result);
    }

    /// <summary>
    ///     Test that a term preceded by a "be" auxiliary and ending in "-ing" is guessed as a verb.
    /// </summary>
    [Fact]
    public void Guess_PrecededByBeAuxiliaryWithIngSuffix_ReturnsVerb()
    {
        // Arrange: "is impacting" - progressive auxiliary signal
        const string text = "The delay is impacting the schedule.";
        var index = text.IndexOf("impacting", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impacting".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Verb, result);
    }

    /// <summary>
    ///     Test that a bare "-ed"/"-ing" inflected form is guessed as a verb even without any other
    ///     signal.
    /// </summary>
    [Fact]
    public void Guess_InflectionSuffixAlone_ReturnsVerb()
    {
        // Arrange: "impacted" - verb inflection suffix signal, no other context word
        const string text = "Weather impacted operations yesterday.";
        var index = text.IndexOf("impacted", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impacted".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Verb, result);
    }

    /// <summary>
    ///     Test that a term at the start of a sentence in Procedure mode is guessed as a verb
    ///     (imperative sentence start signal).
    /// </summary>
    [Fact]
    public void Guess_ImperativeSentenceStartInProcedureMode_ReturnsVerb()
    {
        // Arrange: the match begins the segment, and mode is Procedure
        const string text = "Function the panel before use.";
        var index = text.IndexOf("Function", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "Function".Length, LintMode.Procedure);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Verb, result);
    }

    /// <summary>
    ///     Test that the mode-dependent imperative-sentence-start signal (which is intentionally
    ///     weaker than the local verb-context signals in <see cref="PartOfSpeechGuesser"/>'s own
    ///     documented rule set) is still allowed to conflict with a strong noun-compound signal
    ///     ("Idle threshold" - a noun-phrase modifier immediately followed by a noun) rather than
    ///     unconditionally suppressing it. Regression test for a bug where the noun signal
    ///     evaluation was passed the combined verb result (imperative-or-other-signal) instead of
    ///     just the strong other-verb-signal, causing the imperative signal to wrongly out-rank the
    ///     noun-compound signal and misclassify an adjectival/nominal sentence-start word as a verb
    ///     purely because it began a Procedure-mode segment (for example the first cell of a table
    ///     row).
    /// </summary>
    [Fact]
    public void Guess_ImperativeSentenceStartButNounCompoundFollows_ReturnsNull()
    {
        // Arrange: "Idle threshold..." begins a Procedure-mode segment (so the weak imperative
        // signal fires), but "Idle" is immediately followed by "threshold", a noun-compound head,
        // so the strong noun-compound signal should still be allowed to conflict with it,
        // resolving as ambiguous rather than confidently Verb.
        const string text = "Idle threshold for leaving the path charged or drained";
        var index = text.IndexOf("Idle", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "Idle".Length, LintMode.Procedure);

        // Assert: verify expected behavior
        Assert.Null(result);
    }


    /// <summary>
    ///     Test that the same sentence-start text in Descriptive mode does not trigger the
    ///     imperative signal, resolving as inconclusive when no other signal fires.
    /// </summary>
    [Fact]
    public void Guess_SentenceStartInDescriptiveMode_ReturnsNull()
    {
        // Arrange: identical structure as the Procedure-mode test, but Descriptive mode, with the
        // match followed by a neutral word (not an article) so only the sentence-start/mode
        // behavior is exercised; a finite verb elsewhere in the segment keeps the new
        // verbless-segment noun signal from firing.
        const string text = "Function occurs regularly. The system operates continuously.";
        var index = text.IndexOf("Function", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "Function".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Null(result);
    }

    /// <summary>
    ///     Test that a term preceded by an article ("the") is guessed as a noun.
    /// </summary>
    [Fact]
    public void Guess_PrecededByArticle_ReturnsNoun()
    {
        // Arrange: "the impact" - article signal
        const string text = "The impact was significant.";
        var index = text.IndexOf("impact", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impact".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test that a term preceded by a possessive pronoun ("its") is guessed as a noun.
    /// </summary>
    [Fact]
    public void Guess_PrecededByPossessive_ReturnsNoun()
    {
        // Arrange: "its impact" - possessive signal
        const string text = "Review its impact carefully.";
        var index = text.IndexOf("impact", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impact".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test that a term preceded by a quantifier/demonstrative ("this") is guessed as a noun.
    /// </summary>
    [Fact]
    public void Guess_PrecededByQuantifier_ReturnsNoun()
    {
        // Arrange: "this impact" - quantifier/demonstrative signal
        const string text = "This impact matters.";
        var index = text.IndexOf("impact", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impact".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test that a term preceded by a preposition ("of") is guessed as a noun.
    /// </summary>
    [Fact]
    public void Guess_PrecededByPreposition_ReturnsNoun()
    {
        // Arrange: "of impact" - preposition signal
        const string text = "Consider the size of impact on the system.";
        var index = text.IndexOf("impact", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impact".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test that a plural "-s" suffix (not "-ss") is guessed as a noun.
    /// </summary>
    [Fact]
    public void Guess_PluralSuffix_ReturnsNoun()
    {
        // Arrange: "impacts" - plural noun suffix signal, preceded by a non-signal word
        const string text = "Several impacts appeared later.";
        var index = text.IndexOf("impacts", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impacts".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test that a term followed by "of" is guessed as a noun (noun-phrase continuation).
    /// </summary>
    [Fact]
    public void Guess_FollowedByOf_ReturnsNoun()
    {
        // Arrange: "impact of" - noun-phrase continuation signal, with a neutral preceding word
        const string text = "Consider impact of weather on delays.";
        var index = text.IndexOf("impact", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impact".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test that conflicting signals (both a verb signal and a noun signal fire) resolve as
    ///     inconclusive.
    /// </summary>
    [Fact]
    public void Guess_ConflictingSignals_ReturnsNull()
    {
        // Arrange: "to impacts" - infinitive marker (verb) and plural suffix (noun) both fire
        const string text = "Plan to impacts on the results.";
        var index = text.IndexOf("impacts", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impacts".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Null(result);
    }

    /// <summary>
    ///     Test that no signals firing at all (a bare word mid-sentence with no context) resolves
    ///     as inconclusive.
    /// </summary>
    [Fact]
    public void Guess_NoSignals_ReturnsNull()
    {
        // Arrange: "impact" with no preceding or following signal word; a finite verb elsewhere
        // in the segment ("operates") keeps the new verbless-segment noun signal from firing, so
        // only the no-signal path for the match itself is exercised.
        const string text = "Reports impact daily readers. The system operates continuously.";
        var index = text.IndexOf("impact", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impact".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Null(result);
    }

    /// <summary>
    ///     Test that a determiner still governs the match as a noun when a participle modifier
    ///     ("metering") sits between the determiner and the match.
    /// </summary>
    [Fact]
    public void Guess_ArticleThenParticipleModifier_ReturnsNoun()
    {
        // Arrange: "The metering impact moves" - article governs through the participle modifier
        const string text = "The metering impact moves to the tower.";
        var index = text.IndexOf("impact", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impact".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test that a determiner still governs the match as a noun when a capitalized
    ///     proper-noun modifier (a product name) sits between the determiner and the match.
    /// </summary>
    [Fact]
    public void Guess_ArticleThenProperNounModifier_ReturnsNoun()
    {
        // Arrange: "The Vantage impact moves" - article governs through the proper-noun modifier
        const string text = "The Vantage impact moves to the tower.";
        var index = text.IndexOf("impact", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impact".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test that a determiner still governs the match as a noun when an adjective modifier
    ///     ("custom") sits between the determiner and the match.
    /// </summary>
    [Fact]
    public void Guess_ArticleThenAdjectiveModifier_ReturnsNoun()
    {
        // Arrange: "Install a custom impact" - article governs through the adjective modifier
        const string text = "Install a custom impact in the holder.";
        var index = text.IndexOf("impact", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impact".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test that the modifier-scan-back distance is bounded: an unrelated determiner earlier
    ///     in the sentence, separated by more than the maximum modifier scan distance, does not
    ///     falsely resolve the match as a noun.
    /// </summary>
    [Fact]
    public void Guess_DeterminerBeyondMaxScanDistance_DoesNotGovern()
    {
        // Arrange: five unrelated words separate "the" from "impact", exceeding the scan bound.
        // A trailing finite verb sentence keeps the verbless-segment noun signal from firing, so
        // only the determiner-scan-distance behavior is exercised.
        const string text = "Reports show the metering custom coated impact daily. The pump operates.";
        var index = text.IndexOf("impact", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impact".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Null(result);
    }

    /// <summary>
    ///     Test that a term followed by a finite verb form ("moves") is guessed as a noun,
    ///     because the match is the subject of the clause.
    /// </summary>
    [Fact]
    public void Guess_FollowedByFiniteVerb_ReturnsNoun()
    {
        // Arrange: "impact moves" - the match is the subject, preceded by a neutral word so only
        // the following-finite-verb signal is exercised.
        const string text = "Consider impact moves quickly.";
        var index = text.IndexOf("impact", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impact".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test that a match in a verbless table-cell fragment resolves as a noun, because no
    ///     finite verb appears anywhere in the segment (the verbless-segment signal).
    /// </summary>
    [Fact]
    public void Guess_VerblessTableCellFragment_ReturnsNoun()
    {
        // Arrange: "Wash pump" - a two-word noun-phrase cell with no finite verb anywhere
        const string text = "Wash pump";
        var index = text.IndexOf("pump", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "pump".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test that a match in a verbless list-item fragment resolves as a noun.
    /// </summary>
    [Fact]
    public void Guess_VerblessListItemFragment_ReturnsNoun()
    {
        // Arrange: "Probe geometry and diameter." has no finite verb anywhere
        const string text = "Probe geometry and diameter.";
        var index = text.IndexOf("Probe", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "Probe".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test that a bare comma-separated list fragment with no finite verb resolves each
    ///     dictionary term as a noun.
    /// </summary>
    [Fact]
    public void Guess_VerblessCommaSeparatedList_ReturnsNoun()
    {
        // Arrange: "Holds. Metering device, probe, coupling, fluid." - no finite verb anywhere
        const string text = "Holds. Metering device, probe, coupling, fluid.";
        var index = text.IndexOf("probe", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "probe".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test that a verbless heading fragment resolves the match as a noun.
    /// </summary>
    [Fact]
    public void Guess_VerblessHeadingFragment_ReturnsNoun()
    {
        // Arrange: "Wash Tower" - a heading fragment with no finite verb anywhere
        const string text = "Wash Tower";
        var index = text.IndexOf("Wash", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "Wash".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test that the verbless-segment noun signal does not out-vote a strong match-local verb
    ///     signal: preceded by "to" (infinitive marker) still resolves as a verb even though the
    ///     segment otherwise contains no other finite verb.
    /// </summary>
    [Fact]
    public void Guess_VerblessSegmentButPrecededByTo_ReturnsVerb()
    {
        // Arrange: "to wash" inside an otherwise verbless fragment
        const string text = "Instructions to wash pump parts.";
        var index = text.IndexOf("wash", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "wash".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Verb, result);
    }

    /// <summary>
    ///     Test that an imperative sentence start in Procedure mode still resolves as a verb even
    ///     when the segment otherwise contains no finite verb - the imperative signal is not
    ///     suppressed by the verbless-segment noun signal (they only conflict to null, they do not
    ///     let the noun signal win outright).
    /// </summary>
    [Fact]
    public void Guess_ImperativeInVerblessSegment_ReturnsVerb()
    {
        // Arrange: "Wash the probe." - an imperative instruction with no other finite verb
        const string text = "Wash the probe.";
        var index = text.IndexOf("Wash", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "Wash".Length, LintMode.Procedure);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Verb, result);
    }

    /// <summary>
    ///     Test that a segment containing a finite verb elsewhere does not trigger the
    ///     verbless-segment signal, so a match with no other signal remains ambiguous.
    /// </summary>
    [Fact]
    public void Guess_SegmentHasFiniteVerbElsewhere_MatchStillAmbiguous()
    {
        // Arrange: "pump" has no local signal (followed by "often", an adverb, not a compound-noun
        // head), but "operates" elsewhere is a recognized finite verb
        const string text = "Consider pump often. The system operates continuously.";
        var index = text.IndexOf("pump", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "pump".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Null(result);
    }

    /// <summary>
    ///     Test that a match immediately followed by another noun (a noun-noun compound) resolves
    ///     as a noun, even when a finite verb appears elsewhere in the segment (so the
    ///     verbless-segment signal alone would not have caught it).
    /// </summary>
    [Fact]
    public void Guess_FollowedByCompoundNoun_ReturnsNoun()
    {
        // Arrange: "test fixture" - "test" modifies the following noun "fixture"
        const string text = "The breadboard is a laboratory test fixture.";
        var index = text.IndexOf("test", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "test".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test another noun-noun compound shape: "duty cycle", where "cycle" is the head noun of
    ///     the compound modified by "duty".
    /// </summary>
    [Fact]
    public void Guess_HeadOfCompoundNounAfterModifier_ReturnsNoun()
    {
        // Arrange: "duty cycle" - preceded by "the" (article), exercising the compound via the
        // determiner-governs-through-modifiers path for the head noun "cycle" itself is covered
        // elsewhere; here we assert the compound's first word is a noun signal by adjacency.
        const string text = "Pump rate is set by the duty cycle.";
        var index = text.IndexOf("duty", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "duty".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test that the noun-compound signal does not fire when the following word is itself a
    ///     verb-signal-bearing word (for example an article), so genuine verb usages such as
    ///     "cover the indicative load" are unaffected.
    /// </summary>
    [Fact]
    public void Guess_FollowedByArticleNotCompoundNoun_ReturnsVerb()
    {
        // Arrange: "cover the load" - "the" following "cover" is a verb signal (direct object),
        // not a compound-noun signal.
        const string text = "One supply is enough to cover the indicative load.";
        var index = text.IndexOf("cover", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "cover".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Verb, result);
    }

    /// <summary>
    ///     Test that a match immediately followed by a number resolves as a verb (the match takes
    ///     the number as its direct object), for example "use 0.12 ohms".
    /// </summary>
    [Fact]
    public void Guess_FollowedByNumber_ReturnsVerb()
    {
        // Arrange: "use 0.12" - "use" is a verb taking the number as its object
        const string text = "Blocks 1 to 5 use 0.12 ohms.";
        var index = text.IndexOf("use", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "use".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Equal(PartOfSpeech.Verb, result);
    }
}
