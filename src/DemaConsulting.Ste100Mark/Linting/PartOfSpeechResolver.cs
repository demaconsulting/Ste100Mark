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

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using DemaConsulting.WordSense;
using DemaConsulting.WordSense.Basic;
using WsPos = DemaConsulting.WordSense.PartOfSpeech;

namespace DemaConsulting.Ste100Mark.Linting;

/// <summary>
///     <see cref="IPartOfSpeechResolver"/> backed by the <c>DemaConsulting.WordSense.Basic</c> context-aware
///     part-of-speech tagger.
/// </summary>
/// <remarks>
///     The segment is split into sentences; the sentence containing the occurrence is tokenized
///     with <c>[\w'-]+|[^\w\s]</c> and classified in a single <c>ClassifyAsync</c> call. The token that
///     covers the occurrence is mapped to <see cref="PartOfSpeech.Noun"/> or
///     <see cref="PartOfSpeech.Verb"/> only when the tagger chose a sense whose confidence is at least
///     <see cref="MinimumConfidence"/>; every other outcome (unknown word, low confidence, any other
///     part of speech, an occurrence spanning several tokens) is inconclusive (<see langword="null"/>).
///     The model is thread-safe and fully in-memory (no download), and per-sentence results are cached.
/// </remarks>
internal sealed class PartOfSpeechResolver : IPartOfSpeechResolver
{
    /// <summary>
    ///     Minimum tagger confidence (0 to 1) for a noun or verb answer to be accepted.
    /// </summary>
    /// <remarks>
    ///     Chosen empirically from the 100-sentence false-positive corpus and 26 additional hard cases
    ///     (imperatives, noun-noun compounds, modals; see the design document): all 126 sentences were
    ///     classified correctly, the lowest confidence of a correct noun or verb answer was 0.4926, and
    ///     no incorrect noun or verb answer was observed in that set at any confidence. The value is set
    ///     just below that lowest correct confidence, so that no correct answer in the set is lost while
    ///     near-random answers are still rejected.
    /// </remarks>
    internal const double MinimumConfidence = 0.45;

    /// <summary>Maximum number of cached sentence classifications before the cache is reset.</summary>
    private const int MaxCacheEntries = 4096;

    /// <summary>Regex timeout applied to every pattern used by this class.</summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Sentence boundary: whitespace that follows sentence-ending punctuation.</summary>
    private static readonly Regex SentenceBoundaryRegex =
        new(@"(?<=[.!?:])\s+", RegexOptions.Compiled, RegexTimeout);

    /// <summary>Tokenizer: a word (letters, digits, apostrophes, hyphens) or one punctuation character.</summary>
    private static readonly Regex TokenRegex =
        new(@"[\w'-]+|[^\w\s]", RegexOptions.Compiled, RegexTimeout);

    /// <summary>The lazily created process-wide default resolver.</summary>
    private static readonly Lazy<PartOfSpeechResolver> SharedInstance =
        new(() => new PartOfSpeechResolver(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The tagger model.</summary>
    private readonly IWordSenseModel _model;

    /// <summary>Cache of per-sentence classifications keyed by sentence text.</summary>
    private readonly ConcurrentDictionary<string, SentenceTags> _cache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a new instance of the <see cref="PartOfSpeechResolver"/> class using the
    ///     <see cref="BasicWordSenseModel"/>.
    /// </summary>
    public PartOfSpeechResolver()
        : this(new BasicWordSenseModel())
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PartOfSpeechResolver"/> class using the given model.
    /// </summary>
    /// <param name="model">The tagger model; its lifetime is the lifetime of this resolver.</param>
    internal PartOfSpeechResolver(IWordSenseModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    /// <summary>
    ///     Gets the process-wide default resolver, created on first use and kept for the process lifetime.
    /// </summary>
    public static PartOfSpeechResolver Shared => SharedInstance.Value;

    /// <inheritdoc />
    public PartOfSpeech? Resolve(string segmentText, int matchIndex, int matchLength)
    {
        ArgumentNullException.ThrowIfNull(segmentText);
        if (matchIndex < 0 || matchLength <= 0 || matchIndex + matchLength > segmentText.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(matchIndex), "The match must lie within the segment text.");
        }

        // Find the sentence that holds the match.
        var sentenceStart = 0;
        var sentenceEnd = segmentText.Length;
        foreach (Match boundary in SentenceBoundaryRegex.Matches(segmentText))
        {
            if (boundary.Index + boundary.Length <= matchIndex)
            {
                sentenceStart = boundary.Index + boundary.Length;
            }
            else
            {
                sentenceEnd = boundary.Index;
                break;
            }
        }

        if (matchIndex + matchLength > sentenceEnd)
        {
            return null;
        }

        var sentence = segmentText[sentenceStart..sentenceEnd];
        var tags = GetTags(sentence);
        var relativeStart = matchIndex - sentenceStart;
        var relativeEnd = relativeStart + matchLength;

        for (var i = 0; i < tags.Spans.Length; i++)
        {
            var (start, length) = tags.Spans[i];
            if (relativeStart < start || relativeStart >= start + length)
            {
                continue;
            }

            // A match that extends past its first token spans several tokens (a multi-word term).
            if (relativeEnd > start + length)
            {
                return null;
            }

            return tags.Roles[i];
        }

        return null;
    }

    /// <summary>
    ///     Classifies one sentence (cached) and returns the token spans with their resolved roles.
    /// </summary>
    private SentenceTags GetTags(string sentence)
    {
        if (_cache.TryGetValue(sentence, out var cached))
        {
            return cached;
        }

        var matches = TokenRegex.Matches(sentence);
        var spans = new (int Start, int Length)[matches.Count];
        var words = new string[matches.Count];
        for (var i = 0; i < matches.Count; i++)
        {
            spans[i] = (matches[i].Index, matches[i].Length);
            words[i] = matches[i].Value;
        }

        var roles = new PartOfSpeech?[matches.Count];

        // The Basic model completes synchronously, so waiting here cannot deadlock or block on I/O.
        var results = _model.ClassifyAsync(words).GetAwaiter().GetResult();
        if (results.Length == words.Length)
        {
            for (var i = 0; i < results.Length; i++)
            {
                roles[i] = MapRole(results[i]);
            }
        }

        var tags = new SentenceTags(spans, roles);
        if (_cache.Count >= MaxCacheEntries)
        {
            _cache.Clear();
        }

        _cache[sentence] = tags;
        return tags;
    }

    /// <summary>
    ///     Maps one tagger result to a repository part of speech, or <see langword="null"/> when it is not
    ///     a confident noun or verb.
    /// </summary>
    private static PartOfSpeech? MapRole(DemaConsulting.WordSense.WordSense result)
    {
        if (result.Details is null || result.Confidence < MinimumConfidence)
        {
            return null;
        }

        return result.Details.PartOfSpeech switch
        {
            WsPos.Noun => PartOfSpeech.Noun,
            WsPos.Verb => PartOfSpeech.Verb,
            _ => null
        };
    }

    /// <summary>The token spans of one sentence and the role resolved for each token.</summary>
    private sealed record SentenceTags((int Start, int Length)[] Spans, PartOfSpeech?[] Roles);
}
