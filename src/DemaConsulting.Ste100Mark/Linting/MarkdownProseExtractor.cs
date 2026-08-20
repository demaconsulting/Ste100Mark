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
    /// <summary>A Markdown heading line (<c># </c> through <c>###### </c>). Always single-line.</summary>
    Heading,

    /// <summary>
    ///     A bulleted or numbered list item, including any wrapped "lazy continuation" lines folded
    ///     into it (a following line with no list marker of its own, up to the next blank line,
    ///     heading, list item, or table row) - may therefore span multiple source lines, the same way
    ///     <see cref="Paragraph"/> does.
    /// </summary>
    ListItem,

    /// <summary>A single Markdown table row line (a line whose first non-whitespace character is <c>|</c>). Always single-line.</summary>
    TableRow,

    /// <summary>One or more consecutive non-heading, non-list-item, non-table-row lines forming a paragraph.</summary>
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
/// <param name="LineOffsets">
///     Maps each source line folded into <see cref="Text"/> to the character offset within
///     <see cref="Text"/> where that line's content starts, in ascending offset order. A
///     single-line segment (<see cref="SegmentRole.Heading"/> or <see cref="SegmentRole.TableRow"/>)
///     always has exactly one entry, <c>(0, LineNumber)</c>. A multi-line
///     <see cref="SegmentRole.Paragraph"/> or <see cref="SegmentRole.ListItem"/> has one entry per
///     source line it was folded from, since <see cref="MarkdownProseExtractor"/> joins both
///     paragraph lines and a list item's wrapped continuation lines with a single space. Used by
///     <see cref="ResolveLine"/> to report the true source line of a match/sentence found at a given
///     offset within <see cref="Text"/>, rather than always reporting the segment's first line.
/// </param>
internal sealed record ProseSegment(
    string Text,
    int LineNumber,
    SegmentRole Role,
    IReadOnlyList<(int Offset, int Line)> LineOffsets)
{
    /// <summary>
    ///     Initializes a single-line segment (<see cref="SegmentRole.Heading"/> or
    ///     <see cref="SegmentRole.TableRow"/>), whose entire <see cref="Text"/> originates from
    ///     <paramref name="lineNumber"/>.
    /// </summary>
    public ProseSegment(string text, int lineNumber, SegmentRole role)
        : this(text, lineNumber, role, [(0, lineNumber)])
    {
    }

    /// <summary>
    ///     Resolves the true 1-based source line number for a character offset within
    ///     <see cref="Text"/>, accounting for multi-line paragraphs whose lines were joined with a
    ///     single space (see <see cref="LineOffsets"/>).
    /// </summary>
    /// <param name="charOffset">Character offset within <see cref="Text"/>.</param>
    /// <returns>The 1-based source line number containing <paramref name="charOffset"/>.</returns>
    public int ResolveLine(int charOffset)
    {
        var line = LineOffsets[0].Line;
        foreach (var (offset, lineNumber) in LineOffsets)
        {
            if (offset > charOffset)
            {
                break;
            }

            line = lineNumber;
        }

        return line;
    }
}

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
    ///     Matches a bulleted or numbered list item line at any indentation depth, including a nested
    ///     item indented more than the 3 leading spaces <see cref="ListItemRegex"/> permits for a
    ///     top-level item. Used only to detect that a following line is itself a (nested) list item -
    ///     and must therefore end the previous item's wrapped-continuation accumulation - rather than
    ///     being folded in as continuation text. Nested items are otherwise flattened to top-level
    ///     <see cref="SegmentRole.ListItem"/> segments; this extractor does not model list nesting.
    /// </summary>
    private static readonly Regex NestedListItemRegex = new(@"^\s*(?:[-*+]|\d+[.)])\s+(?<text>.*)$", RegexOptions.Compiled, RegexTimeout);

    /// <summary>
    ///     Matches a Markdown table row line (a line whose first non-whitespace character is
    ///     <c>|</c>). Used to keep table rows out of the paragraph accumulator, since concatenating
    ///     several rows into one run-on "sentence" would corrupt sentence/word counting (the pipe
    ///     characters and multiple cells are not natural-language prose).
    /// </summary>
    private static readonly Regex TableRowRegex = new(@"^\s{0,3}\|", RegexOptions.Compiled, RegexTimeout);

    /// <summary>
    ///     Matches a Markdown table delimiter/separator row (for example <c>| --- | :-- |</c>),
    ///     which contains no prose and should be skipped entirely rather than emitted as a segment.
    /// </summary>
    private static readonly Regex TableSeparatorRowRegex = new(@"^\s{0,3}\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$", RegexOptions.Compiled, RegexTimeout);

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

        var buffer = new StringBuilder();
        var bufferStartLine = -1;
        var bufferRole = SegmentRole.Paragraph;
        var lineOffsets = new List<(int Offset, int Line)>();
        var inFence = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var rawLine = lines[i];

            // Fenced code blocks are never prose; toggle state on each fence delimiter and skip
            // every line (including the delimiters themselves) while inside one.
            if (FenceRegex.IsMatch(rawLine))
            {
                FlushBuffer(segments, buffer, lineOffsets, bufferRole, ref bufferStartLine);
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
                FlushBuffer(segments, buffer, lineOffsets, bufferRole, ref bufferStartLine);
                continue;
            }

            var headingMatch = HeadingRegex.Match(cleaned);
            if (headingMatch.Success)
            {
                FlushBuffer(segments, buffer, lineOffsets, bufferRole, ref bufferStartLine);
                segments.Add(new ProseSegment(headingMatch.Groups["text"].Value, lineNumber, SegmentRole.Heading));
                continue;
            }

            var listItemMatch = ListItemRegex.Match(cleaned);
            if (listItemMatch.Success)
            {
                // Rule 8.4: each vertical-list item is counted as its own sentence against the
                // sentence word-count limit, so a list item is never merged with a preceding
                // paragraph or a different list item. A wrapped continuation line of the *same*
                // item (a following line with no list marker of its own, e.g. a long item that a
                // Markdown author wrapped at 80 columns) is folded back into this item's text
                // below, the same way a wrapped paragraph line is - otherwise a disallowed phrase
                // spanning the wrap (for example "process error" split across two source lines)
                // would never match, since it would straddle two disconnected segments.
                FlushBuffer(segments, buffer, lineOffsets, bufferRole, ref bufferStartLine);
                bufferRole = SegmentRole.ListItem;
                bufferStartLine = lineNumber;
                lineOffsets.Add((0, lineNumber));
                buffer.Append(listItemMatch.Groups["text"].Value.Trim());
                continue;
            }

            if (TableRowRegex.IsMatch(cleaned))
            {
                // Table rows are never merged into a paragraph or across rows: without this, several
                // consecutive rows collapse into one run-on "sentence" containing every pipe
                // character and cell from multiple rows, corrupting sentence/word counting. The
                // separator row (e.g. "| --- | :-- |") carries no prose and is skipped entirely.
                // Each cell is emitted as its own segment (mirroring the Rule 8.4 list-item
                // rationale: a short cell such as "Path to config file" is not itself a full
                // sentence and should not be merged with unrelated cells), so a cell with a few
                // words is checked on its own terms, while a cell containing a genuine descriptive
                // paragraph is still fully checked.
                FlushBuffer(segments, buffer, lineOffsets, bufferRole, ref bufferStartLine);
                if (!TableSeparatorRowRegex.IsMatch(cleaned))
                {
                    foreach (var cell in SplitTableRowCells(cleaned))
                    {
                        if (cell.Length > 0)
                        {
                            segments.Add(new ProseSegment(cell, lineNumber, SegmentRole.TableRow));
                        }
                    }
                }

                continue;
            }

            // A line starting a (possibly nested) list item always ends any active
            // paragraph/list-item continuation, even when it is indented deeper than
            // ListItemRegex's 3-space top-level limit: without this check, a nested item such as
            // "- parent\n    - child" would fall into the continuation branch below and merge the
            // child into the parent's ListItem segment, letting phrase/word checks cross what are
            // really two separate items. Nested items are otherwise flattened - they are still
            // emitted as their own top-level ListItem segment, just not merged into the parent's.
            if (NestedListItemRegex.IsMatch(cleaned) && !listItemMatch.Success)
            {
                FlushBuffer(segments, buffer, lineOffsets, bufferRole, ref bufferStartLine);
                bufferRole = SegmentRole.ListItem;
                bufferStartLine = lineNumber;
                lineOffsets.Add((0, lineNumber));
                buffer.Append(NestedListItemRegex.Match(cleaned).Groups["text"].Value.Trim());
                continue;
            }

            // Any other non-blank line accumulates into the current buffer: either a continuation
            // of the paragraph/list-item already being accumulated, or (when no buffer is active)
            // the start of a new paragraph. Test bufferStartLine rather than buffer.Length so that
            // a list item with no visible text on its marker line (e.g. "- " followed immediately
            // by a continuation) is still recognized as active and keeps its ListItem role, instead
            // of being reset to Paragraph.
            if (bufferStartLine < 0)
            {
                bufferRole = SegmentRole.Paragraph;
                bufferStartLine = lineNumber;
            }
            else if (buffer.Length > 0)
            {
                buffer.Append(' ');
            }

            lineOffsets.Add((buffer.Length, lineNumber));
            buffer.Append(cleaned.Trim());
        }

        FlushBuffer(segments, buffer, lineOffsets, bufferRole, ref bufferStartLine);

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
    ///     Splits a Markdown table row into its cell texts, trimming surrounding whitespace and the
    ///     leading/trailing <c>|</c> delimiters. A <c>|</c> inside an inline code span (for example
    ///     <c>`a|b`</c>) is not treated as a cell separator, matching how Markdown table syntax
    ///     itself distinguishes escaped/code-span pipes from real column delimiters.
    /// </summary>
    /// <param name="row">A single table row line (already known to start with <c>|</c>).</param>
    /// <returns>The non-empty, trimmed text of each cell in the row, in column order.</returns>
    private static IEnumerable<string> SplitTableRowCells(string row)
    {
        var codeSpans = FindInlineCodeSpans(row);
        var cells = new List<string>();
        var cellStart = 0;

        for (var i = 0; i < row.Length; i++)
        {
            if (row[i] != '|' || OverlapsInlineCodeSpan(i, 1, codeSpans))
            {
                continue;
            }

            cells.Add(row[cellStart..i].Trim());
            cellStart = i + 1;
        }

        cells.Add(row[cellStart..].Trim());

        return cells.Where(cell => cell.Length > 0);
    }

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
    ///     Emits the accumulated buffer as a <see cref="ProseSegment"/>, if non-empty, and resets
    ///     the buffer and offset map for whatever is accumulated next.
    /// </summary>
    /// <param name="segments">Segment list to append to.</param>
    /// <param name="buffer">Text accumulated so far (a paragraph or a list item and its wrapped continuation lines).</param>
    /// <param name="lineOffsets">
    ///     Per-line offset map accumulated so far (see <see cref="ProseSegment.LineOffsets"/>);
    ///     copied into the emitted segment and then cleared.
    /// </param>
    /// <param name="role">
    ///     The role to emit the segment as - <see cref="SegmentRole.Paragraph"/> or
    ///     <see cref="SegmentRole.ListItem"/> (a list item's wrapped continuation lines are folded
    ///     into the same buffer and emitted together as one <see cref="SegmentRole.ListItem"/>
    ///     segment, mirroring how a multi-line paragraph is folded and emitted as one segment).
    /// </param>
    /// <param name="startLine">Line number the buffer started on.</param>
    private static void FlushBuffer(
        List<ProseSegment> segments,
        StringBuilder buffer,
        List<(int Offset, int Line)> lineOffsets,
        SegmentRole role,
        ref int startLine)
    {
        if (buffer.Length > 0)
        {
            segments.Add(new ProseSegment(buffer.ToString(), startLine, role, [.. lineOffsets]));
            buffer.Clear();
        }

        // Always clear lineOffsets and reset startLine, even when buffer was empty (e.g. a list
        // item with an empty marker such as "- " with no text of its own): otherwise a stale
        // (0, lineNumber) entry recorded for that empty item would be carried forward and
        // incorrectly attributed to whatever segment is accumulated next.
        lineOffsets.Clear();
        startLine = -1;
    }
}
