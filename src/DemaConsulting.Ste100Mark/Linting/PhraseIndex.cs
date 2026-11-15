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
///     Token index over a (possibly large) set of approved phrases (single words included). It
///     answers whether a span of text lies entirely inside an occurrence of any phrase, without a
///     regex scan per phrase.
/// </summary>
/// <remarks>
///     An occurrence is matched case-insensitively (ordinal, culture-independent) and is bounded on
///     both sides by a character that is neither a word character nor a hyphen. Every U+0020 in a
///     phrase matches one or more whitespace characters (the regex <c>\s</c> set); consecutive
///     spaces therefore need at least that many whitespace characters, and a leading or trailing
///     space requires whitespace at that edge. Every other phrase character, including a literal
///     tab or non-breaking space, matches only itself. Every occurrence of a phrase is honored,
///     including occurrences that overlap another occurrence of the same phrase. A phrase without
///     any word or hyphen character cannot be indexed and is ignored.
/// </remarks>
internal sealed class PhraseIndex
{
    /// <summary>
    ///     Phrases keyed by each word/hyphen token they contain.
    /// </summary>
    private readonly ILookup<string, Phrase> _byToken;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PhraseIndex"/> class.
    /// </summary>
    /// <param name="phrases">The phrases; entries without a word or hyphen character are ignored.</param>
    public PhraseIndex(IEnumerable<string> phrases)
    {
        _byToken = phrases
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p => new Phrase(p, p.Count(c => c != ' ')))
            .SelectMany(p => new string(p.Text.Select(c => IsTokenChar(c) ? c : ' ').ToArray())
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(token => (token, p)))
            .ToLookup(x => x.token, x => x.p, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Determines whether <c>text[start..(start + length)]</c> lies entirely inside an occurrence
    ///     of an indexed phrase.
    /// </summary>
    /// <param name="text">The text containing the span.</param>
    /// <param name="start">Start of the span.</param>
    /// <param name="length">Length of the span.</param>
    /// <returns><see langword="true"/> when an enclosing phrase occurrence exists.</returns>
    /// <remarks>
    ///     The first word/hyphen token inside the span selects the candidate phrases, so a span
    ///     that starts with punctuation is still resolved. A span with no token character (for
    ///     example <c>&amp;</c>) is resolved through the nearest tokens on either side of it.
    /// </remarks>
    public bool Contains(string text, int start, int length)
    {
        if (length <= 0)
        {
            return false;
        }

        var end = start + length;
        var first = start;
        while (first < end && !IsTokenChar(text[first]))
        {
            first++;
        }

        if (first < end)
        {
            return ContainsViaToken(text, start, end, first);
        }

        var left = start - 1;
        while (left >= 0 && !IsTokenChar(text[left]))
        {
            left--;
        }

        if (left >= 0 && ContainsViaToken(text, start, end, left))
        {
            return true;
        }

        var right = end;
        while (right < text.Length && !IsTokenChar(text[right]))
        {
            right++;
        }

        return right < text.Length && ContainsViaToken(text, start, end, right);
    }

    /// <summary>
    ///     Tests the candidate phrases that contain the maximal token around
    ///     <paramref name="tokenChar"/> for an enclosing occurrence of the span.
    /// </summary>
    private bool ContainsViaToken(string text, int start, int end, int tokenChar)
    {
        var tokenStart = tokenChar;
        while (tokenStart > 0 && IsTokenChar(text[tokenStart - 1]))
        {
            tokenStart--;
        }

        var tokenEnd = tokenChar + 1;
        while (tokenEnd < text.Length && IsTokenChar(text[tokenEnd]))
        {
            tokenEnd++;
        }

        foreach (var phrase in _byToken[text.Substring(tokenStart, tokenEnd - tokenStart)])
        {
            // Every non-whitespace text character between the occurrence start and the span start
            // is matched by a distinct literal phrase character, which bounds the backward scan.
            var budget = phrase.LiteralCount;
            for (var s = start; s >= 0 && budget >= 0; s--)
            {
                budget -= IsRegexSpace(text[s]) ? 0 : 1;
                if ((s == 0 || !IsTokenChar(text[s - 1]))
                    && MatchEnd(text, s, phrase.Text, 0) >= end)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns the end of the phrase occurrence that matches the phrase from
    ///     <paramref name="pi"/> against the text from <paramref name="ti"/> (backtracking exactly
    ///     like a greedy <c>\s+</c> per space), or -1 when it does not match or is not followed by
    ///     a boundary.
    /// </summary>
    private static int MatchEnd(string text, int ti, string phrase, int pi)
    {
        while (pi < phrase.Length && phrase[pi] != ' ')
        {
            if (ti >= text.Length
                || (phrase[pi] != text[ti]
                    && string.Compare(text, ti, phrase, pi, 1, StringComparison.OrdinalIgnoreCase) != 0))
            {
                return -1;
            }

            ti++;
            pi++;
        }

        if (pi == phrase.Length)
        {
            return ti < text.Length && IsTokenChar(text[ti]) ? -1 : ti;
        }

        var spaces = 0;
        while (pi + spaces < phrase.Length && phrase[pi + spaces] == ' ')
        {
            spaces++;
        }

        var run = 0;
        while (ti + run < text.Length && IsRegexSpace(text[ti + run]))
        {
            run++;
        }

        for (var taken = run; taken >= spaces; taken--)
        {
            var end = MatchEnd(text, ti + taken, phrase, pi + spaces);
            if (end >= 0)
            {
                return end;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Whether <paramref name="c"/> is matched by the regex <c>\s</c> class.
    /// </summary>
    private static bool IsRegexSpace(char c) =>
        c is >= '\t' and <= '\r' or '\u0085'
        || char.GetUnicodeCategory(c) is UnicodeCategory.SpaceSeparator
            or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;

    /// <summary>
    ///     An indexed phrase and the number of its characters that are not spaces.
    /// </summary>
    private sealed record Phrase(string Text, int LiteralCount);

    /// <summary>
    ///     Whether <paramref name="c"/> is a regex word character (<c>\w</c>) or a hyphen.
    /// </summary>
    private static bool IsTokenChar(char c) =>
        c == '-' || char.IsLetterOrDigit(c)
        || char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.ConnectorPunctuation;
}
