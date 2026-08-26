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
///     Unit tests for the SentenceAnalyzer class.
/// </summary>
public class SentenceAnalyzerTests
{
    /// <summary>
    ///     Test that a simple sentence splits into a single sentence with the expected word count.
    /// </summary>
    [Fact]
    public void Split_SingleSimpleSentence_ReturnsOneSentenceWithWordCount()
    {
        // Act: execute the operation being tested
        var sentences = SentenceAnalyzer.Split("This is a simple sentence.");

        // Assert: verify expected behavior
        var sentence = Assert.Single(sentences);
        Assert.Equal(5, sentence.WordCount);
    }

    /// <summary>
    ///     Test that multiple sentences terminated by periods are split correctly.
    /// </summary>
    [Fact]
    public void Split_MultipleSentences_ReturnsEachSentenceSeparately()
    {
        // Act: execute the operation being tested
        var sentences = SentenceAnalyzer.Split("First sentence. Second sentence.");

        // Assert: verify expected behavior
        Assert.Equal(2, sentences.Count);
        Assert.Equal("First sentence.", sentences[0].Text);
        Assert.Equal("Second sentence.", sentences[1].Text);
    }

    /// <summary>
    ///     Test that a colon introducing a vertical list is treated as a sentence terminator, per
    ///     Rule 4.1's treatment of a colon the same as a period.
    /// </summary>
    [Fact]
    public void Split_ColonFollowedByCapital_TreatedAsSentenceTerminator()
    {
        // Act: execute the operation being tested
        var sentences = SentenceAnalyzer.Split("Do the following: Open the panel.");

        // Assert: verify expected behavior
        Assert.Equal(2, sentences.Count);
    }

    /// <summary>
    ///     Test that empty text produces no sentences.
    /// </summary>
    [Fact]
    public void Split_EmptyText_ReturnsNoSentences()
    {
        // Act: execute the operation being tested
        var sentences = SentenceAnalyzer.Split("   ");

        // Assert: verify expected behavior
        Assert.Empty(sentences);
    }

    /// <summary>
    ///     Test that a sentence starting with an inline code span is still recognized as a new
    ///     sentence, rather than being merged with the previous sentence.
    /// </summary>
    [Fact]
    public void Split_SentenceStartingWithCodeSpan_TreatedAsSeparateSentence()
    {
        // Act: execute the operation being tested
        var sentences = SentenceAnalyzer.Split("Run the build first. `dotnet build` compiles the project.");

        // Assert: verify expected behavior
        Assert.Equal(2, sentences.Count);
        Assert.Equal("Run the build first.", sentences[0].Text);
        Assert.Equal("`dotnet build` compiles the project.", sentences[1].Text);
    }

    /// <summary>
    ///     Test that a sentence starting with italic or bold emphasis is still recognized as a new
    ///     sentence, rather than being merged with the previous sentence.
    /// </summary>
    [Fact]
    public void Split_SentenceStartingWithEmphasis_TreatedAsSeparateSentence()
    {
        // Act: execute the operation being tested
        var sentences = SentenceAnalyzer.Split("This is done first. *Emphasis* starts the next sentence.");

        // Assert: verify expected behavior
        Assert.Equal(2, sentences.Count);
        Assert.Equal("This is done first.", sentences[0].Text);
        Assert.Equal("*Emphasis* starts the next sentence.", sentences[1].Text);
    }

    /// <summary>
    ///     Test that a parenthetical span counts as exactly one word within its containing sentence
    ///     (Rule 8.5), regardless of how many words it contains.
    /// </summary>
    [Fact]
    public void CountWords_ParentheticalSpan_CountsAsOneWord()
    {
        // Arrange: five words outside the parenthetical, plus one four-word parenthetical
        const string sentence = "Check the panel (see the reference guide) carefully.";

        // Act: execute the operation being tested
        var count = SentenceAnalyzer.CountWords(sentence);

        // Assert: "Check the panel P carefully." => 5 words
        Assert.Equal(5, count);
    }

    /// <summary>
    ///     Test that a parenthetical which itself forms a complete sentence is also extracted as a
    ///     separate sentence by Split, per Rule 8.5.
    /// </summary>
    [Fact]
    public void Split_ParentheticalFormingCompleteSentence_ExtractedSeparately()
    {
        // Act: execute the operation being tested
        var sentences = SentenceAnalyzer.Split("Check the panel (See the reference guide.) carefully.");

        // Assert: the containing sentence and the extracted parenthetical sentence are both present
        Assert.Equal(2, sentences.Count);
        Assert.Contains(sentences, s => s.Text == "See the reference guide.");
    }

    /// <summary>
    ///     Test that a hyphenated word counts as a single word (Rule 8.7).
    /// </summary>
    [Fact]
    public void CountWords_HyphenatedWord_CountsAsOneWord()
    {
        // Act: execute the operation being tested
        var count = SentenceAnalyzer.CountWords("This is a well-known fact.");

        // Assert: "This is a well-known fact." => 5 words
        Assert.Equal(5, count);
    }

    /// <summary>
    ///     Test that a number with a unit counts as a single word (Rule 8.6).
    /// </summary>
    [Fact]
    public void CountWords_NumberWithUnit_CountsAsOneWord()
    {
        // Act: execute the operation being tested
        var count = SentenceAnalyzer.CountWords("Tighten the bolt to 10 Nm of torque.");

        // Assert: "Tighten the bolt to N of torque." => 7 words
        Assert.Equal(7, count);
    }

    /// <summary>
    ///     Test that quoted text counts as a single word (Rule 8.6).
    /// </summary>
    [Fact]
    public void CountWords_QuotedText_CountsAsOneWord()
    {
        // Act: execute the operation being tested
        var count = SentenceAnalyzer.CountWords("Select the \"Advanced Settings Panel\" option.");

        // Assert: "Select the Q option." => 4 words
        Assert.Equal(4, count);
    }

    /// <summary>
    ///     Test that a title-case proper-noun-like sequence counts as a single word (Rule 8.6).
    /// </summary>
    [Fact]
    public void CountWords_TitleCaseSequence_CountsAsOneWord()
    {
        // Act: execute the operation being tested
        var count = SentenceAnalyzer.CountWords("Contact the International Standards Organization today.");

        // Assert: "Contact the T today." => 4 words
        Assert.Equal(4, count);
    }

    /// <summary>
    ///     Test that whitespace-only sentence text produces a word count of zero.
    /// </summary>
    [Fact]
    public void CountWords_WhitespaceOnly_ReturnsZero()
    {
        // Act: execute the operation being tested
        var count = SentenceAnalyzer.CountWords("   ");

        // Assert: verify expected behavior
        Assert.Equal(0, count);
    }

    /// <summary>
    ///     Test that an inline code span counts as exactly one word (Rule 8.6 extension),
    ///     regardless of how many tokens appear inside it.
    /// </summary>
    [Fact]
    public void CountWords_InlineCodeSpan_CountsAsOneWord()
    {
        // Act: execute the operation being tested
        var count = SentenceAnalyzer.CountWords("Run the `dotnet build --configuration Release` command.");

        // Assert: "Run the C command." => 4 words
        Assert.Equal(4, count);
    }

    /// <summary>
    ///     Test that a sentence containing an inline code span keeps the literal code text
    ///     (backticks included) verbatim in <see cref="Sentence.Text"/> for diagnostic display.
    /// </summary>
    [Fact]
    public void Split_SentenceWithInlineCodeSpan_KeepsVerbatimText()
    {
        // Act: execute the operation being tested
        var sentences = SentenceAnalyzer.Split("Run the `dotnet build` command.");

        // Assert: the sentence text still contains the literal backticked code
        var sentence = Assert.Single(sentences);
        Assert.Contains("`dotnet build`", sentence.Text);
    }
}
