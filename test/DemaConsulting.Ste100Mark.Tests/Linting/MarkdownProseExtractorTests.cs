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
