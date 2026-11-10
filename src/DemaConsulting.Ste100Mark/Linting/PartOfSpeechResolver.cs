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
using DemaConsulting.PartOfSpeech;

namespace DemaConsulting.Ste100Mark.Linting;

/// <summary>
///     One token of a tagged sentence: its offsets within the sentence text, tag and confidence.
/// </summary>
/// <param name="Start">The zero-based start offset of the token within the sentence text.</param>
/// <param name="Length">The length of the token.</param>
/// <param name="Tag">The universal part-of-speech tag chosen for the token.</param>
/// <param name="Confidence">The confidence (0 to 1) of the chosen tag.</param>
internal readonly record struct TaggedSpan(int Start, int Length, Upos Tag, double Confidence);

/// <summary>
///     <see cref="IPartOfSpeechResolver"/> backed by the <c>DemaConsulting.PartOfSpeech</c> context-aware
///     part-of-speech tagger (<see cref="Tagger.Default"/>).
/// </summary>
/// <remarks>
///     The segment is split into sentences; the sentence containing the occurrence is tokenized and tagged
///     by the package in a single call. The token that covers the occurrence is mapped to
///     <see cref="PartOfSpeech.Noun"/>, <see cref="PartOfSpeech.Verb"/>, <see cref="PartOfSpeech.Adjective"/>,
///     or <see cref="PartOfSpeech.Adverb"/> only when the tagger's
///     confidence is at least <see cref="MinimumConfidence"/>. A confident determiner is preserved as
///     <see cref="PartOfSpeech.Determiner"/> so the checker can distinguish it from every dictionary
///     sense role; determiner is resolver evidence only and is not accepted in dictionary configuration.
///     Other unsupported outcomes (low confidence, any other part of speech, or an occurrence spanning
///     several tokens) are inconclusive (<see langword="null"/>). The package tags words that are not
///     in its lexicon as nouns, so unknown words cannot be detected. The tagger is thread-safe and fully
///     in-memory (no download), and per-sentence results are cached.
/// </remarks>
internal sealed class PartOfSpeechResolver : IPartOfSpeechResolver
{
    /// <summary>
    ///     Minimum tagger confidence (0 to 1) for a supported answer to be accepted.
    /// </summary>
    /// <remarks>
    ///     This is the strict threshold documented by the package: on its open test split it answers
    ///     83.7% of decisions with 2.4 harmful errors per 1000 answered decisions. In this project's
    ///     110-sample hand check no error was found at a confidence of 0.95 or higher, whereas
    ///     low-confidence VERB answers on noun-noun compounds were wrong about half the time. A strict
    ///     value is preferred because a wrong answer hides a real violation or reports a false one, while
    ///     an inconclusive answer only makes the checker report the term as ambiguous.
    /// </remarks>
    internal const double MinimumConfidence = 0.95;

    /// <summary>Maximum number of cached sentence classifications before completed entries are evicted.</summary>
    private const int MaxCacheEntries = 4096;

    /// <summary>
    ///     Sentence boundary, shared with <see cref="SentenceAnalyzer"/> so both agree on where a sentence
    ///     ends (for example, no boundary in <c>e.g. test the valve</c>).
    /// </summary>
    private static readonly Regex SentenceBoundaryRegex = SentenceAnalyzer.SentenceSplitRegex;

    /// <summary>The lazily created process-wide default resolver.</summary>
    private static readonly Lazy<PartOfSpeechResolver> SharedInstance =
        new(() => new PartOfSpeechResolver(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The tagging function: sentence text to tagged tokens (offsets relative to that text).</summary>
    private readonly Func<string, IReadOnlyList<TaggedSpan>> _tag;

    /// <summary>Cache entry count at which completed entries are evicted.</summary>
    private readonly int _maxCacheEntries;

    /// <summary>Cache of per-sentence classifications keyed by sentence text.</summary>
    /// <remarks>
    ///     Each value is a <see cref="Lazy{T}"/> so concurrent callers share a single classification.
    /// </remarks>
    private readonly ConcurrentDictionary<string, Lazy<SentenceTags>> _cache = new(StringComparer.Ordinal);

    /// <summary>Serializes cache miss management (lookup-or-insert and eviction).</summary>
    private readonly object _cacheLock = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="PartOfSpeechResolver"/> class using the
    ///     <see cref="Tagger.Default"/>.
    /// </summary>
    public PartOfSpeechResolver()
        : this(TagWithDefaultTagger)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PartOfSpeechResolver"/> class using the given
    ///     tagging function.
    /// </summary>
    /// <param name="tag">The tagging function; it must be thread-safe.</param>
    internal PartOfSpeechResolver(Func<string, IReadOnlyList<TaggedSpan>> tag)
        : this(tag, MaxCacheEntries)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PartOfSpeechResolver"/> class using the given
    ///     tagging function and cache capacity.
    /// </summary>
    /// <param name="tag">The tagging function; it must be thread-safe.</param>
    /// <param name="maxCacheEntries">Entry count at which completed cache entries are evicted.</param>
    internal PartOfSpeechResolver(Func<string, IReadOnlyList<TaggedSpan>> tag, int maxCacheEntries)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCacheEntries);
        _tag = tag;
        _maxCacheEntries = maxCacheEntries;
    }

    /// <summary>Gets the number of cached sentence entries (for tests).</summary>
    internal int CachedSentenceCount => _cache.Count;

    /// <summary>
    ///     Reports whether the given sentence currently has a cache entry (for tests).
    /// </summary>
    /// <param name="sentence">The sentence text.</param>
    /// <returns><see langword="true"/> when the sentence is cached.</returns>
    internal bool IsSentenceCached(string sentence) => _cache.ContainsKey(sentence);

    /// <summary>
    ///     Gets the process-wide default resolver, created on first use and kept for the process lifetime.
    /// </summary>
    public static PartOfSpeechResolver Shared => SharedInstance.Value;

    /// <inheritdoc />
    public PartOfSpeech? Resolve(string segmentText, int matchIndex, int matchLength)
    {
        ArgumentNullException.ThrowIfNull(segmentText);
        if (matchIndex < 0 || matchIndex > segmentText.Length || matchLength <= 0
            || matchLength > segmentText.Length - matchIndex)
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
        // Only a new key can grow the cache, so only a miss may evict. Eviction removes completed
        // entries only (an in-flight Lazy stays reachable so concurrent callers keep sharing it), and
        // only as many as needed to make room for the new key: the first completed entry found.
        // The lookup-or-insert and eviction run under a lock so the capacity check, eviction and
        // insertion are atomic; the classification itself (Lazy.Value) runs outside the lock.
        if (!_cache.TryGetValue(sentence, out var entry))
        {
            lock (_cacheLock)
            {
                if (!_cache.TryGetValue(sentence, out entry))
                {
                    if (_cache.Count >= _maxCacheEntries)
                    {
                        foreach (var pair in _cache)
                        {
                            if (pair.Value.IsValueCreated && _cache.TryRemove(pair))
                            {
                                break;
                            }
                        }
                    }

                    // One Lazy per sentence: concurrent callers block on the same computation.
                    var key = sentence;
                    entry = new Lazy<SentenceTags>(() => Classify(key), LazyThreadSafetyMode.ExecutionAndPublication);
                    _cache[sentence] = entry;
                }
            }
        }

        SentenceTags tags;
        try
        {
            tags = entry.Value;
        }
        catch
        {
            // A failed classification must not poison the cache; remove exactly this entry.
            _cache.TryRemove(new KeyValuePair<string, Lazy<SentenceTags>>(sentence, entry));
            throw;
        }

        TrimCompleted(entry);
        return tags;
    }

    /// <summary>
    ///     Evicts completed entries (other than the one just used) while the cache is over capacity,
    ///     so entries that were in flight when the cache filled do not leave it permanently oversized.
    /// </summary>
    private void TrimCompleted(Lazy<SentenceTags> keep)
    {
        if (_cache.Count <= _maxCacheEntries)
        {
            return;
        }

        lock (_cacheLock)
        {
            foreach (var pair in _cache)
            {
                if (_cache.Count <= _maxCacheEntries)
                {
                    break;
                }

                if (!ReferenceEquals(pair.Value, keep) && pair.Value.IsValueCreated)
                {
                    _cache.TryRemove(pair);
                }
            }
        }
    }

    /// <summary>
    ///     Tags one sentence and maps every token to its resolved role.
    /// </summary>
    private SentenceTags Classify(string sentence)
    {
        var tokens = _tag(sentence);
        var spans = new (int Start, int Length)[tokens.Count];
        var roles = new PartOfSpeech?[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
        {
            spans[i] = (tokens[i].Start, tokens[i].Length);
            roles[i] = MapRole(tokens[i]);
        }

        return new SentenceTags(spans, roles);
    }

    /// <summary>
    ///     Tags a sentence with the shared package tagger, flattening every sentence it returns.
    /// </summary>
    /// <remarks>
    ///     Token offsets index the string passed in, so they are relative to the sentence text.
    /// </remarks>
    private static List<TaggedSpan> TagWithDefaultTagger(string sentence)
    {
        var result = new List<TaggedSpan>();
        foreach (var tagged in Tagger.Default.Tag(sentence))
        {
            foreach (var token in tagged.Tokens)
            {
                result.Add(new TaggedSpan(token.Start, token.Length, token.Tag, token.Confidence));
            }
        }

        return result;
    }

    /// <summary>
    ///     Maps one tagged token to a repository part of speech, or <see langword="null"/> when it is not
    ///     a confident noun, verb, adjective, or adverb. A confident determiner is preserved as
    ///     resolver-only evidence and is not a dictionary sense role; all other unsupported tags are
    ///     inconclusive.
    /// </summary>
    private static PartOfSpeech? MapRole(TaggedSpan token)
    {
        if (token.Confidence < MinimumConfidence)
        {
            return null;
        }

        return token.Tag switch
        {
            Upos.Noun => PartOfSpeech.Noun,
            Upos.Verb => PartOfSpeech.Verb,
            Upos.Adj => PartOfSpeech.Adjective,
            Upos.Adv => PartOfSpeech.Adverb,
            Upos.Det => PartOfSpeech.Determiner,
            _ => null
        };
    }

    /// <summary>The token spans of one sentence and the role resolved for each token.</summary>
    private sealed record SentenceTags((int Start, int Length)[] Spans, PartOfSpeech?[] Roles);
}
