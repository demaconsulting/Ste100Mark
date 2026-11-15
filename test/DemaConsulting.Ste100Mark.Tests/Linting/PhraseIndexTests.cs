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
using DemaConsulting.Ste100Mark.Linting;

namespace DemaConsulting.Ste100Mark.Tests.Linting;

/// <summary>
///     Unit tests for the PhraseIndex class.
/// </summary>
public class PhraseIndexTests
{
    /// <summary>
    ///     Test that a span equal to a phrase word is contained, and one outside the phrase is not.
    /// </summary>
    [Fact]
    public void Contains_SpanInsidePhrase_ReturnsTrueOnlyInsidePhrase()
    {
        // Arrange
        var index = new PhraseIndex(["Test Mode"]);
        const string text = "A test now and Test Mode here.";

        // Act / Assert
        Assert.False(index.Contains(text, 2, 4));
        Assert.True(index.Contains(text, 15, 4));
        Assert.True(index.Contains(text, 15, 9));
    }

    /// <summary>
    ///     Test that overlapping phrases are both honored.
    /// </summary>
    [Fact]
    public void Contains_OverlappingPhrases_BothHonored()
    {
        // Arrange
        var index = new PhraseIndex(["Test Mode", "Mode Controller"]);
        const string text = "Open Test Mode Controller now.";

        // Act / Assert
        Assert.True(index.Contains(text, 5, 4));
        Assert.True(index.Contains(text, 15, 10));
        Assert.False(index.Contains(text, 5, 20));
    }

    /// <summary>
    ///     Test that spaces in a phrase match any whitespace run, including tabs and line breaks.
    /// </summary>
    [Fact]
    public void Contains_WhitespaceRuns_Tolerated()
    {
        // Arrange
        var index = new PhraseIndex(["Test Mode"]);
        const string text = "Open Test \t\n  Mode now.";

        // Act / Assert
        Assert.True(index.Contains(text, 5, 4));
        Assert.True(index.Contains(text, 5, text.IndexOf("Mode", StringComparison.Ordinal) - 5 + 4));
    }

    /// <summary>
    ///     Test that matching ignores letter case.
    /// </summary>
    [Fact]
    public void Contains_DifferentCase_Matches()
    {
        // Arrange
        var index = new PhraseIndex(["Test Mode"]);

        // Act / Assert
        Assert.True(index.Contains("open the TEST MODE panel", 9, 4));
    }

    /// <summary>
    ///     Test that a hyphen before the phrase blocks the match.
    /// </summary>
    [Fact]
    public void Contains_HyphenBeforePhrase_NotContained()
    {
        // Arrange
        var index = new PhraseIndex(["test mode"]);

        // Act / Assert
        Assert.False(index.Contains("the pre-test mode here", 8, 4));
    }

    /// <summary>
    ///     Test that a phrase followed by a word character is not matched.
    /// </summary>
    [Fact]
    public void Contains_PhraseFollowedByWordCharacter_NotContained()
    {
        // Arrange
        var index = new PhraseIndex(["test mode"]);

        // Act / Assert
        Assert.False(index.Contains("the test modes here", 4, 4));
    }

    /// <summary>
    ///     Test that a phrase token with punctuation is indexed by its word run.
    /// </summary>
    [Fact]
    public void Contains_PhraseWithPunctuation_Matches()
    {
        // Arrange
        var index = new PhraseIndex(["(safe) mode"]);
        const string text = "Use (safe) mode now.";

        // Act / Assert
        Assert.True(index.Contains(text, 5, 4));
        Assert.True(index.Contains(text, 11, 4));
    }

    /// <summary>
    ///     Test that a phrase at the very start and end of the text is matched.
    /// </summary>
    [Fact]
    public void Contains_PhraseAtTextEdges_Matches()
    {
        // Arrange
        var index = new PhraseIndex(["test mode"]);

        // Act / Assert
        Assert.True(index.Contains("test mode", 0, 4));
        Assert.True(index.Contains("test mode", 5, 4));
    }

    /// <summary>
    ///     Test that blank phrases are ignored and an empty index contains nothing.
    /// </summary>
    [Fact]
    public void Contains_BlankPhraseOrNoPhrases_ReturnsFalse()
    {
        // Arrange
        var index = new PhraseIndex(["", "   "]);

        // Act / Assert
        Assert.False(index.Contains("test mode", 0, 4));
        Assert.False(index.Contains("  ", 0, 0));
    }

    /// <summary>
    ///     Differential test: for randomized phrases and texts, every hit-shaped span (starting and
    ///     ending on a word/hyphen token boundary) is classified exactly like the combined
    ///     lookahead regex that the index replaces.
    /// </summary>
    [Fact]
    public void Contains_RandomizedCorpus_MatchesCombinedRegexOracle()
    {
        // Arrange
        var random = new Random(12345);
        string[] vocabulary =
            ["test", "Test", "MODE", "mode", "re-test", "(safe)", "Zorbix", "Acme", "a_b", "x1", "bleed", "valve", "pre-test", "Ünï"];
        string[] separators = [" ", "  ", "\t", "\n", " \n ", ", ", ". ", " - ", "(", ")"];
        var checkedSpans = 0;

        for (var round = 0; round < 300; round++)
        {
            var phrases = Enumerable.Range(0, random.Next(1, 8))
                .Select(_ => string.Join(' ', Enumerable.Range(0, random.Next(2, 4)).Select(_ => vocabulary[random.Next(vocabulary.Length)])))
                .ToList();
            var oracle = new Regex(
                $@"(?<![\w-])(?=(?<phrase>(?:{string.Join('|', phrases.OrderByDescending(p => p.Length).Select(ToPattern))})(?![\w-])))",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var index = new PhraseIndex(phrases);
            var text = string.Concat(Enumerable.Range(0, random.Next(4, 14))
                .Select(_ => vocabulary[random.Next(vocabulary.Length)] + separators[random.Next(separators.Length)]));
            var spans = oracle.Matches(text).Select(m => (m.Groups["phrase"].Index, m.Groups["phrase"].Length)).ToList();

            // Act / Assert: every span from a token start to a token end
            var tokens = Regex.Matches(text, @"[\w-]+");
            foreach (Match first in tokens)
            {
                foreach (Match last in tokens.Where(t => t.Index >= first.Index))
                {
                    var start = first.Index;
                    var length = last.Index + last.Length - start;
                    var expected = spans.Any(s => start >= s.Item1 && start + length <= s.Item1 + s.Item2);
                    Assert.True(
                        expected == index.Contains(text, start, length),
                        $"phrases [{string.Join(" | ", phrases)}] text '{text}' span {start}+{length}");
                    checkedSpans++;
                }
            }
        }

        Assert.True(checkedSpans > 1000);
    }

    /// <summary>
    ///     Converts a phrase to the same whitespace-tolerant pattern the combined regex used.
    /// </summary>
    private static string ToPattern(string phrase) =>
        Regex.Escape(phrase).Replace(@"\ ", @"\s+", StringComparison.Ordinal);
}
