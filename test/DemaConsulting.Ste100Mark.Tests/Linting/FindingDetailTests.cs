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

using PartOfSpeech = DemaConsulting.Ste100Mark.Linting.PartOfSpeech;

/// <summary>
///     Unit tests for the FindingDetail class.
/// </summary>
public class FindingDetailTests
{
    /// <summary>
    ///     Builds evidence for the first occurrence of <paramref name="surface"/> in a segment.
    /// </summary>
    private static DictionaryEvidence MakeEvidence(
        string text,
        string surface,
        SegmentRole role = SegmentRole.Paragraph,
        int lineNumber = 1,
        IReadOnlyList<(int Offset, int Line)>? lineOffsets = null,
        int start = -1,
        IReadOnlyList<string>? formKinds = null,
        bool generated = false)
    {
        var segment = lineOffsets is null
            ? new ProseSegment(text, lineNumber, role)
            : new ProseSegment(text, lineNumber, role, lineOffsets);
        return new DictionaryEvidence(
            segment,
            start >= 0 ? start : text.IndexOf(surface, StringComparison.Ordinal),
            surface.Length,
            surface,
            "perform",
            formKinds ?? [FindingDetail.FormBase],
            false,
            false,
            generated,
            [new EvidenceSense("verb", ["carry out"])],
            "verb",
            1,
            [new EvidenceSuggestion("carry out", true, "verb")],
            new RoleEvidence(PartOfSpeech.Verb, "resolved", "verb", 0.99, 0.95, true, false, "noun", 0.04, true),
            FindingDetail.RuleNotApplicable,
            FindingDetail.RuleNotApplicable);
    }

    /// <summary>
    ///     Wraps evidence in a diagnostic.
    /// </summary>
    private static Diagnostic Wrap(DictionaryEvidence? evidence, int line = 1)
    {
        return new Diagnostic("doc.md", line, null, "STE100-DICT", Severity.Error, "message", null, null, evidence);
    }

    /// <summary>
    ///     Test that a diagnostic without evidence has no detail.
    /// </summary>
    [Fact]
    public void Build_NoEvidence_ReturnsNull()
    {
        // Act: build detail for a diagnostic with no evidence
        var detail = FindingDetail.Build(Wrap(null), ["text"], 2);

        // Assert: nothing to report
        Assert.Null(detail);
    }

    /// <summary>
    ///     Test that the sentence containing the finding is extracted, not the whole segment.
    /// </summary>
    [Fact]
    public void Build_Sentence_IsTheSentenceHoldingTheSpan()
    {
        // Arrange: a paragraph with two sentences and a finding in the second
        const string text = "Open the valve. We perform the test now.";

        // Act: build detail
        var detail = FindingDetail.Build(Wrap(MakeEvidence(text, "perform")), [text], 0);

        // Assert: only the second sentence is reported
        Assert.NotNull(detail);
        Assert.Equal("We perform the test now.", detail.Sentence.Text);
        Assert.False(detail.Sentence.Truncated);
    }

    /// <summary>
    ///     Test that a long sentence is capped at 400 characters around the span and flagged.
    /// </summary>
    [Fact]
    public void Build_LongSentence_IsCappedAndMarkedTruncated()
    {
        // Arrange: one very long sentence with the finding in the middle
        var text = string.Concat(Enumerable.Repeat("The valve stays shut ", 40)) + "and we perform the test " +
                   string.Concat(Enumerable.Repeat("while the report waits ", 40)) + "now.";

        // Act: build detail
        var detail = FindingDetail.Build(Wrap(MakeEvidence(text, "perform")), [text], 0);

        // Assert: capped, truncated, and still holding the flagged word
        Assert.NotNull(detail);
        Assert.True(detail.Sentence.Truncated);
        Assert.True(detail.Sentence.Text.Length <= FindingDetail.MaxSentenceLength);
        Assert.Contains("perform", detail.Sentence.Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an exact column is located by occurrence counting for the n-th occurrence.
    /// </summary>
    [Fact]
    public void Build_Span_LocatesNthOccurrenceColumn()
    {
        // Arrange: the same word twice on one line; the finding is the second occurrence
        const string text = "We perform it and perform it.";
        var second = text.LastIndexOf("perform", StringComparison.Ordinal);

        // Act: build detail
        var detail = FindingDetail.Build(Wrap(MakeEvidence(text, "perform", start: second)), ["> " + text[..0] + text], 0);

        // Assert: the column points at the second occurrence in the source line (two-character prefix)
        Assert.NotNull(detail);
        Assert.True(detail.Span.ColumnsExact);
        Assert.Equal(second + 3, detail.Span.StartColumn);
        Assert.Equal(second + 3 + "perform".Length, detail.Span.EndColumn);
        Assert.Equal(second, detail.Span.SegmentStart);
        Assert.Equal(second + 7, detail.Span.SegmentEnd);
    }

    /// <summary>
    ///     Test that columns are omitted when the text cannot be found in the source line.
    /// </summary>
    [Fact]
    public void Build_Span_ColumnsNotExactWhenSourceDiffers()
    {
        // Arrange: a segment whose source line has different text (markup removed from the segment)
        var evidence = MakeEvidence("We perform it.", "perform");

        // Act: build detail with an unrelated source line
        var detail = FindingDetail.Build(Wrap(evidence), ["Something else entirely."], 0);

        // Assert: line only, no columns
        Assert.NotNull(detail);
        Assert.False(detail.Span.ColumnsExact);
        Assert.Null(detail.Span.StartColumn);
        Assert.Null(detail.Span.EndColumn);
        Assert.Equal(1, detail.Span.StartLine);
    }

    /// <summary>
    ///     Test that a table cell accepts a column only when the text occurs once in the row.
    /// </summary>
    [Fact]
    public void Build_TableCell_RequiresSingleOccurrenceInRow()
    {
        // Arrange: a cell whose word also appears in another cell of the same row
        var evidence = MakeEvidence("We perform it.", "perform", SegmentRole.TableRow);

        // Act: build detail against a row with one and with two occurrences
        var single = FindingDetail.Build(Wrap(evidence), ["| a | We perform it. |"], 0);
        var twice = FindingDetail.Build(Wrap(evidence), ["| perform | We perform it. |"], 0);

        // Assert: exact for one occurrence, not exact for an ambiguous row
        Assert.NotNull(single);
        Assert.True(single.Span.ColumnsExact);
        Assert.NotNull(twice);
        Assert.False(twice.Span.ColumnsExact);
        Assert.Equal("table-cell", single.ContextKind);
    }

    /// <summary>
    ///     Test that a multi-line paragraph reports the true line of the span.
    /// </summary>
    [Fact]
    public void Build_Span_UsesResolvedSourceLine()
    {
        // Arrange: a paragraph folded from two source lines
        const string text = "Open the valve. We perform it.";
        var offset = text.IndexOf("We", StringComparison.Ordinal);
        var evidence = MakeEvidence(text, "perform", lineNumber: 4, lineOffsets: [(0, 4), (offset, 5)]);

        // Act: build detail
        var detail = FindingDetail.Build(Wrap(evidence, 5), ["", "", "", "Open the valve.", "We perform it."], 0);

        // Assert: the second source line
        Assert.NotNull(detail);
        Assert.Equal(5, detail.Span.StartLine);
        Assert.Equal(5, detail.Span.EndLine);
        Assert.Equal(4, detail.Span.StartColumn);
    }

    /// <summary>
    ///     Test that context lines surround the flagged line, are clamped to the file, and are
    ///     omitted for zero.
    /// </summary>
    [Fact]
    public void Build_Context_ClampsToFileAndOmitsForZero()
    {
        // Arrange: a five-line file with the finding on line 2
        string[] lines = ["one", "We perform it.", "three", "four", "five"];
        var evidence = MakeEvidence("We perform it.", "perform", lineNumber: 2);

        // Act: build with 2 context lines, 0 context lines
        var two = FindingDetail.Build(Wrap(evidence, 2), lines, 2);
        var zero = FindingDetail.Build(Wrap(evidence, 2), lines, 0);

        // Assert: lines 1 to 4 for two, none for zero
        Assert.NotNull(two);
        Assert.NotNull(two.Context);
        Assert.Equal(1, two.Context.StartLine);
        Assert.Equal(["one", "We perform it.", "three", "four"], two.Context.Lines);
        Assert.NotNull(zero);
        Assert.Null(zero.Context);
    }

    /// <summary>
    ///     Test that a long context line is capped.
    /// </summary>
    [Fact]
    public void Build_Context_CapsLongLines()
    {
        // Arrange: a very long source line
        var line = "We perform it. " + new string('x', 500);
        var evidence = MakeEvidence("We perform it.", "perform");

        // Act: build detail
        var detail = FindingDetail.Build(Wrap(evidence), [line], 1);

        // Assert: the context line is capped
        Assert.NotNull(detail?.Context);
        Assert.Equal(FindingDetail.MaxContextLineLength, detail.Context.Lines[0].Length);
    }

    /// <summary>
    ///     Test that the context kind follows the segment role and block quote marker.
    /// </summary>
    [Theory]
    [InlineData("Heading", "# We perform it.", "heading")]
    [InlineData("ListItem", "- We perform it.", "list-item")]
    [InlineData("Paragraph", "We perform it.", "paragraph")]
    [InlineData("Paragraph", "> We perform it.", "block-quote")]
    public void Build_ContextKind_FollowsSegmentRole(string roleName, string source, string expected)
    {
        // Arrange: evidence for the role
        var evidence = MakeEvidence("We perform it.", "perform", Enum.Parse<SegmentRole>(roleName));

        // Act: build detail
        var detail = FindingDetail.Build(Wrap(evidence), [source], 0);

        // Assert: the expected kind
        Assert.Equal(expected, detail?.ContextKind);
    }

    /// <summary>
    ///     Test the confidence table, one row per class and reason, with first-match precedence.
    /// </summary>
    [Theory]
    [InlineData("none", "unavailable", 1, false, false, "none", null, "doubtful", "role-inconclusive")]
    [InlineData("none", "multi-token", 1, false, false, "none", null, "doubtful", "role-inconclusive")]
    [InlineData("verb", "resolved", 2, false, false, "none", null, "doubtful", "multiple-senses")]
    [InlineData("none", "below-threshold", 1, false, false, "none", null, "doubtful", "tagger-below-threshold")]
    [InlineData("none", "unknown-word", 1, false, false, "none", null, "doubtful", "tagger-below-threshold")]
    [InlineData("none", "below-threshold", 2, false, false, "none", null, "doubtful", "multiple-senses")]
    [InlineData("verb", "resolved", 1, true, false, "none", null, "probable", "split-phrasal")]
    [InlineData("verb", "resolved", 1, false, true, "none", null, "probable", "hyphenated-form")]
    [InlineData("verb", "resolved", 1, false, false, "fragment", 0.99, "probable", "fragment-with-auxiliary")]
    [InlineData("verb", "resolved", 1, false, false, "participle", 0.20, "probable", "participle-low-margin")]
    [InlineData("verb", "resolved", 1, false, false, "participle", 0.80, "certain", "role-confident")]
    [InlineData("verb", "resolved", 1, false, false, "none", 0.10, "certain", "role-confident")]
    public void Classify_ConfidenceTable_ReturnsClassAndReason(
        string role,
        string outcome,
        int candidates,
        bool split,
        bool hyphenated,
        string rule,
        double? margin,
        string expectedClass,
        string expectedReason)
    {
        // Arrange: evidence with the scenario's signals
        var evidence = MakeEvidence("We perform it.", "perform") with
        {
            IsSplit = split,
            IsHyphenated = hyphenated,
            CandidateCount = candidates,
            Tagger = new RoleEvidence(role == "verb" ? PartOfSpeech.Verb : null, outcome, Margin: margin),
            ParticipleRule = rule == "participle" ? FindingDetail.RuleKeptConfidentVerb : FindingDetail.RuleNotApplicable,
            FragmentRule = rule == "fragment" ? FindingDetail.RuleKeptAuxiliaryPresent : FindingDetail.RuleNotApplicable
        };

        // Act: classify
        var confidence = FindingDetail.Classify(evidence);

        // Assert: the table row
        Assert.Equal(expectedClass, confidence.Class);
        Assert.Equal(expectedReason, confidence.Reason);
    }

    /// <summary>
    ///     Test that a base-term finding offers allow and allow-in-phrase, but never glossary.
    /// </summary>
    [Fact]
    public void Hints_BaseTerm_HasNoGlossaryOption()
    {
        // Arrange: a base-term finding
        var evidence = MakeEvidence("We perform it now.", "perform");

        // Act: build detail
        var detail = FindingDetail.Build(Wrap(evidence), ["We perform it now."], 0);

        // Assert: allow with the base term, and no glossary
        Assert.NotNull(detail);
        var kinds = detail.Suppression.Options.Select(o => o.Kind).ToList();
        Assert.Equal(["allow", "allow-in-phrase"], kinds);
        Assert.Equal("dictionary:\n  allow:\n    - perform", detail.Suppression.Options[0].Snippet);
    }

    /// <summary>
    ///     Test that a generated form offers a glossary option with a placeholder definition and a
    ///     part of speech chosen by form.
    /// </summary>
    [Theory]
    [InlineData("past", "adjective")]
    [InlineData("participle", "adjective")]
    [InlineData("gerund", "noun")]
    [InlineData("plural", "noun")]
    public void Hints_GlossaryOnlyForGeneratedForms(string kind, string expectedPos)
    {
        // Arrange: a generated form of the given kind
        var evidence = MakeEvidence("The Performed item.", "Performed", formKinds: [kind], generated: true);

        // Act: build detail
        var detail = FindingDetail.Build(Wrap(evidence), ["The Performed item."], 0);

        // Assert: allow uses the lower-case spelling; glossary has the placeholder and pos
        Assert.NotNull(detail);
        Assert.Equal("dictionary:\n  allow:\n    - performed", detail.Suppression.Options[0].Snippet);
        var glossary = Assert.Single(detail.Suppression.Options, o => o.Kind == "glossary");
        Assert.Equal(
            $"dictionary:\n  glossary:\n    performed:\n      definition: <describe the term>\n      pos: {expectedPos}",
            glossary.Snippet);
    }

    /// <summary>
    ///     Test that a third-person form has no glossary suggestion because no part of speech fits.
    /// </summary>
    [Fact]
    public void Hints_ThirdPersonForm_HasNoGlossaryOption()
    {
        // Arrange: a generated third-person form
        var evidence = MakeEvidence("It performs it.", "performs", formKinds: ["third-person"], generated: true);

        // Act: build detail
        var detail = FindingDetail.Build(Wrap(evidence), ["It performs it."], 0);

        // Assert: no glossary option
        Assert.NotNull(detail);
        Assert.DoesNotContain(detail.Suppression.Options, o => o.Kind == "glossary");
    }

    /// <summary>
    ///     Test that the phrase candidates use the neighbor words in the same sentence.
    /// </summary>
    [Fact]
    public void Hints_AllowInPhrase_UsesNeighborWords()
    {
        // Arrange: a finding with a word before and after, and a previous sentence that must not leak in
        const string text = "Close the valve. The crew must perform the test now.";

        // Act: build detail
        var detail = FindingDetail.Build(Wrap(MakeEvidence(text, "perform")), [text], 0);

        // Assert: the two candidates are previous+surface and surface+next
        Assert.NotNull(detail);
        var phrase = Assert.Single(detail.Suppression.Options, o => o.Kind == "allow-in-phrase");
        Assert.Equal("dictionary:\n  allow-in-phrase:\n    - must perform\n    - perform the", phrase.Snippet);
    }

    /// <summary>
    ///     Test that no phrase option is offered when the sentence holds only the flagged text.
    /// </summary>
    [Fact]
    public void Hints_AllowInPhrase_OmittedWithoutNeighbors()
    {
        // Arrange: a single-word sentence
        var detail = FindingDetail.Build(Wrap(MakeEvidence("Perform.", "Perform")), ["Perform."], 0);

        // Assert: only the allow option
        Assert.NotNull(detail);
        Assert.Equal(["allow"], detail.Suppression.Options.Select(o => o.Kind).ToList());
    }

    /// <summary>
    ///     Test that a neighbor word separated by punctuation is not used for a phrase, and that a
    ///     phrase with YAML special characters is quoted.
    /// </summary>
    [Fact]
    public void Hints_AllowInPhrase_SkipsPunctuationAndQuotesSpecialCharacters()
    {
        // Arrange: a quoted word before the finding, and a next word with a colon
        const string text = "The \"crew\" perform it.";
        const string colonText = "We perform: it.";

        // Act: build detail for both
        var quoted = FindingDetail.Build(Wrap(MakeEvidence(text, "perform")), [text], 0);
        var colon = FindingDetail.Build(Wrap(MakeEvidence(colonText, "perform")), [colonText], 0);

        // Assert: only the following word is used in the first; the second has no next word
        Assert.NotNull(quoted);
        var phrase = Assert.Single(quoted.Suppression.Options, o => o.Kind == "allow-in-phrase");
        Assert.Equal("dictionary:\n  allow-in-phrase:\n    - perform it", phrase.Snippet);
        Assert.NotNull(colon);
        Assert.Contains(colon.Suppression.Options, o => o.Kind == "allow-in-phrase");
    }

    /// <summary>
    ///     Test that a split phrasal finding allows the base term and offers no glossary.
    /// </summary>
    [Fact]
    public void Hints_SplitPhrasal_AllowsBaseTermWithoutGlossary()
    {
        // Arrange: a split phrasal finding
        const string text = "We carry the test out now.";
        var evidence = MakeEvidence(text, "carry the test out", generated: true) with
        {
            BaseTerm = "carry out",
            IsSplit = true
        };

        // Act: build detail
        var detail = FindingDetail.Build(Wrap(evidence), [text], 0);

        // Assert: base term in allow, form kind phrasal-split, no glossary
        Assert.NotNull(detail);
        Assert.Equal("dictionary:\n  allow:\n    - carry out", detail.Suppression.Options[0].Snippet);
        Assert.Equal("phrasal-split", detail.Match.FormKind);
        Assert.DoesNotContain(detail.Suppression.Options, o => o.Kind == "glossary");
    }

    /// <summary>
    ///     Test that a hyphenated finding allows the whole spelling and reports the hyphenated kind.
    /// </summary>
    [Fact]
    public void Hints_Hyphenated_AllowsPrefixedSpelling()
    {
        // Arrange: a hyphenated finding
        const string text = "We re-perform it now.";
        var evidence = MakeEvidence(text, "re-perform", generated: true) with { IsHyphenated = true };

        // Act: build detail
        var detail = FindingDetail.Build(Wrap(evidence), [text], 0);

        // Assert: the prefixed spelling in allow, hyphenated kind, no glossary
        Assert.NotNull(detail);
        Assert.Equal("dictionary:\n  allow:\n    - re-perform", detail.Suppression.Options[0].Snippet);
        Assert.Equal("hyphenated", detail.Match.FormKind);
        Assert.DoesNotContain(detail.Suppression.Options, o => o.Kind == "glossary");
    }

    /// <summary>
    ///     Test that the standing warning is always present.
    /// </summary>
    [Fact]
    public void Hints_AlwaysIncludeWarning()
    {
        // Act: build detail for a simple finding
        var detail = FindingDetail.Build(Wrap(MakeEvidence("We perform it.", "perform")), ["We perform it."], 0);

        // Assert: the warning text
        Assert.NotNull(detail);
        Assert.Equal(FindingDetail.SuppressionWarning, detail.Suppression.Warning);
        Assert.StartsWith("Use only for a true false positive", detail.Suppression.Warning, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the tagger evidence is carried through, including the threshold and runner-up.
    /// </summary>
    [Fact]
    public void Build_Tagger_CarriesEvidenceAndThreshold()
    {
        // Act: build detail
        var detail = FindingDetail.Build(Wrap(MakeEvidence("We perform it.", "perform")), ["We perform it."], 0);

        // Assert: tagger fields
        Assert.NotNull(detail);
        Assert.Equal("verb", detail.Tagger.Role);
        Assert.Equal("verb", detail.Tagger.Tag);
        Assert.Equal(0.99, detail.Tagger.Confidence);
        Assert.Equal(PartOfSpeechResolver.MinimumConfidence, detail.Tagger.Threshold);
        Assert.Equal("noun", detail.Tagger.RunnerUp?.Tag);
        Assert.Equal("resolved", detail.Tagger.Outcome);
    }

    /// <summary>
    ///     Test that a resolver without tagger evidence leaves the optional tagger fields empty.
    /// </summary>
    [Fact]
    public void Build_Tagger_WithoutEvidenceLeavesFieldsNull()
    {
        // Arrange: evidence from a resolver with no tagger data
        var evidence = MakeEvidence("We perform it.", "perform") with { Tagger = RoleEvidence.FromRole(null) };

        // Act: build detail
        var detail = FindingDetail.Build(Wrap(evidence), ["We perform it."], 0);

        // Assert: role, tag, margin and runner-up are empty and the outcome is unavailable
        Assert.NotNull(detail);
        Assert.Null(detail.Tagger.Role);
        Assert.Null(detail.Tagger.Tag);
        Assert.Null(detail.Tagger.Margin);
        Assert.Null(detail.Tagger.RunnerUp);
        Assert.Equal("unavailable", detail.Tagger.Outcome);
    }
}
