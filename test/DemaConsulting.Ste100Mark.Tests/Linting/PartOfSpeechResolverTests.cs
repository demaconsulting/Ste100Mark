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
using DemaConsulting.Ste100Mark.Linting;

namespace DemaConsulting.Ste100Mark.Tests.Linting;

using PartOfSpeech = DemaConsulting.Ste100Mark.Linting.PartOfSpeech;
using Upos = DemaConsulting.PartOfSpeech.Upos;

/// <summary>
///     Unit tests for the PartOfSpeechResolver class: scenario tests against the real
///     <c>DemaConsulting.PartOfSpeech</c> tagger (ported from the former heuristic's test suite)
///     and mechanism tests (offsets, threshold, mapping, caching) against a scripted tagging function.
/// </summary>
public class PartOfSpeechResolverTests
{
    /// <summary>
    ///     Scripted tagger: records every sentence it is asked to tag (as word and punctuation tokens)
    ///     and answers from a delegate.
    /// </summary>
    /// <param name="answer">Produces the tag and confidence of each token for the supplied tokens.</param>
    private sealed class ScriptedModel(Func<string[], (Upos Tag, double Confidence)[]> answer)
    {
        /// <summary>Tokenizer used by the scripted tagger: a word or one punctuation character.</summary>
        private static readonly Regex TokenRegex = new(@"[\w'-]+|[^\w\s]", RegexOptions.None, TimeSpan.FromSeconds(1));

        /// <summary>Gets the token lists of every sentence tagged.</summary>
        public List<string[]> Calls { get; } = [];

        /// <summary>
        ///     Converts a scripted tagger to the tagging function used by the resolver.
        /// </summary>
        /// <param name="model">The scripted tagger.</param>
        public static implicit operator Func<string, IReadOnlyList<TaggedSpan>>(ScriptedModel model) => model.Tag;

        /// <summary>
        ///     Tags one sentence.
        /// </summary>
        /// <param name="sentence">The sentence text.</param>
        /// <returns>The tagged tokens with offsets relative to the sentence.</returns>
        public IReadOnlyList<TaggedSpan> Tag(string sentence)
        {
            var matches = TokenRegex.Matches(sentence);
            var tokens = matches.Select(m => m.Value).ToArray();
            lock (Calls)
            {
                Calls.Add(tokens);
            }

            var tags = answer(tokens);
            var result = new List<TaggedSpan>();
            for (var i = 0; i < tags.Length && i < matches.Count; i++)
            {
                result.Add(new TaggedSpan(matches[i].Index, matches[i].Length, tags[i].Tag, tags[i].Confidence, true));
            }

            return result;
        }
    }

    /// <summary>
    ///     Builds a scripted tagger that tags every token as the given part of speech and confidence.
    /// </summary>
    private static ScriptedModel Uniform(Upos pos, double confidence) =>
        new(tokens => tokens.Select(_ => (pos, confidence)).ToArray());

    /// <summary>
    ///     Scenarios where the real tagger resolves the match as a noun.
    /// </summary>
    public static IEnumerable<object[]> NounScenarios() =>
    [
        ["impact", "The impact was significant."],
        ["impact", "Review its impact carefully."],
        ["impact", "This impact matters."],
        ["impact", "Consider the size of impact on the system."],
        ["impacts", "Several impacts appeared later."],
        ["impact", "Install a custom impact in the holder."],
        ["impact", "Consider impact of weather on delays."],
        ["test", "The breadboard is a laboratory test fixture."],
        ["duty", "Pump rate is set by the duty cycle."],
        ["backup", "Schedule the backup modules for every shift."],
        ["port", "Configure the serial port settings before first use."],
        ["use", "Keep the valve in use during the test."],
        ["test", "Check the test fixture for damage."],
        ["test", "The test passed."]
    ];

    /// <summary>
    ///     Scenarios where the real tagger resolves the match as a verb.
    /// </summary>
    public static IEnumerable<object[]> VerbScenarios() =>
    [
        ["impact", "We need to impact the outcome."],
        ["impact", "It must impact the schedule."],
        ["impacting", "The delay is impacting the schedule."],
        ["Wash", "Wash the probe."],
        ["wash", "Instructions to wash pump parts."],
        ["cover", "One supply is enough to cover the indicative load."],
        ["Function", "Function the panel before use."],
        ["Hold", "Hold at the end of the stroke while the column settles"],
        ["Check", "Check the valve before use."]
    ];

    /// <summary>
    ///     Scenarios where the real tagger confidently resolves adjective and adverb usage.
    /// </summary>
    public static IEnumerable<object[]> AdjectiveAndAdverbScenarios() =>
    [
        ["open", "The valve is open."],
        ["open", "Keep the access panel open."],
        ["open", "The protective cover is open."],
        ["open", "Leave the drain port open."],
        ["open", "Verify that the bypass is open."],
        ["open", "The inspection door stays open."],
        ["open", "Ensure the valve is open."],
        ["slowly", "The valve closes slowly."],
        ["carefully", "Adjust the valve carefully."],
        ["correctly", "Install the seal correctly."],
        ["directly", "Connect the hose directly."],
        ["clearly", "Mark the switch clearly."]
    ];

    /// <summary>
    ///     The real tagger's determiner evidence for the ambiguous word "any".
    /// </summary>
    public static IEnumerable<object[]> DeterminerScenarios() =>
    [
        ["Any", "Any valve can fail."]
    ];

    /// <summary>
    ///     Scenarios where the real tagger is below the minimum confidence or chooses another part of speech.
    /// </summary>
    public static IEnumerable<object[]> InconclusiveScenarios() =>
    [
        ["Heading", "Heading:"],
        ["mix", "Check the coolant mix ratio before startup."],
        ["Block", "Block 0 failed the self-test."],
        ["Trigger", "Trigger not issued correctly by the controller."]
    ];

    /// <summary>
    ///     Test that the real tagger resolves noun usage of dual-class words as a noun.
    /// </summary>
    [Theory]
    [MemberData(nameof(NounScenarios))]
    public void Resolve_NounUsage_ReturnsNoun(string term, string sentence)
    {
        // Arrange: a resolver using the real tagger
        var resolver = new PartOfSpeechResolver();

        // Act: resolve the first occurrence of the term
        var result = resolver.Resolve(sentence, sentence.IndexOf(term, StringComparison.Ordinal), term.Length);

        // Assert: confident noun
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test that the real tagger resolves verb usage of dual-class words as a verb.
    /// </summary>
    [Theory]
    [MemberData(nameof(VerbScenarios))]
    public void Resolve_VerbUsage_ReturnsVerb(string term, string sentence)
    {
        // Arrange: a resolver using the real tagger
        var resolver = new PartOfSpeechResolver();

        // Act: resolve the first occurrence of the term
        var result = resolver.Resolve(sentence, sentence.IndexOf(term, StringComparison.Ordinal), term.Length);

        // Assert: confident verb
        Assert.Equal(PartOfSpeech.Verb, result);
    }

    /// <summary>
    ///     Test that confident adjective and adverb usage is mapped to the matching repository role.
    /// </summary>
    [Theory]
    [MemberData(nameof(AdjectiveAndAdverbScenarios))]
    public void Resolve_AdjectiveOrAdverbUsage_ReturnsMatchingRole(string term, string sentence)
    {
        // Arrange: a resolver using the real tagger
        var resolver = new PartOfSpeechResolver();

        // Act: resolve the first occurrence of the term
        var result = resolver.Resolve(sentence, sentence.IndexOf(term, StringComparison.Ordinal), term.Length);

        // Assert: the confident tag maps to the corresponding dictionary role
        Assert.Equal(term == "open" ? PartOfSpeech.Adjective : PartOfSpeech.Adverb, result);
    }

    /// <summary>
    ///     Test that the observed determiner tag for "any" is kept separate from supported
    ///     dictionary sense roles so the checker can suppress a non-role usage.
    /// </summary>
    [Theory]
    [MemberData(nameof(DeterminerScenarios))]
    public void Resolve_DeterminerUsage_ReturnsDeterminerRole(string term, string sentence)
    {
        // Arrange: the real tagger and the token observed in its sentence context
        var resolver = new PartOfSpeechResolver();
        var start = sentence.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        var token = DemaConsulting.PartOfSpeech.Tagger.Default.Tag(sentence)
            .SelectMany(s => s.Tokens)
            .Single(t => t.Start == start && t.Length == term.Length);

        // Act: resolve the determiner occurrence
        var result = resolver.Resolve(sentence, start, term.Length);

        // Assert: determiner output is preserved so the checker can rule out every supported sense
        Assert.Equal(Upos.Det, token.Tag);
        Assert.Equal(PartOfSpeech.Determiner, result);
    }

    /// <summary>
    ///     Test that low-confidence or non-noun/verb answers of the real tagger are inconclusive.
    /// </summary>
    [Theory]
    [MemberData(nameof(InconclusiveScenarios))]
    public void Resolve_BelowThresholdOrOtherTag_ReturnsNull(string term, string sentence)
    {
        // Arrange: a resolver using the real tagger
        var resolver = new PartOfSpeechResolver();

        // Act: resolve the first occurrence of the term
        var result = resolver.Resolve(sentence, sentence.IndexOf(term, StringComparison.Ordinal), term.Length);

        // Assert: inconclusive
        Assert.Null(result);
    }

    /// <summary>
    ///     Test that the real tagger reports a confidence at or above the minimum for the noun and verb
    ///     scenarios, and below it (or a different part of speech) for the inconclusive scenarios.
    /// </summary>
    [Fact]
    public void Tagger_Default_ConfidenceSeparatesResolvedFromInconclusiveScenarios()
    {
        // Arrange: the scenario sets, each tagged directly by the real tagger
        static DemaConsulting.PartOfSpeech.TaggedToken TokenOf(string term, string sentence)
        {
            var start = sentence.IndexOf(term, StringComparison.Ordinal);
            return DemaConsulting.PartOfSpeech.Tagger.Default.Tag(sentence)
                .SelectMany(s => s.Tokens)
                .Single(t => t.Start == start && t.Length == term.Length);
        }

        // Act and Assert: resolved scenarios are confident supported roles
        foreach (var scenario in NounScenarios())
        {
            var token = TokenOf((string)scenario[0], (string)scenario[1]);
            Assert.Equal(Upos.Noun, token.Tag);
            Assert.True(token.Confidence >= PartOfSpeechResolver.MinimumConfidence, (string)scenario[1]);
        }

        foreach (var scenario in VerbScenarios())
        {
            var token = TokenOf((string)scenario[0], (string)scenario[1]);
            Assert.Equal(Upos.Verb, token.Tag);
            Assert.True(token.Confidence >= PartOfSpeechResolver.MinimumConfidence, (string)scenario[1]);
        }

        foreach (var scenario in AdjectiveAndAdverbScenarios())
        {
            var token = TokenOf((string)scenario[0], (string)scenario[1]);
            Assert.Contains(token.Tag, new[] { Upos.Adj, Upos.Adv });
            Assert.True(token.Confidence >= PartOfSpeechResolver.MinimumConfidence, (string)scenario[1]);
        }

        // Act and Assert: inconclusive scenarios are not a confident noun or verb
        foreach (var scenario in InconclusiveScenarios())
        {
            var token = TokenOf((string)scenario[0], (string)scenario[1]);
            var confidentNounOrVerb = token.Tag is Upos.Noun or Upos.Verb
                && token.Confidence >= PartOfSpeechResolver.MinimumConfidence;
            Assert.False(confidentNounOrVerb, (string)scenario[1]);
        }
    }

    /// <summary>
    ///     Test that a match in the second sentence of a segment is resolved from that sentence only
    ///     and its offset is mapped correctly.
    /// </summary>
    [Fact]
    public void Resolve_MatchInSecondSentence_UsesThatSentenceOnly()
    {
        // Arrange: the same word is a noun in sentence one and a verb in sentence two
        var resolver = new PartOfSpeechResolver();
        const string text = "The test passed. You must test the unit.";
        var second = text.LastIndexOf("test", StringComparison.Ordinal);

        // Act: execute the operation being tested
        var first = resolver.Resolve(text, text.IndexOf("test", StringComparison.Ordinal), 4);
        var last = resolver.Resolve(text, second, 4);

        // Assert: each occurrence is resolved in its own sentence context
        Assert.Equal(PartOfSpeech.Noun, first);
        Assert.Equal(PartOfSpeech.Verb, last);
    }

    /// <summary>
    ///     Test that only one sentence is sent to the tagger per call and that the offsets of a
    ///     contraction and a hyphenated word map the match to the right token.
    /// </summary>
    [Fact]
    public void Resolve_ContractionAndHyphen_TokenOffsetsMapToMatch()
    {
        // Arrange: a two-sentence segment with a contraction and a hyphenated word
        var seen = new List<string>();
        var resolver = new PartOfSpeechResolver(sentence =>
        {
            seen.Add(sentence);
            return [new TaggedSpan(0, 5, Upos.Aux, 0.99, true), new TaggedSpan(6, 3, Upos.Verb, 0.99, true)];
        });
        const string text = "Don't use well-known tools, please. Next sentence.";

        // Act: execute the operation being tested
        var result = resolver.Resolve(text, text.IndexOf("use", StringComparison.Ordinal), 3);

        // Assert: exactly one call with only the first sentence, and the offsets map to the verb
        Assert.Equal("Don't use well-known tools, please.", Assert.Single(seen));
        Assert.Equal(PartOfSpeech.Verb, result);
    }

    /// <summary>
    ///     Test that the real tagger handles a contraction and a hyphenated word before the match.
    /// </summary>
    [Fact]
    public void Resolve_ContractionAndHyphenWithRealTagger_ResolvesMatch()
    {
        // Arrange: the real tagger splits the contraction into two tokens
        var resolver = new PartOfSpeechResolver();
        const string text = "Don't use well-known tools, please.";

        // Act: resolve a verb and a noun after the contraction
        var verb = resolver.Resolve(text, text.IndexOf("use", StringComparison.Ordinal), 3);
        var noun = resolver.Resolve(text, text.IndexOf("tools", StringComparison.Ordinal), 5);

        // Assert: offsets map to the right tokens
        Assert.Equal(PartOfSpeech.Verb, verb);
        Assert.Equal(PartOfSpeech.Noun, noun);
    }

    /// <summary>
    ///     Test that a repeated word resolves the occurrence at the requested offset.
    /// </summary>
    [Fact]
    public void Resolve_RepeatedWord_ResolvesRequestedOccurrence()
    {
        // Arrange: tagger answers Verb only for the second "run" token
        var seen = 0;
        (Upos Tag, double Confidence) TagToken(string t)
        {
            if (t != "run")
            {
                return (Upos.Adv, 0.9);
            }

            seen++;
            return (seen == 2 ? Upos.Verb : Upos.Noun, 0.99);
        }

        var model = new ScriptedModel(tokens => tokens.Select(TagToken).ToArray());
        var resolver = new PartOfSpeechResolver(model);
        const string text = "A run can run.";

        // Act: resolve both occurrences
        var first = resolver.Resolve(text, 2, 3);
        var second = resolver.Resolve(text, 10, 3);

        // Assert: occurrences map to their own tokens
        Assert.Equal(PartOfSpeech.Noun, first);
        Assert.Equal(PartOfSpeech.Verb, second);
    }

    /// <summary>
    ///     Test that confidence below the minimum is inconclusive and confidence at the minimum is accepted.
    /// </summary>
    [Fact]
    public void Resolve_ConfidenceThreshold_BoundaryIsInclusive()
    {
        // Arrange: one resolver just below the threshold and one exactly at it
        var below = new PartOfSpeechResolver(Uniform(Upos.Noun, PartOfSpeechResolver.MinimumConfidence - 0.01));
        var at = new PartOfSpeechResolver(Uniform(Upos.Noun, PartOfSpeechResolver.MinimumConfidence));

        // Act: execute the operation being tested
        var belowResult = below.Resolve("The test passed.", 4, 4);
        var atResult = at.Resolve("The test passed.", 4, 4);

        // Assert: below is inconclusive, at the threshold is accepted
        Assert.Null(belowResult);
        Assert.Equal(PartOfSpeech.Noun, atResult);
    }

    /// <summary>
    ///     Test that the minimum confidence is the strict threshold documented by the beta.3 package.
    /// </summary>
    [Fact]
    public void MinimumConfidence_IsPackageStrictThreshold()
    {
        // Arrange/Act/Assert: the constant is the strict threshold
        Assert.Equal(0.90, PartOfSpeechResolver.MinimumConfidence);
    }

    /// <summary>
    ///     Test that a confident noun, verb or proper-noun tag on a word that the lexicon does not know is inconclusive.
    /// </summary>
    [Theory]
    [InlineData(Upos.Noun)]
    [InlineData(Upos.Verb)]
    [InlineData(Upos.Propn)]
    public void Resolve_UnknownWordNounVerbOrPropnTag_ReturnsNull(Upos pos)
    {
        // Arrange: a confident tag on a word that the lexicon does not know
        var resolver = new PartOfSpeechResolver(_ => [new TaggedSpan(4, 4, pos, 0.99, false)]);

        // Act: execute the operation being tested
        var result = resolver.Resolve("The test passed.", 4, 4);

        // Assert: inconclusive although the confidence is above the threshold
        Assert.Null(result);
    }

    /// <summary>
    ///     Test that a confident adjective tag on a word that the lexicon does not know still resolves,
    ///     because only noun, verb, and proper-noun tags of unknown words are guesses.
    /// </summary>
    [Fact]
    public void Resolve_UnknownWordAdjectiveTag_ReturnsAdjective()
    {
        // Arrange: a confident adjective tag on a word that the lexicon does not know
        var resolver = new PartOfSpeechResolver(_ => [new TaggedSpan(4, 4, Upos.Adj, 0.99, false)]);

        // Act: execute the operation being tested
        var result = resolver.Resolve("The test passed.", 4, 4);

        // Assert: the adjective answer is kept
        Assert.Equal(PartOfSpeech.Adjective, result);
    }

    /// <summary>
    ///     Test that the real tagger's confident noun tag on a word missing from its lexicon is
    ///     inconclusive, while a known noun of the same sentence still resolves.
    /// </summary>
    [Fact]
    public void Resolve_UnknownWordWithRealTagger_ReturnsNullAndKnownWordResolves()
    {
        // Arrange: "turbopump" is not in the lexicon; "damage" is
        var resolver = new PartOfSpeechResolver();
        const string text = "Inspect the turbopump for damage.";
        var unknownStart = text.IndexOf("turbopump", StringComparison.Ordinal);
        var knownStart = text.IndexOf("damage", StringComparison.Ordinal);
        var tokens = DemaConsulting.PartOfSpeech.Tagger.Default.Tag(text).SelectMany(s => s.Tokens).ToList();
        var unknownToken = tokens.Single(t => t.Start == unknownStart);
        var knownToken = tokens.Single(t => t.Start == knownStart);

        // Act: resolve both words
        var unknown = resolver.Resolve(text, unknownStart, "turbopump".Length);
        var known = resolver.Resolve(text, knownStart, "damage".Length);

        // Assert: both are confident nouns to the tagger, but only the known word resolves
        Assert.False(unknownToken.IsKnownWord);
        Assert.Equal(Upos.Noun, unknownToken.Tag);
        Assert.True(unknownToken.Confidence >= PartOfSpeechResolver.MinimumConfidence);
        Assert.True(knownToken.IsKnownWord);
        Assert.True(knownToken.Confidence >= PartOfSpeechResolver.MinimumConfidence);
        Assert.Null(unknown);
        Assert.Equal(PartOfSpeech.Noun, known);
    }

    /// <summary>
    ///     Test that unsupported parts of speech other than the preserved determiner role map to
    ///     inconclusive.
    /// </summary>
    [Theory]
    [InlineData(Upos.Aux)]
    [InlineData(Upos.Adp)]
    public void Resolve_UnsupportedPartOfSpeech_ReturnsNull(Upos pos)
    {
        // Arrange: a confident tagger answer of the given part of speech
        var resolver = new PartOfSpeechResolver(Uniform(pos, 0.99));

        // Act: execute the operation being tested
        var result = resolver.Resolve("The test passed.", 4, 4);

        // Assert: inconclusive
        Assert.Null(result);
    }

    /// <summary>
    ///     Test that a match no token covers is inconclusive.
    /// </summary>
    [Fact]
    public void Resolve_NoTokensCoverMatch_ReturnsNull()
    {
        // Arrange: a tagger that returns no tokens at all
        var resolver = new PartOfSpeechResolver(_ => []);

        // Act: execute the operation being tested
        var result = resolver.Resolve("The test passed.", 4, 4);

        // Assert: inconclusive
        Assert.Null(result);
    }

    /// <summary>
    ///     Test that a match spanning more than one token (a multi-word term) is inconclusive.
    /// </summary>
    [Fact]
    public void Resolve_MultiWordMatch_ReturnsNull()
    {
        // Arrange: a confident noun for every token
        var resolver = new PartOfSpeechResolver(Uniform(Upos.Noun, 0.99));
        const string text = "Do this prior to the test.";

        // Act: execute the operation being tested
        var result = resolver.Resolve(text, text.IndexOf("prior to", StringComparison.Ordinal), "prior to".Length);

        // Assert: inconclusive
        Assert.Null(result);
    }

    /// <summary>
    ///     Test that repeated resolution within one sentence classifies the sentence only once.
    /// </summary>
    [Fact]
    public void Resolve_SameSentenceTwice_ClassifiesOnce()
    {
        // Arrange: a scripted model counting calls
        var model = Uniform(Upos.Noun, 0.99);
        var resolver = new PartOfSpeechResolver(model);
        const string text = "The test passed.";

        // Act: resolve two different tokens of the same sentence
        resolver.Resolve(text, 4, 4);
        resolver.Resolve(text, 9, 6);

        // Assert: the model was asked once
        Assert.Single(model.Calls);
    }

    /// <summary>
    ///     Test that concurrent resolution of the same uncached sentence classifies it only once.
    /// </summary>
    [Fact]
    public async Task Resolve_ConcurrentSameSentence_ClassifiesOnce()
    {
        // Arrange: a slow scripted model counting classification calls
        var calls = 0;
        using var gate = new ManualResetEventSlim(false);
        var model = new ScriptedModel(tokens =>
        {
            Interlocked.Increment(ref calls);
            gate.Wait(TimeSpan.FromSeconds(5));
            return tokens.Select(_ => (Upos.Noun, 0.99)).ToArray();
        });
        var resolver = new PartOfSpeechResolver(model);
        const string text = "The test passed.";

        // Act: resolve the same sentence from many threads, holding the first classification open
        using var started = new CountdownEvent(16);
        var tasks = Enumerable.Range(0, 16)
            .Select(_ => Task.Factory.StartNew(
                () =>
                {
                    started.Signal();
                    return resolver.Resolve(text, 4, 4);
                },
                TaskCreationOptions.LongRunning))
            .ToArray();
        started.Wait(TestContext.Current.CancellationToken);
        gate.Set();
        var results = await Task.WhenAll(tasks);

        // Assert: one classification served every caller
        Assert.Equal(1, calls);
        Assert.All(results, r => Assert.Equal(PartOfSpeech.Noun, r));
    }

    /// <summary>
    ///     Test that a lowercase continuation after an abbreviation does not start a new sentence,
    ///     matching the sentence analyzer's boundary rule.
    /// </summary>
    [Fact]
    public void Resolve_LowercaseContinuationAfterPeriod_StaysInSameSentence()
    {
        // Arrange: a scripted model and an abbreviation followed by a lowercase continuation
        var model = Uniform(Upos.Verb, 0.99);
        var resolver = new PartOfSpeechResolver(model);
        const string text = "Use e.g. test the valve.";

        // Act: resolve "test"
        var result = resolver.Resolve(text, text.IndexOf("test", StringComparison.Ordinal), 4);

        // Assert: the whole text is one sentence classified with its full context
        Assert.Equal(PartOfSpeech.Verb, result);
        var call = Assert.Single(model.Calls);
        Assert.Equal(["Use", "e", ".", "g", ".", "test", "the", "valve", "."], call);
    }

    /// <summary>
    ///     Test that a null segment text is rejected.
    /// </summary>
    [Fact]
    public void Resolve_NullSegmentText_ThrowsArgumentNullException()
    {
        // Arrange: a resolver
        var resolver = new PartOfSpeechResolver(Uniform(Upos.Noun, 0.99));

        // Act / Assert: a null argument throws
        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!, 0, 1));
    }

    /// <summary>
    ///     Test that a match outside the segment text is rejected.
    /// </summary>
    [Fact]
    public void Resolve_MatchOutsideText_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: a resolver
        var resolver = new PartOfSpeechResolver(Uniform(Upos.Noun, 0.99));

        // Act / Assert: an out-of-range match throws
        Assert.Throws<ArgumentOutOfRangeException>(() => resolver.Resolve("short", 3, 10));
    }

    /// <summary>
    ///     Test that a match length large enough to overflow an int sum is rejected like any other
    ///     out-of-range match.
    /// </summary>
    [Fact]
    public void Resolve_MatchLengthIntMaxValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: a resolver
        var resolver = new PartOfSpeechResolver(Uniform(Upos.Noun, 0.99));

        // Act / Assert: index + length would overflow, but the range is still rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => resolver.Resolve("short", 3, int.MaxValue));
    }

    /// <summary>
    ///     Test that cache eviction at capacity never drops an in-flight classification, so a concurrent
    ///     lookup of that sentence still shares it.
    /// </summary>
    [Fact]
    public async Task Resolve_EvictionWhileInFlight_ClassifiesInFlightSentenceOnce()
    {
        // Arrange: capacity 2; the model blocks only on the "Gate" sentence and counts its calls
        var gateCalls = 0;
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var model = new ScriptedModel(tokens =>
        {
            if (tokens[0] == "Gate")
            {
                Interlocked.Increment(ref gateCalls);
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(5));
            }

            return tokens.Select(_ => (Upos.Noun, 0.99)).ToArray();
        });
        var resolver = new PartOfSpeechResolver(model, 2);
        resolver.Resolve("Alpha one.", 0, 5);
        resolver.Resolve("Beta two.", 0, 4);

        // Act: start the gate sentence (evicts the completed entries), then add a new key while it is
        // in flight, then look the gate sentence up again before releasing it
        var first = Task.Factory.StartNew(
            () => resolver.Resolve("Gate one.", 0, 4),
            TaskCreationOptions.LongRunning);
        entered.Wait(TestContext.Current.CancellationToken);
        resolver.Resolve("Delta four.", 0, 5);
        resolver.Resolve("Echo five.", 0, 4);
        Assert.True(resolver.IsSentenceCached("Gate one."));
        release.Set();
        var firstResult = await first;
        var secondResult = resolver.Resolve("Gate one.", 0, 4);

        // Assert: the in-flight entry survived eviction, so the later lookup reused it
        Assert.Equal(1, gateCalls);
        Assert.Equal(PartOfSpeech.Noun, firstResult);
        Assert.Equal(PartOfSpeech.Noun, secondResult);
    }

    /// <summary>
    ///     Test that exceeding capacity by one evicts a single completed entry and keeps the rest cached.
    /// </summary>
    [Fact]
    public void Resolve_CapacityExceededByOne_EvictsOnlyOneEntry()
    {
        // Arrange: capacity 3, filled with three sentences
        var model = Uniform(Upos.Noun, 0.99);
        var resolver = new PartOfSpeechResolver(model, 3);
        string[] sentences = ["Alpha one.", "Beta two.", "Gamma three."];
        foreach (var s in sentences)
        {
            resolver.Resolve(s, 0, 4);
        }

        // Act: add a fourth sentence
        resolver.Resolve("Delta four.", 0, 5);

        // Assert: only one earlier entry was dropped; the new one and the other two are still cached
        Assert.Equal(3, resolver.CachedSentenceCount);
        Assert.True(resolver.IsSentenceCached("Delta four."));
        var survivors = sentences.Where(resolver.IsSentenceCached).ToArray();
        Assert.Equal(2, survivors.Length);

        // Act: resolve the survivors again
        var callsBefore = model.Calls.Count;
        foreach (var s in survivors)
        {
            resolver.Resolve(s, 0, 4);
        }

        // Assert: they were served from the cache without reclassification
        Assert.Equal(callsBefore, model.Calls.Count);
    }

    /// <summary>
    ///     Test that concurrent misses of distinct sentences keep the cache within its capacity plus the
    ///     number of in-flight entries (in-flight entries are never evicted).
    /// </summary>
    [Fact]
    public async Task Resolve_ConcurrentMisses_CacheStaysWithinCapacityPlusInFlight()
    {
        // Arrange: capacity 2, pre-filled with completed entries; many threads released together
        const int capacity = 2;
        const int threads = 16;
        using var entered = new CountdownEvent(threads);
        var blocking = false;
        var countWhileInFlight = 0;
        PartOfSpeechResolver resolver = null!;
        var model = new ScriptedModel(tokens =>
        {
            // Hold every classification in flight until all workers have entered the model
            if (Volatile.Read(ref blocking))
            {
                entered.Signal();
                entered.Wait(TimeSpan.FromSeconds(10));
                var observed = resolver.CachedSentenceCount;
                lock (entered)
                {
                    countWhileInFlight = Math.Max(countWhileInFlight, observed);
                }
            }

            return tokens.Select(_ => (Upos.Noun, 0.99)).ToArray();
        });
        resolver = new PartOfSpeechResolver(model, capacity);
        resolver.Resolve("Alpha one.", 0, 5);
        resolver.Resolve("Beta two.", 0, 4);
        Volatile.Write(ref blocking, true);
        using var barrier = new Barrier(threads);
        var active = 0;
        var peak = 0;

        // Act: every thread resolves its own new sentence at the same moment
        var tasks = Enumerable.Range(0, threads).Select(i => Task.Factory.StartNew(
            () =>
            {
                barrier.SignalAndWait(TimeSpan.FromSeconds(10));
                lock (barrier)
                {
                    active++;
                    peak = Math.Max(peak, active);
                }

                resolver.Resolve($"Word{i} text.", 0, 4);
                lock (barrier)
                {
                    active--;
                }
            },
            TaskCreationOptions.LongRunning)).ToArray();
        await Task.WhenAll(tasks);

        // Assert: in-flight entries may transiently exceed capacity, but once all classifications
        // have completed the cache has been trimmed back to its capacity
        Assert.True(peak >= 1);
        Assert.True(
            countWhileInFlight > capacity,
            $"Cache held {countWhileInFlight} entries while all calls were in flight; expected more than {capacity}.");
        Assert.True(
            resolver.CachedSentenceCount <= capacity,
            $"Cache holds {resolver.CachedSentenceCount} entries; capacity is {capacity}.");
    }

    /// <summary>
    ///     Test that a null tagging function is rejected by the constructor.
    /// </summary>
    [Fact]
    public void Constructor_NullTagger_ThrowsArgumentNullException()
    {
        // Act / Assert: a null tagging function throws
        Assert.Throws<ArgumentNullException>(() => new PartOfSpeechResolver((Func<string, IReadOnlyList<TaggedSpan>>)null!));
    }

    /// <summary>
    ///     Test that the shared default resolver is a single instance.
    /// </summary>
    [Fact]
    public void Shared_ReturnsSameInstance()
    {
        // Act / Assert: the process-wide resolver is created once
        Assert.Same(PartOfSpeechResolver.Shared, PartOfSpeechResolver.Shared);
    }

    /// <summary>
    ///     Test that repeated and parallel resolution of the same match with the real tagger always
    ///     gives the same answer.
    /// </summary>
    [Fact]
    public void Resolve_RepeatedAndConcurrentQueries_ReturnSameResult()
    {
        // Arrange: a resolver using the real tagger and a match with a confident verb answer
        var resolver = new PartOfSpeechResolver();
        const string text = "Wash the probe.";

        // Act: resolve the same match repeatedly and from several threads
        var first = resolver.Resolve(text, 0, 4);
        var results = new PartOfSpeech?[32];
        Parallel.For(0, results.Length, i => results[i] = resolver.Resolve(text, 0, 4));

        // Assert: every answer equals the first
        Assert.Equal(PartOfSpeech.Verb, first);
        Assert.All(results, r => Assert.Equal(first, r));
    }

    /// <summary>
    ///     Builds a package glossary with one noun entry in the given mode.
    /// </summary>
    private static DemaConsulting.PartOfSpeech.Glossary BuildGlossary(
        string term,
        DemaConsulting.PartOfSpeech.GlossaryMode mode,
        Upos tag = Upos.Noun) =>
        DemaConsulting.PartOfSpeech.Glossary.Create(
            [new DemaConsulting.PartOfSpeech.GlossaryEntry(term, tag)],
            new DemaConsulting.PartOfSpeech.GlossaryOptions { Mode = mode });

    /// <summary>
    ///     Test that a hard glossary noun for a made-up word, which the lexicon does not know, resolves
    ///     as a noun, because a constrained token counts as known.
    /// </summary>
    [Fact]
    public void Resolve_GlossaryUnknownWordNoun_ResolvesNounInHardMode()
    {
        // Arrange: a hard glossary naming a made-up word as a noun
        const string text = "Wash the zorbix.";
        var plain = new PartOfSpeechResolver();
        var glossary = new PartOfSpeechResolver(
            BuildGlossary("zorbix", DemaConsulting.PartOfSpeech.GlossaryMode.Hard));

        // Act
        var withoutGlossary = plain.Resolve(text, text.IndexOf("zorbix", StringComparison.Ordinal), 6);
        var withGlossary = glossary.Resolve(text, text.IndexOf("zorbix", StringComparison.Ordinal), 6);

        // Assert: only the glossary yields a confident noun
        Assert.Null(withoutGlossary);
        Assert.Equal(PartOfSpeech.Noun, withGlossary);
    }

    /// <summary>
    ///     Test that a soft glossary entry also counts its matched token as known, so the glossary tag
    ///     is not discarded by the unknown-word rule.
    /// </summary>
    [Fact]
    public void Resolve_SoftGlossaryMatchedToken_CountsAsKnownWord()
    {
        // Arrange: a soft glossary naming a made-up word as a noun
        const string text = "Wash the zorbix.";
        var resolver = new PartOfSpeechResolver(
            BuildGlossary("zorbix", DemaConsulting.PartOfSpeech.GlossaryMode.Soft));

        // Act
        var result = resolver.Resolve(text, text.IndexOf("zorbix", StringComparison.Ordinal), 6);

        // Assert: the matched token is constrained, hence known, hence resolved
        Assert.Equal(PartOfSpeech.Noun, result);
    }

    /// <summary>
    ///     Test that two resolvers built for different glossaries do not share cached results.
    /// </summary>
    [Fact]
    public void Resolve_TwoGlossaries_SameSentence_DoNotShareCache()
    {
        // Arrange: the same sentence, one glossary saying noun and one saying verb
        const string text = "Zorbix the panel.";
        var nounResolver = new PartOfSpeechResolver(
            BuildGlossary("zorbix", DemaConsulting.PartOfSpeech.GlossaryMode.Hard));
        var verbResolver = new PartOfSpeechResolver(
            BuildGlossary("zorbix", DemaConsulting.PartOfSpeech.GlossaryMode.Hard, Upos.Verb));

        // Act
        var asNoun = nounResolver.Resolve(text, 0, 6);
        var asVerb = verbResolver.Resolve(text, 0, 6);

        // Assert: each glossary decides its own answer
        Assert.Equal(PartOfSpeech.Noun, asNoun);
        Assert.Equal(PartOfSpeech.Verb, asVerb);
    }

    /// <summary>
    ///     Test that a resolver without a glossary behaves as before for an unknown word.
    /// </summary>
    [Fact]
    public void Resolve_NoGlossary_BehaviorUnchanged()
    {
        // Arrange / Act: an unknown made-up word without any glossary
        const string text = "Wash the zorbix.";
        var result = new PartOfSpeechResolver().Resolve(text, text.IndexOf("zorbix", StringComparison.Ordinal), 6);

        // Assert: the unknown-word rule makes the answer inconclusive
        Assert.Null(result);
    }

    /// <summary>
    ///     Test that a null glossary is rejected by the constructor.
    /// </summary>
    [Fact]
    public void Constructor_NullGlossary_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
            new PartOfSpeechResolver((DemaConsulting.PartOfSpeech.Glossary)null!));
    }

    /// <summary>
    ///     Test that a confident token yields a resolved outcome with its margin, runner-up and flags.
    /// </summary>
    [Fact]
    public void ResolveWithEvidence_ConfidentToken_ReportsTaggerEvidence()
    {
        // Arrange: a confident verb token with a runner-up reading
        var resolver = new PartOfSpeechResolver(_ =>
            [new TaggedSpan(4, 4, Upos.Verb, 0.97, true, 0.90, Upos.Noun, 0.07, false)]);

        // Act: resolve with evidence
        var evidence = resolver.ResolveWithEvidence("The test passed.", 4, 4);

        // Assert: the evidence carries every tagger field
        Assert.Equal(PartOfSpeech.Verb, evidence.Role);
        Assert.Equal("resolved", evidence.Outcome);
        Assert.Equal("verb", evidence.Tag);
        Assert.Equal(0.97, evidence.Confidence);
        Assert.Equal(0.90, evidence.Margin);
        Assert.True(evidence.IsKnownWord);
        Assert.False(evidence.IsConstrained);
        Assert.Equal("noun", evidence.RunnerUpTag);
        Assert.Equal(0.07, evidence.RunnerUpProbability);
        Assert.True(evidence.ThresholdMet);
    }

    /// <summary>
    ///     Test that each inconclusive cause maps to its own outcome.
    /// </summary>
    [Theory]
    [InlineData(Upos.Verb, 0.50, true, "below-threshold")]
    [InlineData(Upos.Verb, 0.99, false, "unknown-word")]
    [InlineData(Upos.Adp, 0.99, true, "unsupported-tag")]
    public void ResolveWithEvidence_InconclusiveToken_ReportsOutcome(Upos tag, double confidence, bool known, string expected)
    {
        // Arrange: one token with the given tag, confidence and lexicon status
        var resolver = new PartOfSpeechResolver(_ => [new TaggedSpan(4, 4, tag, confidence, known)]);

        // Act: resolve with evidence
        var evidence = resolver.ResolveWithEvidence("The test passed.", 4, 4);

        // Assert: no role, and the outcome names the cause
        Assert.Null(evidence.Role);
        Assert.Equal(expected, evidence.Outcome);
    }

    /// <summary>
    ///     Test that a multi-token match and a match no token covers have their own outcomes.
    /// </summary>
    [Fact]
    public void ResolveWithEvidence_MultiTokenAndNoToken_ReportOutcomes()
    {
        // Arrange: one resolver with a confident token for every word, one with no tokens
        const string text = "Do this prior to the test.";
        var tagged = new PartOfSpeechResolver(Uniform(Upos.Noun, 0.99));
        var empty = new PartOfSpeechResolver(_ => []);

        // Act: resolve a two-word span, and a span with no token
        var multi = tagged.ResolveWithEvidence(text, text.IndexOf("prior to", StringComparison.Ordinal), "prior to".Length);
        var none = empty.ResolveWithEvidence(text, 3, 4);

        // Assert: multi-token and unavailable
        Assert.Equal("multi-token", multi.Outcome);
        Assert.Equal("unavailable", none.Outcome);
    }

    /// <summary>
    ///     Test that Resolve and ResolveWithEvidence agree for the same occurrence.
    /// </summary>
    [Fact]
    public void ResolveWithEvidence_AgreesWithResolve()
    {
        // Arrange: the real tagger on a confident verb use
        var resolver = new PartOfSpeechResolver();
        const string text = "Open the valve now.";

        // Act: resolve both ways
        var role = resolver.Resolve(text, 0, 4);
        var evidence = resolver.ResolveWithEvidence(text, 0, 4);

        // Assert: identical role
        Assert.Equal(role, evidence.Role);
    }

    /// <summary>
    ///     Test that the default interface implementation wraps Resolve with no tagger evidence.
    /// </summary>
    [Fact]
    public void ResolveWithEvidence_DefaultImplementation_WrapsResolve()
    {
        // Arrange: a resolver that only implements Resolve
        IPartOfSpeechResolver resolver = new FixedPartOfSpeechResolver(PartOfSpeech.Noun);
        IPartOfSpeechResolver inconclusive = new FixedPartOfSpeechResolver(null);

        // Act: resolve with evidence through the interface
        var resolved = resolver.ResolveWithEvidence("The valve.", 4, 5);
        var unknown = inconclusive.ResolveWithEvidence("The valve.", 4, 5);

        // Assert: role and outcome only
        Assert.Equal(PartOfSpeech.Noun, resolved.Role);
        Assert.Equal("resolved", resolved.Outcome);
        Assert.Null(resolved.Tag);
        Assert.Null(unknown.Role);
        Assert.Equal("unavailable", unknown.Outcome);
    }
}
