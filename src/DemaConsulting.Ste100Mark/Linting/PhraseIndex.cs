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

using System.Globalization;

namespace DemaConsulting.Ste100Mark.Linting;

/// <summary>
///     Token index over a (possibly large) set of approved multi-word phrases. It answers whether a
///     span of text lies entirely inside an occurrence of any phrase, without a regex scan per
///     phrase. An occurrence is case-insensitive, whitespace-tolerant (a space in a phrase matches
///     one or more whitespace characters) and bounded by a character that is neither a word
///     character nor a hyphen on both sides.
/// </summary>
internal sealed class PhraseIndex
{
    /// <summary>
    ///     Phrases keyed by each word/hyphen token they contain.
    /// </summary>
    private readonly ILookup<string, string[]> _byToken;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PhraseIndex"/> class.
    /// </summary>
    /// <param name="phrases">The phrases; blank entries are ignored.</param>
    public PhraseIndex(IEnumerable<string> phrases)
    {
        _byToken = phrases
            .Select(p => (Words: p.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries), Text: p))
            .SelectMany(p => new string(p.Text.Select(c => IsTokenChar(c) ? c : ' ').ToArray())
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(token => (token, p.Words)))
            .ToLookup(x => x.token, x => x.Words, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Determines whether <c>text[start..(start + length)]</c> lies entirely inside an occurrence
    ///     of an indexed phrase.
    /// </summary>
    /// <param name="text">The text containing the span.</param>
    /// <param name="start">Start of the span; it must begin on a word/hyphen token boundary.</param>
    /// <param name="length">Length of the span.</param>
    /// <returns><see langword="true"/> when an enclosing phrase occurrence exists.</returns>
    public bool Contains(string text, int start, int length)
    {
        var tokenEnd = start;
        while (tokenEnd < start + length && IsTokenChar(text[tokenEnd]))
        {
            tokenEnd++;
        }

        if (tokenEnd == start)
        {
            return false;
        }

        foreach (var words in _byToken[text.Substring(start, tokenEnd - start)])
        {
            var budget = words.Sum(w => w.Length);
            for (var s = start; s >= 0 && budget >= 0; s--)
            {
                budget -= char.IsWhiteSpace(text[s]) ? 0 : 1;
                if (!char.IsWhiteSpace(text[s])
                    && (s == 0 || !IsTokenChar(text[s - 1]))
                    && MatchEnd(text, s, words) is var end
                    && end >= start + length)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns the end of the phrase occurrence starting at <paramref name="s"/>, or -1 when the
    ///     phrase does not match there or is not followed by a boundary.
    /// </summary>
    private static int MatchEnd(string text, int s, string[] words)
    {
        var pos = s;
        for (var i = 0; i < words.Length; i++)
        {
            if (i > 0)
            {
                var gap = pos;
                while (pos < text.Length && char.IsWhiteSpace(text[pos]))
                {
                    pos++;
                }

                if (pos == gap)
                {
                    return -1;
                }
            }

            if (pos + words[i].Length > text.Length
                || string.Compare(text, pos, words[i], 0, words[i].Length, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return -1;
            }

            pos += words[i].Length;
        }

        return pos < text.Length && IsTokenChar(text[pos]) ? -1 : pos;
    }

    /// <summary>
    ///     Whether <paramref name="c"/> is a regex word character (<c>\w</c>) or a hyphen.
    /// </summary>
    private static bool IsTokenChar(char c) =>
        c == '-' || char.IsLetterOrDigit(c)
        || char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.ConnectorPunctuation;
}
