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
    ///     Test that a dictionary violation positioned on a later line of a multi-line paragraph
    ///     reports that sentence's own source line, not the paragraph's first line. Regression test
    ///     for the reported bug where every dictionary finding in a multi-line paragraph was
    ///     reported at the paragraph's start line regardless of where the disallowed term actually
    ///     appeared.
    /// </summary>
    [Fact]
    public void Evaluate_DisallowedTermOnLaterLineOfMultiLineParagraph_ReportsThatLine()
    {
        // Arrange: a four-line paragraph (extracted end-to-end, so line numbers are real), where
        // only the fourth line contains a disallowed embedded-dictionary term ("utilize").
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        var markdown = string.Join(
            '\n',
            "The first line has no issue.",
            "The second line has no issue.",
            "The third line has no issue.",
            "Please utilize the tool on the fourth line.");
        var segments = MarkdownProseExtractor.Extract(markdown);

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: the diagnostic reports line 4 (where "utilize" appears), not line 1 (the
        // paragraph's start line).
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(4, diagnostic.Line);
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
    ///     Test that a term is not flagged when it appears in the per-file
    ///     <c>extraAllowedTerms</c> collection, even though the merged dictionary itself still
    ///     disallows it - the mechanism <see cref="LintConfig.ResolveAllowedTerms"/> uses to permit
    ///     "shall" for a requirements-documents profile without allow-listing it project-wide.
    /// </summary>
    [Fact]
    public void Evaluate_TermInExtraAllowedTerms_NotFlagged()
    {
        // Arrange: "utilize" remains disallowed in the merged dictionary, but is passed as a
        // per-file allowed term (simulating a matching profile's dictionary allow delta)
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Please utilize the tool.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate(
            "file.md", segments, dictionary, LintMode.Descriptive, extraAllowedTerms: ["utilize"]);

        // Assert: verify expected behavior
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that <c>extraAllowedTerms</c> matching is case-insensitive, consistent with every
    ///     other dictionary term comparison in this checker.
    /// </summary>
    [Fact]
    public void Evaluate_ExtraAllowedTermsDifferentCasing_StillSuppressesDiagnostic()
    {
        // Arrange: the extra-allowed term is supplied in a different case than the dictionary key
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Please utilize the tool.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate(
            "file.md", segments, dictionary, LintMode.Descriptive, extraAllowedTerms: ["UTILIZE"]);

        // Assert: verify expected behavior
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that supplying an unrelated <c>extraAllowedTerms</c> entry does not suppress a
    ///     different disallowed term still present in the segment.
    /// </summary>
    [Fact]
    public void Evaluate_ExtraAllowedTermsUnrelatedTerm_StillFlagsOtherDisallowedTerm()
    {
        // Arrange: allow "shall" (not present in this prose) while "utilize" remains disallowed
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Please utilize the tool.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate(
            "file.md", segments, dictionary, LintMode.Descriptive, extraAllowedTerms: ["shall"]);

        // Assert: "utilize" is still reported since it was not in the extra-allowed set
        Assert.Single(diagnostics);
    }

    /// <summary>
    ///     Test that a single-sense term is still reported using its one sense when the guesser
    ///     is inconclusive (no confident signal either way), and that a single-sense entry's
    ///     message is never POS-labeled.
    /// </summary>
    [Fact]
    public void Evaluate_SingleSenseTerm_InconclusiveContext_ReportedWithoutPosLabel()
    {
        // Arrange: "utilize" is a single-sense, verb-only embedded entry; "Please" triggers no
        // noun or verb signal, so the guesser is inconclusive and the sole sense is reported.
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Please utilize the tool.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: reported with its one sense's suggestion, no POS label
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("use", diagnostic.Suggestion);
        Assert.DoesNotContain("used as a", diagnostic.Message);
        Assert.DoesNotContain("ambiguous", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a single-sense term is suppressed (no diagnostic) when the guesser
    ///     confidently resolves a grammatical role that the term's one sense does not restrict -
    ///     the regression case for the "POS fallback never runs for single-sense entries" bug.
    /// </summary>
    [Fact]
    public void Evaluate_SingleSenseVerbOnlyTerm_ConfidentNounContext_NotFlagged()
    {
        // Arrange: "utilize" is a single-sense, verb-only embedded entry. Preceded by "the" (an
        // article, a confident noun signal) and followed by a noun, the guesser confidently
        // resolves Noun, which the verb-only entry does not restrict.
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Check the utilize option.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: not disallowed in this (noun) role, so no diagnostic is reported
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that a multi-sense term is suppressed (no diagnostic) when the guesser confidently
    ///     resolves a grammatical role that none of the term's senses cover (for example, an
    ///     entry with only adjective/verb senses matched in a confident noun context).
    /// </summary>
    [Fact]
    public void Evaluate_MultiSenseTerm_ConfidentGuessMatchesNoSense_NotFlagged()
    {
        // Arrange: a project-supplied entry with adjective and verb senses only (no noun sense).
        // "The reverse" is preceded by an article, a confident noun signal that matches neither.
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    ["reverse"] =
                    [
                        new DictionarySenseYaml { Pos = PartOfSpeech.Adjective, Alternatives = ["opposite"] },
                        new DictionarySenseYaml { Pos = PartOfSpeech.Verb, Alternatives = ["turn around"] }
                    ]
                }
            }
        };
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Set the reverse to neutral.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: the confident noun guess matches neither the adjective nor verb sense
        Assert.Empty(diagnostics);
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
        // Arrange: no signal word around the match; adding a finite verb elsewhere in the segment
        // keeps the whole-segment noun signal from firing here.
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("They discuss Impact sometimes. The team operates daily.", 1, SegmentRole.Paragraph)];

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
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Deploy happens soon. The team operates daily.", 1, SegmentRole.Paragraph)];

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

    /// <summary>
    ///     Shared verbless-segment noun-signal fixture: <c>wash</c>, <c>pump</c> and <c>probe</c>
    ///     each have a single verb-only sense, while <c>arrangement</c> and <c>state</c> each have
    ///     a noun sense - matching the reported ASD-STE100 corpus false-positive shape (verb-only
    ///     entries used as nouns in verbless fragments alongside genuine noun-sense findings in
    ///     the same cell).
    /// </summary>
    private static LintDictionary VerblessSegmentFixtureDictionary()
    {
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    ["wash"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Verb, Alternatives = ["clean"] }],
                    ["pump"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Verb, Alternatives = ["move"] }],
                    ["probe"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Verb, Alternatives = ["find"] }],
                    ["function"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Verb, Alternatives = ["operate"] }],
                    ["arrangement"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Noun, Alternatives = ["configuration"] }],
                    ["state"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Noun, Alternatives = ["condition"] }]
                }
            }
        };
        return LintDictionary.Load(config, Directory.GetCurrentDirectory());
    }

    /// <summary>
    ///     Test that a verbless table-row cell using verb-only dictionary terms as a noun phrase
    ///     produces no diagnostics, for both the "wash" and "pump" terms in the same cell.
    /// </summary>
    [Fact]
    public void Evaluate_VerblessTableCellNounPhrase_NotFlagged()
    {
        // Arrange: "Wash pump" - a verbless noun-phrase cell
        var dictionary = VerblessSegmentFixtureDictionary();
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Wash pump", 1, SegmentRole.TableRow)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that a verbless table-header cell using a verb-only dictionary term as a noun
    ///     label produces no diagnostic.
    /// </summary>
    [Fact]
    public void Evaluate_VerblessTableHeaderCell_NotFlagged()
    {
        // Arrange: "Function" as a bare table header label
        var dictionary = VerblessSegmentFixtureDictionary();
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Function", 1, SegmentRole.TableRow)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that a verbless list-item noun-phrase fragment using a verb-only dictionary term
    ///     produces no diagnostic.
    /// </summary>
    [Fact]
    public void Evaluate_VerblessListItemNounPhrase_NotFlagged()
    {
        // Arrange: "Probe geometry and diameter." - no finite verb anywhere in the fragment
        var dictionary = VerblessSegmentFixtureDictionary();
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Probe geometry and diameter.", 1, SegmentRole.ListItem)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that a verbless comma-separated list fragment using a verb-only dictionary term
    ///     produces no diagnostic.
    /// </summary>
    [Fact]
    public void Evaluate_VerblessCommaSeparatedListFragment_NotFlagged()
    {
        // Arrange: "Holds. Metering device, probe, coupling, fluid." - no finite verb anywhere
        var dictionary = VerblessSegmentFixtureDictionary();
        IReadOnlyList<ProseSegment> segments =
            [new ProseSegment("Holds. Metering device, probe, coupling, fluid.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that a verbless heading fragment using a verb-only dictionary term produces no
    ///     diagnostic.
    /// </summary>
    [Fact]
    public void Evaluate_VerblessHeadingFragment_NotFlagged()
    {
        // Arrange: "Wash Tower" - a heading fragment with no finite verb anywhere
        var dictionary = VerblessSegmentFixtureDictionary();
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Wash Tower", 1, SegmentRole.Heading)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that a genuine noun-sense finding ("arrangement") is still flagged even inside a
    ///     verbless table cell that also contains verb-only terms ("wash", "pump") that must be
    ///     suppressed - the discriminator is the individual term's sense set, not the segment kind.
    /// </summary>
    [Fact]
    public void Evaluate_VerblessCellWithNounSenseTerm_StillFlagsNounSenseTerm()
    {
        // Arrange: "Drive arrangement for the wash pump" - "arrangement" has a noun sense and
        // must still be reported, while "wash" and "pump" (verb-only) must not be
        var dictionary = VerblessSegmentFixtureDictionary();
        IReadOnlyList<ProseSegment> segments =
            [new ProseSegment("Drive arrangement for the wash pump", 1, SegmentRole.TableRow)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: only "arrangement" is reported
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("configuration", diagnostic.Suggestion);
    }

    /// <summary>
    ///     Test that a genuine noun-sense finding ("state") is still flagged in a verbless
    ///     multi-cell table row fragment.
    /// </summary>
    [Fact]
    public void Evaluate_VerblessCellWithStateNounSense_StillFlagsFinding()
    {
        // Arrange: "Bears on | State" - "state" has a noun sense and must still be reported
        var dictionary = VerblessSegmentFixtureDictionary();
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("State", 1, SegmentRole.TableRow)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("condition", diagnostic.Suggestion);
    }

    /// <summary>
    ///     Test that an imperative instruction using a verb-only dictionary term in Procedure mode
    ///     is still flagged as a verb usage, even though the sentence otherwise contains no other
    ///     finite verb - the imperative signal must not be silently overridden by the
    ///     verbless-segment noun signal.
    /// </summary>
    [Fact]
    public void Evaluate_ImperativeVerblessSentenceInProcedureMode_StillFlagsVerbUsage()
    {
        // Arrange: "Wash the probe before each run." - imperative instruction, Procedure mode
        var dictionary = VerblessSegmentFixtureDictionary();
        IReadOnlyList<ProseSegment> segments =
            [new ProseSegment("Wash the probe before each run.", 1, SegmentRole.ListItem)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Procedure);

        // Assert: "wash" (imperative verb usage) is flagged
        Assert.Contains(diagnostics, d => d.Suggestion == "clean");
    }

    /// <summary>
    ///     Test that a determiner/finite-verb-follows sentence with a verb-only dictionary term
    ///     used as the subject remains a non-finding regression guard: "pump" is preceded by "The"
    ///     (article) and followed by "moves" (finite verb), both confident noun signals, so it is
    ///     correctly suppressed regardless of the new verbless-segment signal.
    /// </summary>
    [Fact]
    public void Evaluate_SubjectNounWithDeterminerAndFollowingVerb_NotFlagged()
    {
        // Arrange: "The pump moves fluid to the tower." - "pump" is the subject, not a verb usage
        var dictionary = VerblessSegmentFixtureDictionary();
        IReadOnlyList<ProseSegment> segments =
            [new ProseSegment("The pump moves fluid to the tower.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that a disallowed single-word term is suppressed only when the match falls
    ///     entirely inside a configured <c>allow-in-phrase</c> phrase, without suppressing the
    ///     same term elsewhere in the segment.
    /// </summary>
    [Fact]
    public void Evaluate_TermInsideAllowedPhrase_NotFlaggedButSameTermElsewhereStillFlagged()
    {
        // Arrange: "mix" is disallowed; "swish mix" is an approved phrase (the name of a thing),
        // while a bare "mix" elsewhere in the same segment must still be reported
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    ["mix"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Noun, Alternatives = ["combination"] }]
                },
                AllowInPhrase = ["swish mix"]
            }
        };
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments =
            [new ProseSegment("Fill the swish mix tank, then check the fuel mix.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate(
            "file.md", segments, dictionary, LintMode.Descriptive, allowedPhrases: ["swish mix"]);

        // Assert: only the "fuel mix" occurrence is reported, not the "swish mix" occurrence
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("combination", diagnostic.Suggestion);
    }

    /// <summary>
    ///     Test that <c>allow-in-phrase</c> matching is case-insensitive, consistent with every
    ///     other dictionary term comparison in this checker.
    /// </summary>
    [Fact]
    public void Evaluate_TermInsideAllowedPhraseDifferentCasing_StillSuppressesDiagnostic()
    {
        // Arrange: the configured phrase is lower-case, but the prose capitalizes it
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    ["mix"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Noun, Alternatives = ["combination"] }]
                },
                AllowInPhrase = ["swish mix"]
            }
        };
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Fill the Swish Mix tank.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate(
            "file.md", segments, dictionary, LintMode.Descriptive, allowedPhrases: ["swish mix"]);

        // Assert: verify expected behavior
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that an <c>allow-in-phrase</c> entry matches across normal whitespace variation,
    ///     consistent with multi-word <see cref="DictionaryConfig.Disallow"/> term matching.
    /// </summary>
    [Fact]
    public void Evaluate_AllowedPhraseMatchesAcrossWhitespace_SuppressesDiagnostic()
    {
        // Arrange: extra whitespace between the phrase's words
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    ["mix"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Noun, Alternatives = ["combination"] }]
                },
                AllowInPhrase = ["swish mix"]
            }
        };
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Fill the swish  mix tank.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate(
            "file.md", segments, dictionary, LintMode.Descriptive, allowedPhrases: ["swish mix"]);

        // Assert: verify expected behavior
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that supplying an unrelated <c>allowedPhrases</c> entry does not suppress a
    ///     disallowed term that does not occur within any configured phrase.
    /// </summary>
    [Fact]
    public void Evaluate_AllowedPhrasesUnrelatedPhrase_StillFlagsDisallowedTerm()
    {
        // Arrange: "swish mix" is an allowed phrase, but the prose never contains it
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    ["mix"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Noun, Alternatives = ["combination"] }]
                },
                AllowInPhrase = ["swish mix"]
            }
        };
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Check the fuel mix.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate(
            "file.md", segments, dictionary, LintMode.Descriptive, allowedPhrases: ["swish mix"]);

        // Assert: verify expected behavior
        Assert.Single(diagnostics);
    }

    /// <summary>
    ///     Test that omitting <c>allowedPhrases</c> (the default) does not suppress any term, even
    ///     one that happens to appear inside what would otherwise be an allowed phrase - the
    ///     allowance only applies when explicitly resolved and passed in.
    /// </summary>
    [Fact]
    public void Evaluate_NoAllowedPhrasesSupplied_StillFlagsTermInsideWouldBePhrase()
    {
        // Arrange: no allowedPhrases argument supplied
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    ["mix"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Noun, Alternatives = ["combination"] }]
                },
                AllowInPhrase = ["swish mix"]
            }
        };
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Fill the swish mix tank.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested (allowedPhrases omitted)
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Single(diagnostics);
    }

    /// <summary>
    ///     Test that a self-referential entry (its alternatives list includes its own headword,
    ///     ASD-STE100's convention for "approved in the other part of speech") is not flagged when
    ///     the guesser cannot confidently resolve a role but the usage is a noun-noun compound the
    ///     dictionary term modifies.
    /// </summary>
    [Fact]
    public void Evaluate_SelfReferentialEntry_NounCompoundUsage_NotFlagged()
    {
        // Arrange: "check" is verb-only, but self-referential (alternatives include "CHECK" - the
        // noun form is approved). No noun or verb signal fires locally for "check" here (the
        // determiner "the" does not reach past the ordinary noun "system", "status" is excluded
        // from the compound-noun signal because it ends in "s", and "operates" elsewhere is a
        // recognized finite verb so the whole-segment verbless signal does not fire either), so
        // the guesser is genuinely inconclusive (null) - this exercises the new
        // `guess is null && IsSelfReferential(entry)` suppression path directly, rather than the
        // pre-existing "confident guess matches no sense" rule.
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    ["check"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Verb, Alternatives = ["MAKE SURE", "CHECK"] }]
                }
            }
        };
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments =
        [
            new ProseSegment(
                "The system operates continuously. Technical check status remains stable.",
                1,
                SegmentRole.Paragraph)
        ];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that a self-referential entry still reports a diagnostic when the guesser
    ///     confidently resolves the disallowed grammatical role (an imperative verb usage).
    /// </summary>
    [Fact]
    public void Evaluate_SelfReferentialEntry_ConfidentDisallowedUsage_StillFlagged()
    {
        // Arrange: "check" is verb-only and self-referential; an imperative usage is confidently
        // the disallowed verb role, so it must still be reported.
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    ["check"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Verb, Alternatives = ["MAKE SURE", "CHECK"] }]
                }
            }
        };
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Check the supply level.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Procedure);

        // Assert: verify expected behavior
        Assert.Single(diagnostics);
    }

    /// <summary>
    ///     Test that a match immediately followed by a number is treated as a confident verb usage
    ///     and, against a noun-only self-referential entry (disallowing the noun sense only), is
    ///     not flagged - the verb role is the approved grammatical role for this word.
    /// </summary>
    [Fact]
    public void Evaluate_TermFollowedByNumber_ConfidentVerbUsage_NotFlagged()
    {
        // Arrange: "use" is noun-only, self-referential (alternatives include "USE"); followed by
        // a number, it is confidently a verb, which this entry does not disallow.
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    ["use"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Noun, Alternatives = ["OPERATION", "USE"] }]
                }
            }
        };
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments =
            [new ProseSegment("Blocks 1 to 5 use 0.12 ohms of resistance.", 1, SegmentRole.Paragraph)];

        // Act: execute the operation being tested
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary, LintMode.Descriptive);

        // Assert: verify expected behavior
        Assert.Empty(diagnostics);
    }
}
