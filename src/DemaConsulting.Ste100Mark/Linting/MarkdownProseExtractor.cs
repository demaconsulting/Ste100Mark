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

using System.Text;
using System.Text.RegularExpressions;

namespace DemaConsulting.Ste100Mark.Linting;

/// <summary>
///     Identifies the Markdown structural context a <see cref="ProseSegment"/> was extracted from,
///     since STE100 sentence/paragraph rules treat headings, list items, and paragraphs
///     differently (each list item is its own sentence; paragraphs accumulate multiple sentences).
/// </summary>
internal enum SegmentRole
{
    /// <summary>A Markdown heading line (<c># </c> through <c>###### </c>).</summary>
    Heading,

    /// <summary>A single bulleted or numbered list item line.</summary>
    ListItem,

    /// <summary>One or more consecutive non-heading, non-list-item lines forming a paragraph.</summary>
    Paragraph
}

/// <summary>
///     A run of prose text extracted from a Markdown document, along with the source line at which
///     it starts and its structural role.
/// </summary>
/// <param name="Text">
///     Prose text with fenced code blocks and link destination URLs already removed. Inline code
///     spans are retained verbatim (backticks included) so diagnostic excerpts show the literal
///     code text; see <see cref="MarkdownProseExtractor.InlineCodeSpanRegex"/> and Rule 8.6, which
///     downstream consumers (<see cref="SentenceAnalyzer"/>, <see cref="StructuralRules"/>,
///     <see cref="DictionaryChecker"/>) use to count each span as one word and exclude its content
///     from grammar-sensitive checks.
/// </param>
/// <param name="LineNumber">1-based line number in the source file where the segment begins.</param>
/// <param name="Role">Structural role the segment plays in the source document.</param>
internal sealed record ProseSegment(string Text, int LineNumber, SegmentRole Role);

/// <summary>
///     Extracts the prose portions of a Markdown document that STE100 rules apply to, filtering out
///     content that is not natural-language prose.
/// </summary>
/// <remarks>
///     This is a purpose-built, line-based extractor rather than a full CommonMark implementation —
///     see Risk #2 in the planning report for this feature. It intentionally covers only the
///     Markdown subset used by this repository's own documentation (headings, paragraphs,
///     bulleted/numbered lists, fenced code blocks, inline code spans, and inline links) and does
///     not attempt to handle HTML blocks, reference-style link definitions as prose, or nested
///     fences. No third-party Markdown parsing library is used, per the feature's design decision to
///     keep parsing dependency-free and auditable.
///
///     Fenced code blocks and link destination URLs are removed entirely, but inline code spans are
///     retained verbatim in <see cref="ProseSegment.Text"/> (Rule 8.6 treats a literal technical
///     value as one word, extended here to inline code spans): <see cref="SentenceAnalyzer"/>
///     counts each span as exactly one word, and <see cref="StructuralRules"/>/
///     <see cref="DictionaryChecker"/> use <see cref="FindInlineCodeSpans"/>,
///     <see cref="OverlapsInlineCodeSpan"/>, and <see cref="MaskInlineCodeSpans"/> to exclude
///     inline-code content from grammar-sensitive checks while still displaying it verbatim in
///     diagnostic messages.
/// </remarks>
internal static class MarkdownProseExtractor
{
    /// <summary>
    ///     Timeout applied to every regular expression in this class, bounding worst-case matching
    ///     time against pathological input rather than allowing unbounded backtracking.
    /// </summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Matches a fenced code block delimiter line (```` ``` ```` or <c>~~~</c>, optionally with a language tag).</summary>
    private static readonly Regex FenceRegex = new(@"^\s*(`{3,}|~{3,})", RegexOptions.Compiled, RegexTimeout);

    /// <summary>Matches an ATX heading line, capturing the heading text after the <c>#</c> markers.</summary>
    private static readonly Regex HeadingRegex = new(@"^\s{0,3}#{1,6}\s+(?<text>.*?)\s*#*\s*$", RegexOptions.Compiled, RegexTimeout);

    /// <summary>Matches a bulleted or numbered list item line, capturing the item text.</summary>
    private static readonly Regex ListItemRegex = new(@"^\s{0,3}(?:[-*+]|\d+[.)])\s+(?<text>.*)$", RegexOptions.Compiled, RegexTimeout);

    /// <summary>
    ///     Matches an inline code span (single backtick-delimited run with no embedded backtick).
    ///     Shared by <see cref="SentenceAnalyzer"/>, <see cref="StructuralRules"/>, and
    ///     <see cref="DictionaryChecker"/> so every consumer identifies inline code spans
    ///     identically.
    /// </summary>
    internal static readonly Regex InlineCodeSpanRegex = new("`[^`]*`", RegexOptions.Compiled, RegexTimeout);

    /// <summary>Matches an inline Markdown link, capturing the link text and discarding the destination.</summary>
    private static readonly Regex InlineLinkRegex = new(@"\[(?<text>[^\]]*)\]\([^)]*\)", RegexOptions.Compiled, RegexTimeout);

    /// <summary>Matches a blank (whitespace-only) line.</summary>
    private static readonly Regex BlankLineRegex = new(@"^\s*$", RegexOptions.Compiled, RegexTimeout);

    /// <summary>
    ///     Extracts prose segments from Markdown source text.
    /// </summary>
    /// <param name="markdown">Full text of a Markdown file.</param>
    /// <returns>
    ///     Prose segments in document order. Fenced code block contents and link destination URLs
    ///     are excluded from every segment's <see cref="ProseSegment.Text"/>; inline code spans are
    ///     retained verbatim (see <see cref="ProseSegment.Text"/> remarks).
    /// </returns>
    public static IReadOnlyList<ProseSegment> Extract(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var segments = new List<ProseSegment>();
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        var paragraphBuffer = new StringBuilder();
        var paragraphStartLine = -1;
        var inFence = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var rawLine = lines[i];

            // Fenced code blocks are never prose; toggle state on each fence delimiter and skip
            // every line (including the delimiters themselves) while inside one.
            if (FenceRegex.IsMatch(rawLine))
            {
                FlushParagraph(segments, paragraphBuffer, ref paragraphStartLine);
                inFence = !inFence;
                continue;
            }

            if (inFence)
            {
                continue;
            }

            // Rewrite inline links to keep only their visible text before classifying the line, so
            // that link destination URLs never influence sentence/word counting. Inline code spans
            // are left untouched here — they are retained verbatim in the segment text (Rule 8.6)
            // and only excluded from grammar-sensitive checks downstream, in
            // <see cref="StructuralRules"/> and <see cref="DictionaryChecker"/>.
            var cleaned = CleanLine(rawLine);

            if (BlankLineRegex.IsMatch(cleaned))
            {
                FlushParagraph(segments, paragraphBuffer, ref paragraphStartLine);
                continue;
            }

            var headingMatch = HeadingRegex.Match(cleaned);
            if (headingMatch.Success)
            {
                FlushParagraph(segments, paragraphBuffer, ref paragraphStartLine);
                segments.Add(new ProseSegment(headingMatch.Groups["text"].Value, lineNumber, SegmentRole.Heading));
                continue;
            }

            var listItemMatch = ListItemRegex.Match(cleaned);
            if (listItemMatch.Success)
            {
                // Rule 8.4: each vertical-list item is counted as its own sentence against the
                // sentence word-count limit, so list items are never merged into a paragraph.
                FlushParagraph(segments, paragraphBuffer, ref paragraphStartLine);
                segments.Add(new ProseSegment(listItemMatch.Groups["text"].Value, lineNumber, SegmentRole.ListItem));
                continue;
            }

            // Any other non-blank line accumulates into the current paragraph buffer.
            if (paragraphBuffer.Length == 0)
            {
                paragraphStartLine = lineNumber;
            }
            else
            {
                paragraphBuffer.Append(' ');
            }

            paragraphBuffer.Append(cleaned.Trim());
        }

        FlushParagraph(segments, paragraphBuffer, ref paragraphStartLine);

        return segments;
    }

    /// <summary>
    ///     Rewrites inline links to keep only their visible text, leaving inline code spans and the
    ///     rest of the line untouched.
    /// </summary>
    /// <param name="line">Raw source line.</param>
    /// <returns>Line with link destinations removed; inline code spans retained verbatim.</returns>
    private static string CleanLine(string line) => InlineLinkRegex.Replace(line, m => m.Groups["text"].Value);

    /// <summary>
    ///     Locates every inline code span in <paramref name="text"/>, preserving their character
    ///     offsets so callers can test whether some other match (a dictionary term, a contraction)
    ///     falls inside one via <see cref="OverlapsInlineCodeSpan"/>.
    /// </summary>
    /// <param name="text">Verbatim segment or sentence text to search.</param>
    /// <returns>The start index and length of each inline code span, in document order.</returns>
    internal static IReadOnlyList<(int Start, int Length)> FindInlineCodeSpans(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return InlineCodeSpanRegex
            .Matches(text)
            .Select(m => (m.Index, m.Length))
            .ToList();
    }

    /// <summary>
    ///     Determines whether the span <c>[start, start + length)</c> falls entirely inside one of
    ///     <paramref name="spans"/>, meaning it should be excluded from grammar-sensitive checks
    ///     because it lies wholly within an inline code span.
    /// </summary>
    /// <param name="start">Start index of the span to test.</param>
    /// <param name="length">Length of the span to test.</param>
    /// <param name="spans">Inline code span ranges, as returned by <see cref="FindInlineCodeSpans"/>.</param>
    /// <returns><see langword="true"/> if the tested span is wholly contained in an inline code span.</returns>
    internal static bool OverlapsInlineCodeSpan(int start, int length, IReadOnlyList<(int Start, int Length)> spans)
    {
        ArgumentNullException.ThrowIfNull(spans);

        var end = start + length;
        return spans.Any(span => start >= span.Start && end <= span.Start + span.Length);
    }

    /// <summary>
    ///     Replaces every inline code span in <paramref name="text"/> with a single neutral
    ///     placeholder character, for grammar-sensitive checks that only need a yes/no signal (for
    ///     example, "does this segment contain a semicolon?") and do not need to preserve character
    ///     offsets afterward. Do not use this for checks that report a match's position or verbatim
    ///     text, since the replacement shortens the string; use
    ///     <see cref="FindInlineCodeSpans"/>/<see cref="OverlapsInlineCodeSpan"/> instead for those.
    /// </summary>
    /// <param name="text">Verbatim segment or sentence text.</param>
    /// <returns><paramref name="text"/> with each inline code span replaced by a single character.</returns>
    internal static string MaskInlineCodeSpans(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return InlineCodeSpanRegex.Replace(text, "C");
    }

    /// <summary>
    ///     Emits the accumulated paragraph buffer as a <see cref="ProseSegment"/>, if non-empty, and
    ///     resets the buffer for the next paragraph.
    /// </summary>
    /// <param name="segments">Segment list to append to.</param>
    /// <param name="buffer">Paragraph text accumulated so far.</param>
    /// <param name="startLine">Line number the paragraph started on.</param>
    private static void FlushParagraph(List<ProseSegment> segments, StringBuilder buffer, ref int startLine)
    {
        if (buffer.Length > 0)
        {
            segments.Add(new ProseSegment(buffer.ToString(), startLine, SegmentRole.Paragraph));
            buffer.Clear();
        }

        startLine = -1;
    }
}
