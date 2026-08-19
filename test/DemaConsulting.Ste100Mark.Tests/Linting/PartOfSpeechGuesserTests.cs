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
    ///     Test that the same sentence-start text in Descriptive mode does not trigger the
    ///     imperative signal, resolving as inconclusive when no other signal fires.
    /// </summary>
    [Fact]
    public void Guess_SentenceStartInDescriptiveMode_ReturnsNull()
    {
        // Arrange: identical text/position as the Procedure-mode test, but Descriptive mode
        const string text = "Function the panel before use.";
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
        // Arrange: "impact" with no preceding or following signal word
        const string text = "Reports impact daily readers.";
        var index = text.IndexOf("impact", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var result = PartOfSpeechGuesser.Guess(text, index, "impact".Length, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Null(result);
    }
}
