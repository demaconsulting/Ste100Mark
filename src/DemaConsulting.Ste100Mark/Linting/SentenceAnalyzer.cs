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
///     A single sentence identified within a <see cref="ProseSegment"/>, together with its word
///     count as computed per STE100 counting Rules 8.5-8.7.
/// </summary>
/// <param name="Text">The sentence text, as split from its containing segment.</param>
/// <param name="WordCount">Word count computed per <see cref="SentenceAnalyzer.CountWords"/>.</param>
/// <param name="StartOffset">
///     Character offset within the containing <see cref="ProseSegment.Text"/> where this sentence
///     begins, used by <see cref="ProseSegment.ResolveLine"/> to report the sentence's true source
///     line in a multi-line paragraph rather than always reporting the segment's first line.
/// </param>
internal sealed record Sentence(string Text, int WordCount, int StartOffset);

/// <summary>
///     Splits prose text into sentences and counts words per official ASD-STE100 Issue 9 Rules
///     4.1 and 8.4-8.7.
/// </summary>
/// <remarks>
///     This is a heuristic, regex-based implementation, not a full natural-language parser — see
///     Risk #2 in the planning report for this feature. It is scoped to handle the specific
///     counting rules called out in the feature request (parentheticals, hyphenated words, numbers
///     with units, quoted text, and simple proper-noun/title sequences), not general-purpose
///     English tokenization.
///
///     Rule 8.6 also extends to inline code spans: an inline code span (for example,
///     <c>`dotnet build`</c>) counts as exactly one word toward the sentence word-count limit,
///     regardless of how many tokens appear inside it, consistent with the treatment of numbers,
///     abbreviations, identifiers, and quoted text. <see cref="Sentence.Text"/> keeps the literal
///     code text (backticks included) for display in diagnostics; only <see cref="CountWords"/>'s
///     internal normalization collapses it to a single placeholder token.
///
///     Note: because inline code spans are no longer stripped before sentence splitting (see
///     <see cref="MarkdownProseExtractor"/>), a code span that itself contains sentence-terminating
///     punctuation followed by whitespace and an uppercase letter/digit (for example,
///     <c>`Foo. Bar()`</c>) can cause <see cref="SentenceSplitRegex"/> to split what should be one
///     opaque token into multiple sentences. This is a known limitation of the regex-based
///     heuristic, consistent with its documented scope, and not addressed by this word-count
///     change.
/// </remarks>
internal static class SentenceAnalyzer
{
    /// <summary>
    ///     Timeout applied to every regular expression in this class, bounding worst-case matching
    ///     time against pathological input rather than allowing unbounded backtracking.
    /// </summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Splits after a sentence-terminating <c>.</c>, <c>!</c>, <c>?</c>, or <c>:</c> when
    ///     followed by whitespace and then an uppercase letter, digit, quote, opening parenthesis,
    ///     backtick, asterisk, underscore, or end of text. The Markdown emphasis/code markers
    ///     (backtick, asterisk, underscore) are included because a sentence commonly starts with an
    ///     inline code span or italic/bold text (for example, <c>`code`</c> or <c>*emphasis*</c>)
    ///     rather than a plain letter; without them, such a sentence boundary is missed and the
    ///     next sentence's word count is merged into the previous one, which can produce a false
    ///     Rule 8.4 excess-length sentence finding. Rule 4.1 treats a colon introducing a vertical list
    ///     the same as a period; since <see cref="MarkdownProseExtractor"/> already emits each list
    ///     item as its own segment, treating ':' as a general sentence terminator here also covers
    ///     the (rarer) case of a colon-introduced clause appearing within a single paragraph
    ///     segment.
    /// </summary>
    private static readonly Regex SentenceSplitRegex =
        new(@"(?<=[.!?:])\s+(?=[A-Z0-9""'(`*_]|$)", RegexOptions.Compiled, RegexTimeout);

    /// <summary>Matches a double-quoted span (Rule 8.6: quoted text counts as one word).</summary>
    private static readonly Regex QuotedTextRegex = new("\"[^\"]*\"", RegexOptions.Compiled, RegexTimeout);

    /// <summary>Matches a parenthetical span with no nested parentheses (Rule 8.5).</summary>
    private static readonly Regex ParentheticalRegex = new(@"\([^()]*\)", RegexOptions.Compiled, RegexTimeout);

    /// <summary>
    ///     Matches a number (optionally decimal) immediately followed by a short unit/abbreviation
    ///     token (Rule 8.6: a number with its unit counts as one word).
    /// </summary>
    private static readonly Regex NumberWithUnitRegex =
        new(@"\b\d+(?:\.\d+)?\s?[a-zA-Z%]{1,4}\b", RegexOptions.Compiled, RegexTimeout);

    /// <summary>
    ///     Matches two or more consecutive capitalized words, other than the first word of the
    ///     sentence, as a heuristic stand-in for Rule 8.6's "titles, headings, labels, and proper
    ///     nouns of individuals, groups, organizations, and geopolitical entities count as one
    ///     word" — these all share the surface pattern of consecutive Title-Case words.
    /// </summary>
    private static readonly Regex TitleCaseSequenceRegex =
        new(@"(?<=\S\s)(?:[A-Z][a-zA-Z0-9]*(?:\s+[A-Z][a-zA-Z0-9]*)+)", RegexOptions.Compiled, RegexTimeout);

    /// <summary>
    ///     Splits a prose segment's text into sentences and computes each sentence's official
    ///     word count.
    /// </summary>
    /// <param name="text">Prose text of a single <see cref="ProseSegment"/>.</param>
    /// <returns>Sentences in order of appearance; empty when <paramref name="text"/> is blank.</returns>
    public static IReadOnlyList<Sentence> Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var leadingTrim = text.Length - text.TrimStart().Length;
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        var sentences = new List<Sentence>();
        var partStart = 0;
        foreach (Match splitMatch in SentenceSplitRegex.Matches(trimmed))
        {
            AddSentenceParts(trimmed[partStart..splitMatch.Index], partStart + leadingTrim, sentences);
            partStart = splitMatch.Index + splitMatch.Length;
        }

        AddSentenceParts(trimmed[partStart..], partStart + leadingTrim, sentences);

        return sentences;
    }

    /// <summary>
    ///     Trims one raw split part, and if non-empty, adds its <see cref="Sentence"/> (with its
    ///     resolved start offset within the original segment text) plus any complete-sentence
    ///     parentheticals it contains (Rule 8.5), to <paramref name="sentences"/>.
    /// </summary>
    /// <param name="part">Raw (untrimmed) text between two sentence split points.</param>
    /// <param name="partOffset">Offset of <paramref name="part"/> within the original segment text.</param>
    /// <param name="sentences">Sentence list to append to.</param>
    private static void AddSentenceParts(string part, int partOffset, List<Sentence> sentences)
    {
        var leadingWhitespace = part.Length - part.TrimStart().Length;
        var sentenceText = part.Trim();
        if (sentenceText.Length == 0)
        {
            return;
        }

        var sentenceOffset = partOffset + leadingWhitespace;
        sentences.Add(new Sentence(sentenceText, CountWords(sentenceText), sentenceOffset));

        // Rule 8.5: a parenthetical that itself forms a complete sentence (starts with a
        // capital letter and ends with terminal punctuation) is counted separately from its
        // containing sentence, in addition to counting as a single word within it.
        foreach (Match match in ParentheticalRegex.Matches(sentenceText))
        {
            var innerRaw = match.Value[1..^1];
            var innerLeadingWhitespace = innerRaw.Length - innerRaw.TrimStart().Length;
            var inner = innerRaw.Trim();
            if (LooksLikeCompleteSentence(inner))
            {
                var innerOffset = sentenceOffset + match.Index + 1 + innerLeadingWhitespace;
                sentences.Add(new Sentence(inner, CountWords(inner), innerOffset));
            }
        }
    }

    /// <summary>
    ///     Counts the words in a single sentence per Rules 8.5-8.7: inline code spans, parentheticals,
    ///     and numbers/units/quoted text/proper-noun sequences each count as one word, and
    ///     hyphenated words count as one word.
    /// </summary>
    /// <param name="sentence">A single sentence's text (not a whole paragraph).</param>
    /// <returns>The rule-adjusted word count.</returns>
    public static int CountWords(string sentence)
    {
        ArgumentNullException.ThrowIfNull(sentence);

        if (sentence.Trim().Length == 0)
        {
            return 0;
        }

        // Each substitution below collapses a multi-token span that STE100 counts as a single word
        // into one placeholder token, so that a final whitespace split yields the official count.
        // Hyphenated words (Rule 8.7) require no special handling: "well-known" already contains no
        // internal whitespace, so it survives the final split as a single token.
        //
        // The inline-code-span substitution runs first so that a code span's internal punctuation,
        // quotes, parentheses, or digits are consumed before the later regexes see them, preventing
        // those patterns from independently re-matching content inside the span.
        var normalized = sentence;
        normalized = MarkdownProseExtractor.InlineCodeSpanRegex.Replace(normalized, "C");
        normalized = QuotedTextRegex.Replace(normalized, "Q");
        normalized = ParentheticalRegex.Replace(normalized, "P");
        normalized = NumberWithUnitRegex.Replace(normalized, "N");
        normalized = TitleCaseSequenceRegex.Replace(normalized, "T");

        return normalized
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Length;
    }

    /// <summary>
    ///     Heuristically determines whether a parenthetical's inner text reads as a complete
    ///     sentence in its own right (starts with a capital letter, ends with terminal punctuation).
    /// </summary>
    /// <param name="inner">The parenthetical's inner text, without the surrounding parentheses.</param>
    /// <returns><see langword="true"/> if the text looks like a complete sentence.</returns>
    private static bool LooksLikeCompleteSentence(string inner)
    {
        if (inner.Length < 2)
        {
            return false;
        }

        var startsWithCapital = char.IsUpper(inner[0]);
        var endsWithTerminator = inner[^1] is '.' or '!' or '?';
        return startsWithCapital && endsWithTerminator;
    }
}
