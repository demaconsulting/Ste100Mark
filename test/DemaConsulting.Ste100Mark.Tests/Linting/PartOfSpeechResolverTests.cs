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
using Ws = DemaConsulting.WordSense;
using WsPos = DemaConsulting.WordSense.PartOfSpeech;

namespace DemaConsulting.Ste100Mark.Tests.Linting;

/// <summary>
///     Unit tests for the PartOfSpeechResolver class: scenario tests against the real
///     <c>DemaConsulting.WordSense.Basic</c> tagger (ported from the former heuristic's test suite)
///     and mechanism tests (tokenization, offsets, threshold, mapping, caching) against a scripted
///     model.
/// </summary>
public class PartOfSpeechResolverTests
{
    /// <summary>
    ///     Scripted tagger model: records every call and answers from a delegate.
    /// </summary>
    /// <param name="answer">Produces the results for the supplied tokens.</param>
    private sealed class ScriptedModel(Func<string[], DemaConsulting.WordSense.WordSense[]> answer) : Ws.IWordSenseModel
    {
        /// <summary>Gets the token lists passed to every call.</summary>
        public List<string[]> Calls { get; } = [];

        /// <inheritdoc />
        public Task EnsureModelAsync(Ws.MirrorOptions? mirror = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        /// <inheritdoc />
        public Task<DemaConsulting.WordSense.WordSense[]> ClassifyAsync(
            IEnumerable<string> words, CancellationToken cancellationToken = default)
        {
            var tokens = words.ToArray();
            lock (Calls)
            {
                Calls.Add(tokens);
            }

            return Task.FromResult(answer(tokens));
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }

    /// <summary>
    ///     Builds a result for one token with the given part of speech and confidence.
    /// </summary>
    private static DemaConsulting.WordSense.WordSense Tag(string word, WsPos pos, double confidence) =>
        new(word, new Ws.WordDetails($"{word}#1", word, pos, string.Empty), confidence);

    /// <summary>
    ///     Builds a scripted model that tags every token as the given part of speech and confidence.
    /// </summary>
    private static ScriptedModel Uniform(WsPos pos, double confidence) =>
        new(tokens => tokens.Select(t => Tag(t, pos, confidence)).ToArray());

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
        ["impact", "The metering impact moves to the tower."],
        ["impact", "Install a custom impact in the holder."],
        ["impact", "Consider impact of weather on delays."],
        ["pump", "Wash pump"],
        ["pump", "Instructions to wash pump parts."],
        ["test", "The breadboard is a laboratory test fixture."],
        ["duty", "Pump rate is set by the duty cycle."],
        ["Block", "Block 0 failed the self-test."],
        ["Trigger", "Trigger not issued correctly by the controller."],
        ["backup", "Schedule the backup modules for every shift."],
        ["mix", "Check the coolant mix ratio before startup."],
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
    ///     Test that the real tagger resolves noun usage of dual-class words as a noun.
    /// </summary>
    [Theory]
    [MemberData(nameof(NounScenarios))]
    public void Resolve_NounUsage_ReturnsNoun(string term, string sentence)
    {
        // Arrange: a resolver using the real Basic tagger
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
        // Arrange: a resolver using the real Basic tagger
        var resolver = new PartOfSpeechResolver();

        // Act: resolve the first occurrence of the term
        var result = resolver.Resolve(sentence, sentence.IndexOf(term, StringComparison.Ordinal), term.Length);

        // Assert: confident verb
        Assert.Equal(PartOfSpeech.Verb, result);
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
    ///     Test that only one sentence is sent to the model per call, tokenized into words and
    ///     punctuation with contractions and hyphenated words kept whole.
    /// </summary>
    [Fact]
    public void Resolve_Tokenization_SendsOneSentenceWithWordAndPunctuationTokens()
    {
        // Arrange: a two-sentence segment with a contraction and a hyphenated word
        var model = Uniform(WsPos.Noun, 0.9);
        var resolver = new PartOfSpeechResolver(model);
        const string text = "Don't use well-known tools, please. Next sentence.";

        // Act: execute the operation being tested
        resolver.Resolve(text, text.IndexOf("tools", StringComparison.Ordinal), 5);

        // Assert: exactly one call with only the first sentence's tokens
        var call = Assert.Single(model.Calls);
        Assert.Equal(["Don't", "use", "well-known", "tools", ",", "please", "."], call);
    }

    /// <summary>
    ///     Test that a repeated word resolves the occurrence at the requested offset.
    /// </summary>
    [Fact]
    public void Resolve_RepeatedWord_ResolvesRequestedOccurrence()
    {
        // Arrange: tagger answers Verb only for the second "run" token
        var seen = 0;
        DemaConsulting.WordSense.WordSense TagToken(string t)
        {
            if (t != "run")
            {
                return Tag(t, WsPos.Adverb, 0.9);
            }

            seen++;
            return Tag(t, seen == 2 ? WsPos.Verb : WsPos.Noun, 0.9);
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
        var below = new PartOfSpeechResolver(Uniform(WsPos.Noun, PartOfSpeechResolver.MinimumConfidence - 0.01));
        var at = new PartOfSpeechResolver(Uniform(WsPos.Noun, PartOfSpeechResolver.MinimumConfidence));

        // Act: execute the operation being tested
        var belowResult = below.Resolve("The test passed.", 4, 4);
        var atResult = at.Resolve("The test passed.", 4, 4);

        // Assert: below is inconclusive, at the threshold is accepted
        Assert.Null(belowResult);
        Assert.Equal(PartOfSpeech.Noun, atResult);
    }

    /// <summary>
    ///     Test that the documented minimum confidence keeps every correct answer observed in the
    ///     corpus (the lowest correct confidence observed was 0.4926).
    /// </summary>
    [Fact]
    public void MinimumConfidence_IsBelowLowestObservedCorrectConfidence()
    {
        // Arrange/Act/Assert: the constant is below the empirical minimum and above zero
        Assert.InRange(PartOfSpeechResolver.MinimumConfidence, 0.01, 0.4926);
    }

    /// <summary>
    ///     Test that parts of speech other than noun and verb map to inconclusive.
    /// </summary>
    [Theory]
    [InlineData(WsPos.Adjective)]
    [InlineData(WsPos.Adverb)]
    [InlineData(WsPos.Auxiliary)]
    [InlineData(WsPos.Determiner)]
    [InlineData(WsPos.Preposition)]
    public void Resolve_NonNounVerbPartOfSpeech_ReturnsNull(WsPos pos)
    {
        // Arrange: a confident tagger answer of the given part of speech
        var resolver = new PartOfSpeechResolver(Uniform(pos, 0.99));

        // Act: execute the operation being tested
        var result = resolver.Resolve("The test passed.", 4, 4);

        // Assert: inconclusive
        Assert.Null(result);
    }

    /// <summary>
    ///     Test that a word the real tagger does not know is inconclusive.
    /// </summary>
    [Fact]
    public void Resolve_UnknownWordWithRealModel_ReturnsNull()
    {
        // Arrange: a resolver using the real Basic tagger and a made-up word it cannot know
        var resolver = new PartOfSpeechResolver();
        const string sentence = "The zzqxorb passed.";

        // Act: resolve the made-up word
        var result = resolver.Resolve(sentence, sentence.IndexOf("zzqxorb", StringComparison.Ordinal), 7);

        // Assert: inconclusive
        Assert.Null(result);
    }

    /// <summary>
    ///     Test that a word the tagger does not know (no details) is inconclusive.
    /// </summary>
    [Fact]
    public void Resolve_UnknownWord_ReturnsNull()
    {
        // Arrange: a tagger that returns no chosen sense
        var model = new ScriptedModel(tokens => tokens.Select(t => new DemaConsulting.WordSense.WordSense(t, null, 0)).ToArray());
        var resolver = new PartOfSpeechResolver(model);

        // Act: execute the operation being tested
        var result = resolver.Resolve("The widget passed.", 4, 6);

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
        var resolver = new PartOfSpeechResolver(Uniform(WsPos.Noun, 0.99));
        const string text = "Do this prior to the test.";

        // Act: execute the operation being tested
        var result = resolver.Resolve(text, text.IndexOf("prior to", StringComparison.Ordinal), "prior to".Length);

        // Assert: inconclusive
        Assert.Null(result);
    }

    /// <summary>
    ///     Test that a model result count that does not match the token count is inconclusive.
    /// </summary>
    [Fact]
    public void Resolve_ModelReturnsWrongResultCount_ReturnsNull()
    {
        // Arrange: a model that returns no results at all
        var resolver = new PartOfSpeechResolver(new ScriptedModel(_ => []));

        // Act: execute the operation being tested
        var result = resolver.Resolve("The test passed.", 4, 4);

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
        var model = Uniform(WsPos.Noun, 0.9);
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
            return tokens.Select(t => Tag(t, WsPos.Noun, 0.9)).ToArray();
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
        var model = Uniform(WsPos.Verb, 0.9);
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
        var resolver = new PartOfSpeechResolver(Uniform(WsPos.Noun, 0.9));

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
        var resolver = new PartOfSpeechResolver(Uniform(WsPos.Noun, 0.9));

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
        var resolver = new PartOfSpeechResolver(Uniform(WsPos.Noun, 0.9));

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

            return tokens.Select(t => Tag(t, WsPos.Noun, 0.9)).ToArray();
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
        var model = Uniform(WsPos.Noun, 0.9);
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

            return tokens.Select(t => Tag(t, WsPos.Noun, 0.9)).ToArray();
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
    ///     Test that a null model is rejected by the constructor.
    /// </summary>
    [Fact]
    public void Constructor_NullModel_ThrowsArgumentNullException()
    {
        // Act / Assert: a null model throws
        Assert.Throws<ArgumentNullException>(() => new PartOfSpeechResolver(null!));
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
}
