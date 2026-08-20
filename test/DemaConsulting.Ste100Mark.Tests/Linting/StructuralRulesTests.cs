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
///     Unit tests for the StructuralRules class.
/// </summary>
public class StructuralRulesTests
{
    /// <summary>
    ///     Builds a single paragraph-role segment for a given line of text.
    /// </summary>
    private static IReadOnlyList<ProseSegment> Paragraph(string text) => [new ProseSegment(text, 1, SegmentRole.Paragraph)];

    /// <summary>
    ///     Test that a sentence within the descriptive mode word limit produces no word-limit diagnostic.
    /// </summary>
    [Fact]
    public void Evaluate_SentenceWithinDescriptiveLimit_NoWordLimitDiagnostic()
    {
        // Arrange: a five-word sentence, well under the 25-word descriptive limit
        var segments = Paragraph("This is a short sentence.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-4.1");
    }

    /// <summary>
    ///     Test that a sentence exceeding the descriptive mode word limit (25 words) is flagged.
    /// </summary>
    [Fact]
    public void Evaluate_SentenceExceedingDescriptiveLimit_FlagsWordLimitDiagnostic()
    {
        // Arrange: a 26-word sentence
        var longSentence = string.Join(' ', Enumerable.Repeat("word", 26)) + ".";
        var segments = Paragraph(longSentence);

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        var diagnostic = Assert.Single(diagnostics, d => d.RuleCode == "STE100-4.1");
        Assert.Equal(Severity.Error, diagnostic.Severity);
    }

    /// <summary>
    ///     Test that a sentence within the descriptive limit but exceeding the stricter procedure
    ///     mode limit (20 words) is flagged only in procedure mode.
    /// </summary>
    [Fact]
    public void Evaluate_SentenceExceedingProcedureLimit_FlagsOnlyInProcedureMode()
    {
        // Arrange: a 22-word sentence: over the 20-word procedure limit, under the 25-word descriptive limit
        var sentence = string.Join(' ', Enumerable.Repeat("word", 22)) + ".";
        var segments = Paragraph(sentence);
        var rules = new RulesConfig();

        // Act: execute the operation being tested
        var procedureDiagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Procedure, rules);
        var descriptiveDiagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, rules);

        // Assert: verify expected behavior
        Assert.Contains(procedureDiagnostics, d => d.RuleCode == "STE100-4.1");
        Assert.DoesNotContain(descriptiveDiagnostics, d => d.RuleCode == "STE100-4.1");
    }

    /// <summary>
    ///     Test that a semicolon in prose is flagged by default (Rule 8.1).
    /// </summary>
    [Fact]
    public void Evaluate_Semicolon_FlagsSemicolonDiagnostic()
    {
        // Arrange: a sentence containing a semicolon
        var segments = Paragraph("Open the panel; then close it.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        var diagnostic = Assert.Single(diagnostics, d => d.RuleCode == "STE100-8.1");
        Assert.Equal(Severity.Error, diagnostic.Severity);
    }

    /// <summary>
    ///     Test that AllowSemicolons=true suppresses the semicolon diagnostic.
    /// </summary>
    [Fact]
    public void Evaluate_SemicolonWithAllowSemicolons_NoDiagnostic()
    {
        // Arrange: a sentence containing a semicolon, with the rule disabled
        var segments = Paragraph("Open the panel; then close it.");
        var rules = new RulesConfig { AllowSemicolons = true };

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, rules);

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-8.1");
    }

    /// <summary>
    ///     Test that a diagnostic on a sentence positioned partway through a multi-line paragraph
    ///     reports that sentence's own source line, not the paragraph's first line. Regression test
    ///     for the reported bug where every finding in a multi-line paragraph was reported at the
    ///     paragraph's start line regardless of where the violation actually occurred.
    /// </summary>
    [Fact]
    public void Evaluate_LongSentenceOnLaterLineOfMultiLineParagraph_ReportsThatLine()
    {
        // Arrange: a five-line paragraph (extracted end-to-end, so line numbers are real), where
        // only the fourth line's sentence exceeds the 25-word descriptive limit.
        var longSentence = "Word " + string.Join(' ', Enumerable.Repeat("word", 25)) + ".";
        var markdown = string.Join(
            '\n',
            "First short sentence.",
            "Second short sentence.",
            "Third short sentence.",
            longSentence,
            "Fifth short sentence.");
        var segments = MarkdownProseExtractor.Extract(markdown);

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: the word-limit diagnostic reports line 4 (where the long sentence is), not line 1
        // (the paragraph's start line).
        var diagnostic = Assert.Single(diagnostics, d => d.RuleCode == "STE100-4.1");
        Assert.Equal(4, diagnostic.Line);
    }

    /// <summary>
    ///     Test that a semicolon positioned on a later line of a multi-line paragraph reports that
    ///     line, not the paragraph's start line.
    /// </summary>
    [Fact]
    public void Evaluate_SemicolonOnLaterLineOfMultiLineParagraph_ReportsThatLine()
    {
        // Arrange: a three-line paragraph where only the third line contains a semicolon.
        var markdown = string.Join(
            '\n',
            "First line has no issue.",
            "Second line has no issue.",
            "Third line has an issue; it uses a semicolon.");
        var segments = MarkdownProseExtractor.Extract(markdown);

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: the semicolon diagnostic reports line 3, not line 1
        var diagnostic = Assert.Single(diagnostics, d => d.RuleCode == "STE100-8.1");
        Assert.Equal(3, diagnostic.Line);
    }

    /// <summary>
    ///     Test that a contraction positioned on a later line of a multi-line paragraph reports that
    ///     line, not the paragraph's start line.
    /// </summary>
    [Fact]
    public void Evaluate_ContractionOnLaterLineOfMultiLineParagraph_ReportsThatLine()
    {
        // Arrange: a four-line paragraph where only the fourth line contains a contraction.
        var markdown = string.Join(
            '\n',
            "Line one is fine.",
            "Line two is fine.",
            "Line three is fine.",
            "Line four isn't fine.");
        var segments = MarkdownProseExtractor.Extract(markdown);

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: the contraction diagnostic reports line 4, not line 1
        var diagnostic = Assert.Single(diagnostics, d => d.RuleCode == "STE100-4.2");
        Assert.Equal(4, diagnostic.Line);
    }

    /// <summary>
    ///     Test that the advisory <c>-ing</c> form heuristic on a later line of a multi-line
    ///     paragraph reports that line, not the paragraph's start line.
    /// </summary>
    [Fact]
    public void Evaluate_IngFormOnLaterLineOfMultiLineParagraph_ReportsThatLine()
    {
        // Arrange: a three-line paragraph where only the third line has an -ing word mid-sentence.
        var markdown = string.Join(
            '\n',
            "The first line is short.",
            "The second line is short too.",
            "The unit is monitoring the reading continuously today.");
        var segments = MarkdownProseExtractor.Extract(markdown);

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: at least one -ing-form diagnostic reports line 3, not line 1
        Assert.Contains(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Line == 3);
    }

    /// <summary>
    ///     Test that a contraction is flagged by default (Rule 4.2).
    /// </summary>
    [Fact]
    public void Evaluate_Contraction_FlagsContractionDiagnostic()
    {
        // Arrange: a sentence containing a contraction
        var segments = Paragraph("We don't allow this.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        var diagnostic = Assert.Single(diagnostics, d => d.RuleCode == "STE100-4.2");
        Assert.Contains("don't", diagnostic.Message);
    }

    /// <summary>
    ///     Test that AllowContractions=true suppresses the contraction diagnostic.
    /// </summary>
    [Fact]
    public void Evaluate_ContractionWithAllowContractions_NoDiagnostic()
    {
        // Arrange: a sentence containing a contraction, with the rule disabled
        var segments = Paragraph("We don't allow this.");
        var rules = new RulesConfig { AllowContractions = true };

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, rules);

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-4.2");
    }

    /// <summary>
    ///     Test that a possessive noun ending in <c>'s</c> (e.g. "project's") is not flagged as a
    ///     contraction, since ASD-STE100 Rule 4.2 prohibits contractions ("it's" = "it is"), not
    ///     possessives. This is a regression test for a false-positive found by running the linter
    ///     against the project's own documentation.
    /// </summary>
    [Fact]
    public void Evaluate_PossessiveApostropheS_NotFlaggedAsContraction()
    {
        // Arrange: a sentence containing only a possessive, no true contraction
        var segments = Paragraph("Review the project's design document.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: no contraction diagnostic is raised for the possessive
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-4.2");
    }

    /// <summary>
    ///     Test that a true "it's" contraction is still flagged even alongside a possessive in the
    ///     same sentence, proving the possessive exemption does not over-suppress genuine
    ///     contractions that also use the <c>'s</c> suffix.
    /// </summary>
    [Fact]
    public void Evaluate_ContractionAndPossessiveInSameSentence_FlagsOnlyContraction()
    {
        // Arrange: "It's" is a contraction (it is); "project's" is a possessive
        var segments = Paragraph("It's the project's design document.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: exactly one contraction diagnostic, for "It's" only
        var diagnostic = Assert.Single(diagnostics, d => d.RuleCode == "STE100-4.2");
        Assert.Contains("It's", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a paragraph exceeding the advisory sentence-count cap is flagged at Warn severity.
    /// </summary>

    [Fact]
    public void Evaluate_ParagraphExceedingSentenceCap_FlagsAdvisoryWarning()
    {
        // Arrange: seven one-word sentences in a single paragraph, exceeding the default cap of 6
        var text = string.Concat(Enumerable.Range(1, 7).Select(_ => "Word. "));
        var segments = Paragraph(text.Trim());

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        var diagnostic = Assert.Single(diagnostics, d => d.RuleCode == "STE100-ADV-PARA");
        Assert.Equal(Severity.Warn, diagnostic.Severity);
    }

    /// <summary>
    ///     Test that MaxSentencesParagraph=0 disables the paragraph-length advisory check.
    /// </summary>
    [Fact]
    public void Evaluate_ParagraphLengthDisabled_NoAdvisoryDiagnostic()
    {
        // Arrange: many sentences, but the check is disabled via MaxSentencesParagraph=0
        var text = string.Concat(Enumerable.Range(1, 10).Select(_ => "Word. "));
        var segments = Paragraph(text.Trim());
        var rules = new RulesConfig { MaxSentencesParagraph = 0 };

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, rules);

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-PARA");
    }

    /// <summary>
    ///     Test that a heading segment (not a paragraph) is exempt from the paragraph-length check,
    ///     even when it would otherwise exceed the sentence cap.
    /// </summary>
    [Fact]
    public void Evaluate_HeadingSegment_ExemptFromParagraphLengthCheck()
    {
        // Arrange: a heading-role segment with many short sentences
        var text = string.Concat(Enumerable.Range(1, 10).Select(_ => "Word. "));
        IReadOnlyList<ProseSegment> segments = [new ProseSegment(text.Trim(), 1, SegmentRole.Heading)];

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-PARA");
    }

    /// <summary>
    ///     Test that a sentence matching the passive-voice heuristic is flagged at the configured
    ///     (default Warn) severity when PassiveVoice is not Off.
    /// </summary>
    [Fact]
    public void Evaluate_PassiveVoicePattern_FlagsAdvisoryAtConfiguredSeverity()
    {
        // Arrange: a sentence matching the "was <verb>ed" passive-voice heuristic
        var segments = Paragraph("The report was written by the team.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        var diagnostic = Assert.Single(diagnostics, d => d.RuleCode == "STE100-ADV-PASSIVE");
        Assert.Equal(Severity.Warn, diagnostic.Severity);
    }

    /// <summary>
    ///     Test that PassiveVoice=Off suppresses the passive-voice diagnostic.
    /// </summary>
    [Fact]
    public void Evaluate_PassiveVoiceOff_NoDiagnostic()
    {
        // Arrange: a sentence matching the passive-voice heuristic, with the check disabled
        var segments = Paragraph("The report was written by the team.");
        var rules = new RulesConfig { PassiveVoice = Severity.Off };

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, rules);

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-PASSIVE");
    }

    /// <summary>
    ///     Test that PassiveVoice=Error promotes the passive-voice diagnostic to Error severity.
    /// </summary>
    [Fact]
    public void Evaluate_PassiveVoiceError_FlagsAtErrorSeverity()
    {
        // Arrange: a sentence matching the passive-voice heuristic, configured at Error severity
        var segments = Paragraph("The report was written by the team.");
        var rules = new RulesConfig { PassiveVoice = Severity.Error };

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, rules);

        // Assert: verify expected behavior
        var diagnostic = Assert.Single(diagnostics, d => d.RuleCode == "STE100-ADV-PASSIVE");
        Assert.Equal(Severity.Error, diagnostic.Severity);
    }

    /// <summary>
    ///     Test that a semicolon appearing only inside an inline code span is not flagged, since
    ///     inline code content is excluded from the grammar-sensitive semicolon check (Rule 8.1).
    /// </summary>
    [Fact]
    public void Evaluate_SemicolonOnlyInsideInlineCode_NoDiagnostic()
    {
        // Arrange: a semicolon that appears only inside an inline code span
        var segments = Paragraph("Run the `a;b` command to continue.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-8.1");
    }

    /// <summary>
    ///     Test that a contraction appearing only inside an inline code span is not flagged, since
    ///     inline code content is excluded from the grammar-sensitive contraction check (Rule 4.2).
    /// </summary>
    [Fact]
    public void Evaluate_ContractionOnlyInsideInlineCode_NoDiagnostic()
    {
        // Arrange: a contraction that appears only inside an inline code span
        var segments = Paragraph("Run the `don't-fail` flag to continue.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-4.2");
    }

    /// <summary>
    ///     Test that the passive-voice advisory heuristic does not analyze inline-code content as
    ///     prose grammar, so a "to be + past participle" pattern appearing only inside an inline
    ///     code span produces no diagnostic.
    /// </summary>
    [Fact]
    public void Evaluate_PassiveVoiceOnlyInsideInlineCode_NoDiagnostic()
    {
        // Arrange: the passive-looking phrase appears only inside an inline code span
        var segments = Paragraph("See `was written` for the exact log format.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-PASSIVE");
    }

    /// <summary>
    ///     Test that a word-limit diagnostic's message shows an inline code span verbatim
    ///     (backticks included), rather than a blank gap or placeholder.
    /// </summary>
    [Fact]
    public void Evaluate_WordLimitDiagnosticMessage_ShowsInlineCodeVerbatim()
    {
        // Arrange: one inline code span (counted as one word) followed by 25 plain words => 26
        // words, exceeding the 25-word descriptive limit; the code span is placed first so it
        // survives the diagnostic message's 80-character truncation.
        const string codeSpan = "`flag`";
        var longSentence = codeSpan + " " + string.Join(' ', Enumerable.Repeat("word", 25)) + ".";
        var segments = Paragraph(longSentence);

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: the diagnostic message contains the literal backticked code, not a blank gap
        var diagnostic = Assert.Single(diagnostics, d => d.RuleCode == "STE100-4.1");
        Assert.Contains(codeSpan, diagnostic.Message);
    }

    /// <summary>
    ///     Test that a perfect-tense sentence ("has/have/had verb-ed/en") is flagged by the
    ///     complex-verb advisory heuristic at the configured (default Warn) severity.
    /// </summary>
    [Fact]
    public void Evaluate_PerfectTensePattern_FlagsComplexVerbAdvisory()
    {
        // Arrange: a sentence matching the "has verb-ed" perfect-tense heuristic
        var segments = Paragraph("The technician has opened the panel.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        var diagnostic = Assert.Single(diagnostics, d => d.RuleCode == "STE100-ADV-COMPLEXVERB");
        Assert.Equal(Severity.Warn, diagnostic.Severity);
    }

    /// <summary>
    ///     Test that a modal-perfect-tense sentence ("would have verb-ed") is flagged by the
    ///     complex-verb advisory heuristic.
    /// </summary>
    [Fact]
    public void Evaluate_ModalPerfectTensePattern_FlagsComplexVerbAdvisory()
    {
        // Arrange: a sentence matching the "would have verb-ed" modal-perfect heuristic
        var segments = Paragraph("The team would have written the report.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.Contains(diagnostics, d => d.RuleCode == "STE100-ADV-COMPLEXVERB");
    }

    /// <summary>
    ///     Test that ComplexVerb=Off suppresses the complex-verb diagnostic.
    /// </summary>
    [Fact]
    public void Evaluate_ComplexVerbOff_NoDiagnostic()
    {
        // Arrange: a sentence matching the perfect-tense heuristic, with the check disabled
        var segments = Paragraph("The technician has opened the panel.");
        var rules = new RulesConfig { ComplexVerb = Severity.Off };

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, rules);

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-COMPLEXVERB");
    }

    /// <summary>
    ///     Test that a complex-verb pattern appearing only inside an inline code span is not
    ///     flagged, since inline code content is excluded from the grammar-sensitive check.
    /// </summary>
    [Fact]
    public void Evaluate_ComplexVerbOnlyInsideInlineCode_NoDiagnostic()
    {
        // Arrange: the perfect-tense phrase appears only inside an inline code span
        var segments = Paragraph("See `has opened` for the exact log format.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-COMPLEXVERB");
    }

    /// <summary>
    ///     Test that "has been opened" (perfect-tense passive) is flagged only as a complex-verb
    ///     diagnostic, not also as a passive-voice diagnostic, proving the precedence decision in
    ///     the negative-lookbehind amendment to <c>PassiveVoiceRegex</c> works as intended.
    /// </summary>
    [Fact]
    public void Evaluate_HasBeenOpened_FlagsComplexVerbOnlyNotPassiveVoice()
    {
        // Arrange: perfect-tense passive construction
        var segments = Paragraph("The panel has been opened.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: complex-verb fires, passive-voice does not, for the same sentence
        Assert.Contains(diagnostics, d => d.RuleCode == "STE100-ADV-COMPLEXVERB");
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-PASSIVE");
    }

    /// <summary>
    ///     Test that "was opened" (simple passive, no perfect-tense auxiliary) is still flagged as
    ///     passive-voice, proving the negative-lookbehind amendment did not break existing
    ///     passive-voice detection for non-perfect-tense constructions.
    /// </summary>
    [Fact]
    public void Evaluate_WasOpened_StillFlagsPassiveVoice()
    {
        // Arrange: simple past passive construction
        var segments = Paragraph("The panel was opened.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: passive-voice fires, complex-verb does not
        Assert.Contains(diagnostics, d => d.RuleCode == "STE100-ADV-PASSIVE");
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-COMPLEXVERB");
    }

    /// <summary>
    ///     Test that an <c>-ing</c> word appearing mid-sentence is flagged by the ing-form advisory
    ///     heuristic at the configured (default Warn) severity.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordMidSentence_FlagsIngFormAdvisory()
    {
        // Arrange: a sentence containing an -ing word not touching a sentence-ending period
        var segments = Paragraph("The technician is checking the panel before closing it fully.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        var diagnostic = Assert.Single(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("checking"));
        Assert.Equal(Severity.Warn, diagnostic.Severity);
    }

    /// <summary>
    ///     Test that an <c>-ing</c> word immediately followed by a sentence-ending period is
    ///     skipped by the ing-form heuristic.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordFollowedByPeriod_NotFlagged()
    {
        // Arrange: the -ing word is the last word of the sentence, touching the period
        var segments = Paragraph("Continue reading.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: no ing-form diagnostic for the word touching the period
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM");
    }

    /// <summary>
    ///     Test that an <c>-ing</c> word immediately preceded by a sentence-ending period (the
    ///     first word of a new sentence) is skipped by the ing-form heuristic.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordPrecededByPeriod_NotFlagged()
    {
        // Arrange: the -ing word directly follows a period with no space stripped by the analyzer
        var segments = Paragraph("Stop now.Reading continues after this.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: "Reading" touches the preceding period and is not flagged
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("Reading"));
    }

    /// <summary>
    ///     Test that IngForm=Off suppresses the ing-form diagnostic.
    /// </summary>
    [Fact]
    public void Evaluate_IngFormOff_NoDiagnostic()
    {
        // Arrange: a sentence containing an -ing word, with the check disabled
        var segments = Paragraph("The technician is checking the panel before closing it fully.");
        var rules = new RulesConfig { IngForm = Severity.Off };

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, rules);

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM");
    }

    /// <summary>
    ///     Test that an <c>-ing</c> word appearing only inside an inline code span is not flagged,
    ///     since inline code content is excluded from the grammar-sensitive check.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordOnlyInsideInlineCode_NoDiagnostic()
    {
        // Arrange: the -ing word appears only inside an inline code span
        var segments = Paragraph("Run the `checking` command before closing the tool fully.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: "checking" (inside code span) is not flagged, but "closing" is
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("checking"));
        Assert.Contains(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("closing"));
    }
}
