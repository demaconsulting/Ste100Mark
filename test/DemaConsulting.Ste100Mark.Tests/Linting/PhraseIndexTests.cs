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
    ///     Test that a single-word entry works, honors boundaries, and ignores case.
    /// </summary>
    [Fact]
    public void Contains_SingleWordPhrase_MatchesWholeWordOnly()
    {
        // Arrange
        var index = new PhraseIndex(["Zorbix"]);

        // Act / Assert
        Assert.True(index.Contains("Open zorbix now", 5, 6));
        Assert.False(index.Contains("Open zorbixes now", 5, 6));
        Assert.False(index.Contains("Open pre-zorbix now", 9, 6));
        Assert.False(index.Contains("Open zorbix-2 now", 5, 6));
    }

    /// <summary>
    ///     Test that a literal tab in a phrase matches only a tab, while a space matches any whitespace.
    /// </summary>
    [Fact]
    public void Contains_LiteralTabInPhrase_MatchesOnlyTab()
    {
        // Arrange
        var index = new PhraseIndex(["bleed\tvalve"]);

        // Act / Assert
        Assert.True(index.Contains("The bleed\tvalve now", 4, 5));
        Assert.False(index.Contains("The bleed valve now", 4, 5));
        Assert.False(index.Contains("The bleed\t\tvalve now", 4, 5));
    }

    /// <summary>
    ///     Test that a literal non-breaking space in a phrase matches only that character.
    /// </summary>
    [Fact]
    public void Contains_LiteralNonBreakingSpaceInPhrase_MatchesOnlyThatCharacter()
    {
        // Arrange
        var index = new PhraseIndex(["bleed\u00a0valve"]);

        // Act / Assert
        Assert.True(index.Contains("The bleed\u00a0valve now", 4, 5));
        Assert.False(index.Contains("The bleed valve now", 4, 5));
    }

    /// <summary>
    ///     Test that a space matches a non-breaking space in the text (regex whitespace class).
    /// </summary>
    [Fact]
    public void Contains_SpaceInPhrase_MatchesNonBreakingSpaceInText()
    {
        // Arrange
        var index = new PhraseIndex(["bleed valve"]);

        // Act / Assert
        Assert.True(index.Contains("The bleed\u00a0valve now", 4, 5));
    }

    /// <summary>
    ///     Test that consecutive spaces in a phrase need at least that many whitespace characters.
    /// </summary>
    [Fact]
    public void Contains_DoubleSpaceInPhrase_RequiresTwoWhitespaceCharacters()
    {
        // Arrange
        var index = new PhraseIndex(["bleed  valve"]);

        // Act / Assert
        Assert.False(index.Contains("The bleed valve now", 4, 5));
        Assert.True(index.Contains("The bleed  valve now", 4, 5));
        Assert.True(index.Contains("The bleed \t valve now", 4, 5));
    }

    /// <summary>
    ///     Test that a leading or trailing space in a phrase requires whitespace at that edge.
    /// </summary>
    [Fact]
    public void Contains_LeadingAndTrailingSpace_RequireWhitespaceAtEdges()
    {
        // Arrange
        var index = new PhraseIndex([" valve", "bleed "]);

        // Act / Assert: the lookbehind applies before the leading space, so it needs two spaces
        Assert.True(index.Contains("the  valve now", 5, 5));
        Assert.False(index.Contains("the valve now", 4, 5));
        Assert.False(index.Contains("valve now", 0, 5));
        Assert.True(index.Contains("the bleed  now", 4, 5));
        Assert.False(index.Contains("the bleed now", 4, 5));
        Assert.False(index.Contains("the bleed", 4, 5));
    }

    /// <summary>
    ///     Test that regex metacharacters in a phrase are literal.
    /// </summary>
    [Fact]
    public void Contains_MetacharactersInPhrase_AreLiteral()
    {
        // Arrange
        var index = new PhraseIndex(["a.b test", "(x) [y] mode", "C++ test", "Product #A test"]);

        // Act / Assert
        Assert.True(index.Contains("use a.b test now", 4, 3));
        Assert.False(index.Contains("use aXb test now", 4, 3));
        Assert.True(index.Contains("use (x) [y] mode now", 5, 1));
        Assert.True(index.Contains("use C++ test now", 4, 1));
        Assert.True(index.Contains("use C++ test now", 8, 4));
        Assert.True(index.Contains("use Product #A test now", 4, 7));
    }

    /// <summary>
    ///     Test that Unicode letters match ignoring case.
    /// </summary>
    [Fact]
    public void Contains_UnicodeLetters_MatchIgnoringCase()
    {
        // Arrange
        var index = new PhraseIndex(["Ünï mode"]);

        // Act / Assert
        Assert.True(index.Contains("the ÜNÏ MODE now", 4, 3));
    }

    /// <summary>
    ///     Test that an occurrence overlapping another occurrence of the same phrase is also honored.
    /// </summary>
    [Fact]
    public void Contains_SelfOverlappingOccurrences_AllHonored()
    {
        // Arrange: "a b a" occurs at 0 and at 4 in "a b a b a"; every occurrence is honored
        var index = new PhraseIndex(["a b a"]);
        const string text = "a b a b a";

        // Act / Assert
        Assert.True(index.Contains(text, 0, 1));
        Assert.True(index.Contains(text, 6, 1));
        Assert.True(index.Contains(text, 4, 5));
        Assert.False(index.Contains(text, 0, 9));
    }

    /// <summary>
    ///     Test that a span starting with punctuation is resolved through the first token inside it.
    /// </summary>
    [Fact]
    public void Contains_SpanStartingWithPunctuation_UsesFirstTokenInsideSpan()
    {
        // Arrange
        var index = new PhraseIndex(["Acme (Zorbix) panel"]);
        const string text = "Use the Acme (Zorbix) panel now.";

        // Act / Assert: "(Zorbix" starts with punctuation
        Assert.True(index.Contains(text, 13, 7));
        Assert.False(index.Contains("Use the (Zorbix) panel now.", 8, 7));
    }

    /// <summary>
    ///     Test that a span without any token character is resolved through its neighboring tokens.
    /// </summary>
    [Fact]
    public void Contains_PunctuationOnlySpan_UsesNeighboringTokens()
    {
        // Arrange
        var index = new PhraseIndex(["R&D", "Acme & Product A"]);

        // Act / Assert
        Assert.True(index.Contains("The R&D team", 5, 1));
        Assert.False(index.Contains("The R and & D team", 10, 1));
        Assert.True(index.Contains("The Acme & Product A team", 9, 1));
        Assert.False(index.Contains("The Acme and & Product team", 13, 1));
    }

    /// <summary>
    ///     Test that a phrase made only of punctuation cannot be indexed and is ignored.
    /// </summary>
    [Fact]
    public void Contains_PunctuationOnlyPhrase_Ignored()
    {
        // Arrange
        var index = new PhraseIndex(["&&", "( )"]);

        // Act / Assert
        Assert.False(index.Contains("a && b", 2, 2));
    }

    /// <summary>
    ///     Differential test: for randomized phrases (single words, doubled/edge spaces, tabs,
    ///     non-breaking spaces, punctuation, self-overlap) and texts, every span that starts and
    ///     ends on a non-whitespace character is classified exactly like the original per-phrase
    ///     regex implementation, kept here only as an oracle. The accepted differences are that every
    ///     occurrence is honored (the original <c>Matches</c> skipped an occurrence overlapping an
    ///     earlier one of the same phrase) and that case is matched by ordinal comparison (the original used
    ///     the .NET regex equivalence tables, so exotic folds such as the Kelvin sign are excluded).
    /// </summary>
    [Fact]
    public void Contains_RandomizedCorpus_MatchesPerPhraseRegexOracle()
    {
        // Arrange
        var random = new Random(12345);
        string[] vocabulary =
        [
            "test", "Test", "MODE", "mode", "re-test", "(safe)", "Zorbix", "Acme", "a_b", "x1", "bleed", "valve",
            "pre-test", "Ünï", "R&D", "&", "C++", "a", "b", ".net", "Product"
        ];
        string[] phraseSeparators = [" ", " ", " ", "  ", "\t", "\u00a0", " \t"];
        string[] textSeparators = [" ", "  ", "\t", "\n", " \n ", ", ", ". ", " - ", "(", ")", "\u00a0", " "];
        var checkedSpans = 0;

        for (var round = 0; round < 400; round++)
        {
            var phrases = Enumerable.Range(0, random.Next(1, 8))
                .Select(_ => BuildPhrase(random, vocabulary, phraseSeparators))
                .Where(p => Regex.IsMatch(p, @"[\w-]"))
                .ToList();
            var index = new PhraseIndex(phrases);
            var text = string.Concat(Enumerable.Range(0, random.Next(4, 12))
                .Select(_ => vocabulary[random.Next(vocabulary.Length)] + textSeparators[random.Next(textSeparators.Length)]));
            var original = phrases.SelectMany(p => OriginalSpans(p, text)).ToList();
            var every = phrases.SelectMany(p => EveryOccurrenceSpans(p, text)).ToList();

            // Act / Assert
            for (var start = 0; start < text.Length; start++)
            {
                if (char.IsWhiteSpace(text[start]))
                {
                    continue;
                }

                for (var end = start + 1; end <= text.Length; end++)
                {
                    if (char.IsWhiteSpace(text[end - 1]))
                    {
                        continue;
                    }

                    var expectedEvery = every.Any(s => start >= s.Start && end <= s.Start + s.Length);
                    var expectedOriginal = original.Any(s => start >= s.Start && end <= s.Start + s.Length);
                    var actual = index.Contains(text, start, end - start);
                    Assert.True(
                        expectedEvery == actual,
                        $"phrases [{string.Join(" | ", phrases.Select(Visible))}] text '{Visible(text)}' span {start}+{end - start}");
                    Assert.True(!expectedOriginal || actual, "the index must honor every original occurrence");
                    checkedSpans++;
                }
            }
        }

        Assert.True(checkedSpans > 10000);
    }

    /// <summary>
    ///     Builds a random phrase of one to three vocabulary words with random separators and
    ///     optional leading or trailing spaces.
    /// </summary>
    private static string BuildPhrase(Random random, string[] vocabulary, string[] separators)
    {
        var words = Enumerable.Range(0, random.Next(1, 4)).Select(_ => vocabulary[random.Next(vocabulary.Length)]).ToList();
        var phrase = words[0];
        foreach (var word in words.Skip(1))
        {
            phrase += separators[random.Next(separators.Length)] + word;
        }

        return (random.Next(8) == 0 ? " " : string.Empty) + phrase + (random.Next(8) == 0 ? " " : string.Empty);
    }

    /// <summary>
    ///     The original implementation: one regex per phrase scanned with <c>Matches</c>
    ///     (non-overlapping, left to right).
    /// </summary>
    private static IEnumerable<(int Start, int Length)> OriginalSpans(string phrase, string text) =>
        new Regex(
                $@"(?<![\w-]){ToPattern(phrase)}(?![\w-])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Matches(text)
            .Select(m => (m.Index, m.Length));

    /// <summary>
    ///     The same per-phrase pattern evaluated at every start position, so overlapping
    ///     occurrences of one phrase are all reported.
    /// </summary>
    private static IEnumerable<(int Start, int Length)> EveryOccurrenceSpans(string phrase, string text) =>
        new Regex(
                $@"(?<![\w-])(?=(?<phrase>{ToPattern(phrase)}(?![\w-])))",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Matches(text)
            .Select(m => (m.Groups["phrase"].Index, m.Groups["phrase"].Length));

    /// <summary>
    ///     Makes tabs and non-breaking spaces visible in assertion messages.
    /// </summary>
    private static string Visible(string value) =>
        value.Replace("\t", "<TAB>", StringComparison.Ordinal)
            .Replace("\u00a0", "<NBSP>", StringComparison.Ordinal)
            .Replace("\n", "<LF>", StringComparison.Ordinal);

    /// <summary>
    ///     Converts a phrase to the pattern the original implementation used.
    /// </summary>
    private static string ToPattern(string phrase) =>
        Regex.Escape(phrase).Replace(@"\ ", @"\s+", StringComparison.Ordinal);
}
