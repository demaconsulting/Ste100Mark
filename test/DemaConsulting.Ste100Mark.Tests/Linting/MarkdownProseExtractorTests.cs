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
///     Unit tests for the MarkdownProseExtractor class.
/// </summary>
public class MarkdownProseExtractorTests
{
    /// <summary>
    ///     Test that a heading line produces a heading-role segment with the marker stripped.
    /// </summary>
    [Fact]
    public void Extract_Heading_ReturnsHeadingSegment()
    {
        // Act: execute the operation being tested
        var segments = MarkdownProseExtractor.Extract("# Introduction");

        // Assert: verify expected behavior
        var segment = Assert.Single(segments);
        Assert.Equal(SegmentRole.Heading, segment.Role);
        Assert.Equal("Introduction", segment.Text);
        Assert.Equal(1, segment.LineNumber);
    }

    /// <summary>
    ///     Test that a bulleted list item produces a list-item-role segment.
    /// </summary>
    [Fact]
    public void Extract_BulletListItem_ReturnsListItemSegment()
    {
        // Act: execute the operation being tested
        var segments = MarkdownProseExtractor.Extract("- Open the panel.");

        // Assert: verify expected behavior
        var segment = Assert.Single(segments);
        Assert.Equal(SegmentRole.ListItem, segment.Role);
        Assert.Equal("Open the panel.", segment.Text);
    }

    /// <summary>
    ///     Test that a numbered list item produces a list-item-role segment.
    /// </summary>
    [Fact]
    public void Extract_NumberedListItem_ReturnsListItemSegment()
    {
        // Act: execute the operation being tested
        var segments = MarkdownProseExtractor.Extract("1. Open the panel.");

        // Assert: verify expected behavior
        var segment = Assert.Single(segments);
        Assert.Equal(SegmentRole.ListItem, segment.Role);
    }

    /// <summary>
    ///     Test that each cell of a Markdown table row becomes its own table-row segment, rather than
    ///     being merged into a paragraph with other rows (which would corrupt sentence/word counting
    ///     with pipe characters and unrelated cell text from multiple rows).
    /// </summary>
    [Fact]
    public void Extract_TableRow_ReturnsOneSegmentPerCell()
    {
        // Act: execute the operation being tested
        var segments = MarkdownProseExtractor.Extract("| `--config <file>` | Path to config file |");

        // Assert: two cells, each its own table-row segment, code span preserved verbatim
        Assert.Equal(2, segments.Count);
        Assert.All(segments, s => Assert.Equal(SegmentRole.TableRow, s.Role));
        Assert.Equal("`--config <file>`", segments[0].Text);
        Assert.Equal("Path to config file", segments[1].Text);
    }

    /// <summary>
    ///     Test that a table header separator row (e.g. <c>| --- | --- |</c>) produces no segments at
    ///     all, since it carries no prose.
    /// </summary>
    [Fact]
    public void Extract_TableSeparatorRow_ProducesNoSegments()
    {
        // Act: execute the operation being tested
        var segments = MarkdownProseExtractor.Extract("| --- | :-- | --:|");

        // Assert: verify expected behavior
        Assert.Empty(segments);
    }

    /// <summary>
    ///     Test that consecutive table rows do not merge into one paragraph across rows: this is a
    ///     regression test for a bug where table rows fell through to the generic paragraph
    ///     accumulator, concatenating multiple rows (with pipe characters and cell separators) into
    ///     one run-on "sentence" that corrupted the word-count check.
    /// </summary>
    [Fact]
    public void Extract_ConsecutiveTableRows_DoNotMergeAcrossRows()
    {
        // Arrange: a small table, as commonly used for a CLI options reference
        const string markdown = "| Option | Description |\n| --- | --- |\n| `--strict` | Treat warnings as errors |";

        // Act: execute the operation being tested
        var segments = MarkdownProseExtractor.Extract(markdown);

        // Assert: no segment contains a pipe character (which would indicate rows were merged)
        Assert.All(segments, s => Assert.DoesNotContain('|', s.Text));
        Assert.All(segments, s => Assert.Equal(SegmentRole.TableRow, s.Role));
    }

    /// <summary>
    ///     Test that a table cell containing a genuinely long descriptive paragraph is still fully
    ///     checked on its own terms (not skipped), proving the table-row handling does not suppress
    ///     legitimate findings within a single cell.
    /// </summary>
    [Fact]
    public void Extract_TableCellWithLongParagraph_PreservedAsOwnSegment()
    {
        // Arrange: a table row whose second cell is a long descriptive sentence
        const string longCell =
            "This cell has a very long descriptive paragraph that should still be linted because it "
            + "exceeds the twenty five word limit comfortably in any reasonable interpretation";
        var markdown = $"| Notes | {longCell} |";

        // Act: execute the operation being tested
        var segments = MarkdownProseExtractor.Extract(markdown);

        // Assert: the long cell survives as its own segment with its full text intact
        Assert.Equal(2, segments.Count);
        Assert.Equal(longCell, segments[1].Text);
    }

    /// <summary>
    ///     Test that a pipe character inside an inline code span within a table cell is not
    ///     mistaken for a column delimiter (for example a regex alternation like <c>`a|b`</c>).
    /// </summary>
    [Fact]
    public void Extract_TableRowWithPipeInsideInlineCode_NotTreatedAsColumnSeparator()
    {
        // Act: execute the operation being tested
        var segments = MarkdownProseExtractor.Extract("| Pattern | Matches `a|b` exactly |");

        // Assert: the code span's internal pipe does not split the second cell in two
        Assert.Equal(2, segments.Count);
        Assert.Equal("Pattern", segments[0].Text);
        Assert.Equal("Matches `a|b` exactly", segments[1].Text);
    }

    /// <summary>
    ///     Test that consecutive plain text lines are merged into a single paragraph segment.
    /// </summary>
    [Fact]
    public void Extract_ConsecutiveTextLines_MergedIntoSingleParagraph()
    {
        // Act: execute the operation being tested
        var segments = MarkdownProseExtractor.Extract("Line one text.\nLine two text.");

        // Assert: verify expected behavior
        var segment = Assert.Single(segments);
        Assert.Equal(SegmentRole.Paragraph, segment.Role);
        Assert.Equal("Line one text. Line two text.", segment.Text);
    }

    /// <summary>
    ///     Test that <see cref="ProseSegment.ResolveLine"/> reports the true source line for an
    ///     offset within a multi-line paragraph, not just the paragraph's first line.
    /// </summary>
    [Fact]
    public void Extract_MultiLineParagraph_ResolveLineReportsEachLinesOwnLineNumber()
    {
        // Arrange: a five-line paragraph, where each line contributes one word to the merged text.
        var markdown = "One two.\nThree four.\nFive six.\nSeven eight.\nNine ten.";

        // Act: extract the paragraph segment
        var segments = MarkdownProseExtractor.Extract(markdown);
        var segment = Assert.Single(segments);

        // Assert: the merged text is one space-joined paragraph starting on line 1 ...
        Assert.Equal("One two. Three four. Five six. Seven eight. Nine ten.", segment.Text);
        Assert.Equal(1, segment.LineNumber);

        // ... but resolving the offset of each word reports that word's own source line, not
        // always line 1.
        Assert.Equal(1, segment.ResolveLine(segment.Text.IndexOf("One", StringComparison.Ordinal)));
        Assert.Equal(2, segment.ResolveLine(segment.Text.IndexOf("Three", StringComparison.Ordinal)));
        Assert.Equal(3, segment.ResolveLine(segment.Text.IndexOf("Five", StringComparison.Ordinal)));
        Assert.Equal(4, segment.ResolveLine(segment.Text.IndexOf("Seven", StringComparison.Ordinal)));
        Assert.Equal(5, segment.ResolveLine(segment.Text.IndexOf("Nine", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Test that a list item wrapped across multiple source lines (a "lazy continuation" line,
    ///     indented text with no list marker of its own, immediately following a list item with no
    ///     intervening blank line) is folded into the same <see cref="SegmentRole.ListItem"/>
    ///     segment, rather than the continuation line starting an unrelated new paragraph segment.
    ///     Regression test for a reported bug where an allowed phrase (e.g. "process error") split
    ///     across such a line wrap was never recognized, because the two halves landed in different,
    ///     disconnected segments.
    /// </summary>
    [Fact]
    public void Extract_ListItemWrappedAcrossLines_MergesIntoSingleListItemSegment()
    {
        // Arrange: a list item whose text wraps onto a second, indented continuation line.
        var markdown = "- The following describes a process\n  error that can occur during setup.";

        // Act: extract segments
        var segments = MarkdownProseExtractor.Extract(markdown);

        // Assert: a single list-item segment with the wrapped text merged, not two segments.
        var segment = Assert.Single(segments);
        Assert.Equal(SegmentRole.ListItem, segment.Role);
        Assert.Equal("The following describes a process error that can occur during setup.", segment.Text);
        Assert.Equal(1, segment.LineNumber);

        // ... and ResolveLine reports each half's true source line, not always line 1.
        Assert.Equal(1, segment.ResolveLine(segment.Text.IndexOf("following", StringComparison.Ordinal)));
        Assert.Equal(2, segment.ResolveLine(segment.Text.IndexOf("error", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Test that two distinct list items - the first wrapped across a line break, the second not -
    ///     remain separate segments: the wrapped-continuation merge must not bleed past the next line
    ///     that itself starts a new list item.
    /// </summary>
    [Fact]
    public void Extract_TwoListItemsFirstWrapped_RemainSeparateSegments()
    {
        // Arrange: item one wraps onto a continuation line; item two starts immediately after.
        var markdown = "- First item wraps across\n  a line break here.\n- Second item is separate.";

        // Act: extract segments
        var segments = MarkdownProseExtractor.Extract(markdown);

        // Assert: two list-item segments, not one merged segment.
        Assert.Equal(2, segments.Count);
        Assert.Equal(SegmentRole.ListItem, segments[0].Role);
        Assert.Equal("First item wraps across a line break here.", segments[0].Text);
        Assert.Equal(1, segments[0].LineNumber);
        Assert.Equal(SegmentRole.ListItem, segments[1].Role);
        Assert.Equal("Second item is separate.", segments[1].Text);
        Assert.Equal(3, segments[1].LineNumber);
    }

    /// <summary>
    ///     Test that a list item with an empty marker line (for example <c>- </c> with no text of its
    ///     own) still keeps <see cref="SegmentRole.ListItem"/>, rather than a following continuation
    ///     line resetting the buffer's role to <see cref="SegmentRole.Paragraph"/>. Regression test
    ///     for a review finding: the original fix tested <c>buffer.Length == 0</c> to decide whether a
    ///     new segment was starting, but an empty marker line leaves the buffer empty while a segment
    ///     is still active, so that test wrongly matched.
    /// </summary>
    [Fact]
    public void Extract_ListItemWithEmptyMarkerLineThenContinuation_KeepsListItemRole()
    {
        // Arrange: the marker line has no text; the next line is its wrapped continuation.
        var markdown = "- \n  Open the panel and check the gauge.";

        // Act: extract segments
        var segments = MarkdownProseExtractor.Extract(markdown);

        // Assert: one ListItem segment, not a Paragraph starting on line 2.
        var segment = Assert.Single(segments);
        Assert.Equal(SegmentRole.ListItem, segment.Role);
        Assert.Equal("Open the panel and check the gauge.", segment.Text);
        Assert.Equal(1, segment.LineNumber);
    }

    /// <summary>
    ///     Test that an empty list marker line followed immediately by a second, separate list item
    ///     does not leak a stale line-offset entry into the second item's segment. Regression test for
    ///     a review finding: <c>FlushBuffer</c> previously only cleared <c>lineOffsets</c> inside the
    ///     <c>buffer.Length &gt; 0</c> branch, so an empty marker's <c>(0, lineNumber)</c> entry
    ///     survived the flush and was inherited by whatever segment was accumulated next.
    /// </summary>
    [Fact]
    public void Extract_EmptyListMarkerThenSeparateItem_DoesNotLeakStaleLineOffset()
    {
        // Arrange: an empty marker item immediately followed by a genuine second item.
        var markdown = "- \n- Second item text.";

        // Act: extract segments
        var segments = MarkdownProseExtractor.Extract(markdown);

        // Assert: only the second item is emitted (the empty marker contributes no text), and its
        // line offsets/ResolveLine correctly point at line 2, not line 1.
        var segment = Assert.Single(segments);
        Assert.Equal("Second item text.", segment.Text);
        Assert.Equal(2, segment.LineNumber);
        Assert.Equal(2, segment.ResolveLine(0));
    }

    /// <summary>
    ///     Test that a nested (deeper-indented) list item is not folded as a wrapped-continuation
    ///     line into its parent's <see cref="SegmentRole.ListItem"/> segment. Regression test for a
    ///     review finding: the wrapped-continuation fallback only checked
    ///     <see cref="SegmentRole.Heading"/>/<see cref="SegmentRole.ListItem"/>/
    ///     <see cref="SegmentRole.TableRow"/> at up to 3 leading spaces (the top-level
    ///     <c>ListItemRegex</c> limit), so a nested item indented deeper than that fell through and
    ///     merged into the parent, letting a disallowed phrase/word-count check incorrectly span two
    ///     separate list items.
    /// </summary>
    [Fact]
    public void Extract_NestedListItem_DoesNotMergeIntoParentListItem()
    {
        // Arrange: a parent item with a nested child indented past the 3-space top-level limit.
        var markdown = "- Parent item text.\n    - Child item text.";

        // Act: extract segments
        var segments = MarkdownProseExtractor.Extract(markdown);

        // Assert: two distinct list-item segments, the parent's text unaffected by the child.
        Assert.Equal(2, segments.Count);
        Assert.Equal(SegmentRole.ListItem, segments[0].Role);
        Assert.Equal("Parent item text.", segments[0].Text);
        Assert.Equal(1, segments[0].LineNumber);
        Assert.Equal(SegmentRole.ListItem, segments[1].Role);
        Assert.Equal("Child item text.", segments[1].Text);
        Assert.Equal(2, segments[1].LineNumber);
    }

    /// <summary>
    ///     Test that <see cref="ProseSegment.ResolveLine"/> on a single-line segment (heading, list
    ///     item, or table row) always resolves to that segment's own line number, regardless of the
    ///     offset queried, since these segments are never folded from multiple source lines.
    /// </summary>
    [Fact]
    public void Extract_SingleLineSegment_ResolveLineAlwaysReturnsSegmentLine()
    {
        // Act: extract a heading on line 3 of a document
        var segments = MarkdownProseExtractor.Extract("\n\n# A Heading With Several Words");
        var segment = Assert.Single(segments);

        // Assert: every offset within the single-line segment resolves to its one line number
        Assert.Equal(3, segment.LineNumber);
        Assert.Equal(3, segment.ResolveLine(0));
        Assert.Equal(3, segment.ResolveLine(segment.Text.Length - 1));
    }

    /// <summary>
    ///     Test that a blank line separates two paragraphs into distinct segments.
    /// </summary>
    [Fact]
    public void Extract_BlankLineBetweenParagraphs_ProducesTwoSegments()
    {
        // Act: execute the operation being tested
        var segments = MarkdownProseExtractor.Extract("First paragraph.\n\nSecond paragraph.");

        // Assert: verify expected behavior
        Assert.Equal(2, segments.Count);
        Assert.Equal("First paragraph.", segments[0].Text);
        Assert.Equal("Second paragraph.", segments[1].Text);
    }

    /// <summary>
    ///     Test that content inside a fenced code block is excluded entirely from prose segments.
    /// </summary>
    [Fact]
    public void Extract_FencedCodeBlock_ExcludedFromProse()
    {
        // Arrange: a paragraph, a fenced code block, and another paragraph
        const string markdown = "Before text.\n\n```\nvar utilize = true;\n```\n\nAfter text.";

        // Act: execute the operation being tested
        var segments = MarkdownProseExtractor.Extract(markdown);

        // Assert: only the two prose paragraphs are extracted; code content is absent
        Assert.Equal(2, segments.Count);
        Assert.All(segments, s => Assert.DoesNotContain("utilize", s.Text));
    }

    /// <summary>
    ///     Test that an inline code span is retained verbatim in prose, alongside surrounding text.
    /// </summary>
    [Fact]
    public void Extract_InlineCodeSpan_KeptVerbatimInProse()
    {
        // Act: execute the operation being tested
        var segments = MarkdownProseExtractor.Extract("Run the `dotnet build` command to compile.");

        // Assert: the literal backticked code survives verbatim, alongside surrounding prose
        var segment = Assert.Single(segments);
        Assert.Contains("`dotnet build`", segment.Text);
        Assert.Contains("Run the", segment.Text);
        Assert.Contains("command to compile.", segment.Text);
    }

    /// <summary>
    ///     Test that a Markdown link's destination URL is removed while its visible text is kept.
    /// </summary>
    [Fact]
    public void Extract_InlineLink_KeepsTextRemovesDestination()
    {
        // Act: execute the operation being tested
        var segments = MarkdownProseExtractor.Extract("See the [user guide](https://example.com/guide) for details.");

        // Assert: verify expected behavior
        var segment = Assert.Single(segments);
        Assert.Contains("user guide", segment.Text);
        Assert.DoesNotContain("https://example.com/guide", segment.Text);
    }

    /// <summary>
    ///     Test that an empty document produces no segments.
    /// </summary>
    [Fact]
    public void Extract_EmptyDocument_ReturnsNoSegments()
    {
        // Act: execute the operation being tested
        var segments = MarkdownProseExtractor.Extract(string.Empty);

        // Assert: verify expected behavior
        Assert.Empty(segments);
    }

    /// <summary>
    ///     Test that Extract throws when given a null argument.
    /// </summary>
    [Fact]
    public void Extract_NullMarkdown_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => MarkdownProseExtractor.Extract(null!));
    }
}
