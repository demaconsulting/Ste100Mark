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
///     Unit tests for the DictionaryChecker class.
/// </summary>
public class DictionaryCheckerTests
{
    /// <summary>
    ///     Test that a disallowed embedded term is flagged with the correct rule code and suggestion.
    /// </summary>
    [Fact]
    public void Evaluate_DisallowedEmbeddedTerm_FlagsDiagnosticWithSuggestion()
    {
        // Arrange: the embedded dictionary's "utilize" entry
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Please utilize the tool.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("STE100-DICT", diagnostic.RuleCode);
        Assert.Equal(Severity.Error, diagnostic.Severity);
        Assert.Equal("use", diagnostic.Suggestion);
    }

    /// <summary>
    ///     Test that a multi-word disallowed phrase is matched even across normal whitespace.
    /// </summary>
    [Fact]
    public void Evaluate_MultiWordPhrase_FlagsDiagnostic()
    {
        // Arrange: the embedded dictionary's "prior to" entry
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Check the oil prior to driving.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("before", diagnostic.Suggestion);
    }

    /// <summary>
    ///     Test that matching is case-insensitive.
    /// </summary>
    [Fact]
    public void Evaluate_DifferentCasing_StillFlagsDiagnostic()
    {
        // Arrange: the embedded dictionary's "utilize" entry, capitalized differently in prose
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("UTILIZE the tool.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Single(diagnostics);
    }

    /// <summary>
    ///     Test that matching is whole-word only: a term embedded within a longer word is not
    ///     flagged.
    /// </summary>
    [Fact]
    public void Evaluate_TermEmbeddedInLongerWord_NotFlagged()
    {
        // Arrange: "utilized" should not trigger the "utilize" entry (different word)
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("The tool was utilized yesterday.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that prose containing no disallowed terms produces no diagnostics.
    /// </summary>
    [Fact]
    public void Evaluate_NoDisallowedTerms_ReturnsNoDiagnostics()
    {
        // Arrange: a sentence with no disallowed vocabulary
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Open the panel and check the display.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that a project-supplied dictionary override changes the reported suggestion.
    /// </summary>
    [Fact]
    public void Evaluate_InlineOverriddenTerm_UsesOverriddenSuggestion()
    {
        // Arrange: override the embedded "utilize" entry's alternative via inline config
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    ["utilize"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Verb, Alternatives = ["employ"] }]
                }
            }
        };
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Please utilize the tool.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("employ", diagnostic.Suggestion);
    }

    /// <summary>
    ///     Test that an allow-listed term is no longer flagged.
    /// </summary>
    [Fact]
    public void Evaluate_AllowListedTerm_NotFlagged()
    {
        // Arrange: allow-list "utilize"
        var config = new LintConfig { Dictionary = new DictionaryConfig { Allow = ["utilize"] } };
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Please utilize the tool.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that a single-sense term is always reported using its one sense, even when it
    ///     appears in a context whose signals would otherwise suggest the opposite grammatical
    ///     role - proving the "one sense means no ambiguity is possible" short-circuit.
    /// </summary>
    [Fact]
    public void Evaluate_SingleSenseTerm_AlwaysReportedRegardlessOfContext()
    {
        // Arrange: "utilize" is a single-sense, verb-only embedded entry, placed here after "the"
        // (a noun-leaning signal) which would otherwise conflict with its verb-only sense.
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Check the utilize option.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: reported unconditionally with its one sense's suggestion, no POS label
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("use", diagnostic.Suggestion);
        Assert.DoesNotContain("used as a", diagnostic.Message);
        Assert.DoesNotContain("ambiguous", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a multi-sense term in a confident noun-leaning context (preceded by an
    ///     article) reports only the noun sense.
    /// </summary>
    [Fact]
    public void Evaluate_MultiSenseTerm_NounContext_ReportsNounSense()
    {
        // Arrange: the embedded "impact" entry has a noun sense (effect) and a verb sense (affect)
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("The impact was clear.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: only the noun sense is reported
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("effect", diagnostic.Suggestion);
        Assert.Contains("used as a noun", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a multi-sense term in a confident verb-leaning context (preceded by a modal
    ///     auxiliary) reports only the verb sense.
    /// </summary>
    [Fact]
    public void Evaluate_MultiSenseTerm_VerbContext_ReportsVerbSense()
    {
        // Arrange: the embedded "impact" entry has a noun sense (effect) and a verb sense (affect)
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("This will impact the results.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: only the verb sense is reported
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("affect", diagnostic.Suggestion);
        Assert.Contains("used as a verb", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a multi-sense term with no confidently-resolving context signal reports every
    ///     sense, labeled as ambiguous.
    /// </summary>
    [Fact]
    public void Evaluate_MultiSenseTerm_AmbiguousContext_ReportsAllSensesAmbiguous()
    {
        // Arrange: bare "Impact" with no surrounding noun or verb signal words
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Impact is important.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: both senses are reported, clearly labeled as ambiguous
        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains(
            "Ambiguous part of speech for 'Impact' \u2014 possible corrections: as a noun, use 'effect'; as a verb, use 'affect'.",
            diagnostic.Message);
        Assert.Contains("effect", diagnostic.Suggestion);
        Assert.Contains("affect", diagnostic.Suggestion);
    }

    /// <summary>
    ///     Test that a confidently-resolved sense with exactly one alternative embeds it plainly,
    ///     with no "or" joining word.
    /// </summary>
    [Fact]
    public void Evaluate_ConfidentSenseSingleAlternative_NoOrInMessage()
    {
        // Arrange: the embedded "utilize" entry has a single alternative ("use")
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Please utilize the tool.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: the single alternative is embedded with no "or" joining word
        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("use 'use' instead", diagnostic.Message);
        Assert.DoesNotContain(" or ", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a confidently-resolved sense with two alternatives joins them with "or" and no
    ///     Oxford comma.
    /// </summary>
    [Fact]
    public void Evaluate_ConfidentSenseTwoAlternatives_JoinsWithOrNoOxfordComma()
    {
        // Arrange: override "utilize" with a single, deterministic 2-alternative verb sense
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    ["utilize"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Verb, Alternatives = ["employ", "use"] }]
                }
            }
        };
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Please utilize the tool.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: the two alternatives are joined with "or" and no Oxford comma
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Avoid 'utilize'; use 'employ' or 'use' instead.", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a confidently-resolved sense with three or more alternatives joins them with an
    ///     Oxford comma before the final "or", while the separate <see cref="Diagnostic.Suggestion"/>
    ///     field remains a plain comma-separated list.
    /// </summary>
    [Fact]
    public void Evaluate_ConfidentSenseThreeOrMoreAlternatives_JoinsWithOxfordCommaBeforeOr()
    {
        // Arrange: the new embedded "produce" entry has four alternatives (single-sense, no POS label)
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("They produce a lot of heat.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: the message uses an Oxford comma before "or"; the suggestion stays a plain list
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Avoid 'produce'; use 'cause', 'give', 'make', or 'supply' instead.", diagnostic.Message);
        Assert.Equal("cause, give, make, supply", diagnostic.Suggestion);
    }

    /// <summary>
    ///     Test that an ambiguous multi-sense term groups each sense's alternatives per part of
    ///     speech, applying both the 2-alternative and 3+-alternative join rules within the same
    ///     message.
    /// </summary>
    [Fact]
    public void Evaluate_AmbiguousMultiSenseTerm_GroupsAlternativesPerSenseWithNaturalJoin()
    {
        // Arrange: a fictitious, originally-authored term with a 2-alt noun sense and a 3-alt verb
        // sense, in a bare context with no confidently-resolving signal
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    ["deploy"] =
                    [
                        new DictionarySenseYaml { Pos = PartOfSpeech.Noun, Alternatives = ["setup", "arrangement"] },
                        new DictionarySenseYaml { Pos = PartOfSpeech.Verb, Alternatives = ["place", "set up", "install"] }
                    ]
                }
            }
        };
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Deploy is next.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: both senses are grouped and joined per the natural-language join rules
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(
            "Ambiguous part of speech for 'Deploy' \u2014 possible corrections: as a noun, use 'setup' or 'arrangement'; as a verb, use 'place', 'set up', or 'install'.",
            diagnostic.Message);
    }

    /// <summary>
    ///     Test that a single-sense, <c>pos: any</c> connector-phrase term is always reported
    ///     unconditionally, unaffected by any heuristic signal.
    /// </summary>
    [Fact]
    public void Evaluate_AnyPosSingleSenseTerm_AlwaysReported()
    {
        // Arrange: the embedded "prior to" entry is a single-sense, pos: any connector phrase
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Check the oil prior to driving.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Procedure);

        // Assert: reported unconditionally with its one sense's suggestion
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("before", diagnostic.Suggestion);
        Assert.DoesNotContain("ambiguous", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a disallowed term appearing only inside an inline code span is not flagged,
    ///     since inline code content is excluded from the dictionary check.
    /// </summary>
    [Fact]
    public void Evaluate_DisallowedTermOnlyInsideInlineCode_NotFlagged()
    {
        // Arrange: "utilize" appears only inside an inline code span
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Run the `utilize` flag.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that a disallowed term appearing both inside an inline code span and in
    ///     surrounding prose in the same segment is flagged only for the prose occurrence.
    /// </summary>
    [Fact]
    public void Evaluate_DisallowedTermInsideAndOutsideInlineCode_FlagsOnlyProseOccurrence()
    {
        // Arrange: "utilize" appears once inside an inline code span and once as prose
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments =
        [
            new ProseSegment("Please utilize the tool, not the `utilize` flag.", 1, SegmentRole.Paragraph)
        ];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: exactly one diagnostic, for the prose occurrence
        Assert.Single(diagnostics);
    }
}
