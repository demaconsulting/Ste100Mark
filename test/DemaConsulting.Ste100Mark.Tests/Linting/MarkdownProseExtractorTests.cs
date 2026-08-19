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
