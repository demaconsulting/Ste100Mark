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
    ///     Test that an <c>-ing</c> word immediately following a catenative verb (a gerund
    ///     complement, e.g. "continue reading") is treated as a noun by
    ///     <see cref="PartOfSpeechGuesser.GuessIngFormRole"/> and skipped by the ing-form
    ///     heuristic, rather than as a verb.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordFollowedByCatenativeVerb_NotFlagged()
    {
        // Arrange: "reading" is the gerund complement of the catenative verb "continue"
        var segments = Paragraph("Continue reading.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: no ing-form diagnostic for the gerund complement
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM");
    }

    /// <summary>
    ///     Test that an <c>-ing</c> word that is the first word of a new sentence (a
    ///     sentence-initial subject gerund) is treated as a noun by
    ///     <see cref="PartOfSpeechGuesser.GuessIngFormRole"/> and skipped by the ing-form
    ///     heuristic, even when no space separates it from the preceding sentence's period.
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
        // Arrange: the -ing word appears only inside an inline code span. "while" (not a
        // preposition) precedes "closing" so the genuine verbal gerund-with-object reading is
        // exercised without also tripping the preposition-object gate added for
        // GuessIngFormRole's "before testing the gauge" fix (see
        // Evaluate_IngWordAsObjectOfPrepositionWithDirectObject_NotFlagged).
        var segments = Paragraph("Run the `checking` command while closing the tool fully.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: "checking" (inside code span) is not flagged, but "closing" is
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("checking"));
        Assert.Contains(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("closing"));
    }

    /// <summary>
    ///     Test that a word ending in "-ing" that is never a present-participle verb form (a
    ///     preposition, in this case) is excluded from the ing-form heuristic unconditionally.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordInExclusionList_NotFlagged()
    {
        // Arrange: "during" ends in "-ing" but is a preposition, not a verb form; "is reading" is a
        // genuine present-participle verb use, distinct from the noun use of "reading" elsewhere
        var segments = Paragraph("The operator is reading the gauge during the test.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: "during" is not flagged, but "reading" still is
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("during"));
        Assert.Contains(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("reading"));
    }

    /// <summary>
    ///     Test that an <c>-ing</c> word approved via the file's resolved dictionary allow list is
    ///     excluded from the ing-form heuristic, so approving a term also suppresses it from this
    ///     advisory, not only <see cref="DictionaryChecker"/>.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordOnAllowedTermsList_NotFlagged()
    {
        // Arrange: "metering" is approved via the file's resolved allow list. "while" (not a
        // preposition) precedes "closing" so the genuine verbal gerund-with-object reading is
        // exercised without also tripping the preposition-object gate added for
        // GuessIngFormRole's "before testing the gauge" fix (see
        // Evaluate_IngWordAsObjectOfPrepositionWithDirectObject_NotFlagged).
        var segments = Paragraph("Check the metering system while closing the panel.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate(
            "file.md", segments, LintMode.Descriptive, new RulesConfig(), ["metering"]);

        // Assert: "metering" is not flagged, but "closing" still is
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("metering"));
        Assert.Contains(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("closing"));
    }

    /// <summary>
    ///     Test that a genuine present-participle verb use of a word that could otherwise be
    ///     mistaken for an always-non-verb "-ing" word is still flagged. Regression test for a
    ///     reported bug where "evening" and "ceiling" were unconditionally excluded even though
    ///     both have a genuine, if uncommon, verb use in technical writing ("evening out the
    ///     load", "ceiling the price").
    /// </summary>
    [Fact]
    public void Evaluate_IngWordWithGenuineVerbUse_StillFlagged()
    {
        // Arrange: "evening" is used here as a genuine present-participle verb, not the "morning/
        // evening" time-of-day noun.
        var segments = Paragraph("The technician is evening out the load across both circuits.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: the genuine verb use of "evening" is still flagged
        Assert.Contains(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("evening"));
    }

    /// <summary>
    ///     Test that "string" - a base-form noun/verb whose spelling happens to end in the letters
    ///     "ing" but which is never a present-participle verb form (there is no verb "str") - is
    ///     excluded from the ing-form heuristic, matching the existing "king"/"ring"/"thing"/
    ///     "spring" exclusions. Regression test for a reported bug where "string" was missing from
    ///     the exclusion list.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordStringInExclusionList_NotFlagged()
    {
        // Arrange: "string" ends in "-ing" but is a base-form noun/verb, not a verb form. "while"
        // (not a preposition) precedes "closing" so the genuine verbal gerund-with-object reading
        // is exercised without also tripping the preposition-object gate added for
        // GuessIngFormRole's "before testing the gauge" fix (see
        // Evaluate_IngWordAsObjectOfPrepositionWithDirectObject_NotFlagged).
        var segments = Paragraph("String the cable while closing the panel.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: "string" is not flagged, but "closing" still is
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("string", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("closing"));
    }

    /// <summary>
    ///     Test that an <c>-ing</c> word used as a sentence-initial subject noun (a gerund process
    ///     noun) is not flagged. Category E regression test.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordAsSentenceInitialSubject_NotFlagged()
    {
        // Arrange: "Testing" is the subject noun of the sentence, not a present-participle verb
        var segments = Paragraph("Testing confirms the seal integrity.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("Testing"));
    }

    /// <summary>
    ///     Test that an <c>-ing</c> word used as a sentence-initial subject noun is still not
    ///     flagged even when it is itself immediately followed by a direct object (e.g. "the
    ///     flow"). Regression test for a reported false positive where the transitive-object
    ///     follow-on signal was checked before the sentence-initial noun signal, wrongly
    ///     classifying a gerund subject as a verb merely because the next word was an article.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordAsSentenceInitialSubjectWithDirectObject_NotFlagged()
    {
        // Arrange: "Metering" is the sentence-initial subject noun, even though "the flow"
        // immediately follows it like a transitive verb's direct object would
        var segments = Paragraph("Metering the flow is required.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("Metering"));
    }

    /// <summary>
    ///     Test that an <c>-ing</c> word immediately following a catenative verb (a gerund
    ///     complement) is still not flagged even when it is itself immediately followed by a
    ///     direct object (e.g. "the gauge"). Regression test for a reported false positive where
    ///     the transitive-object follow-on signal was checked before the catenative-complement
    ///     noun signal, wrongly classifying a catenative gerund complement as a verb merely
    ///     because the next word was an article.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordAsCatenativeComplementWithDirectObject_NotFlagged()
    {
        // Arrange: "monitoring" is the gerund complement of the catenative verb "continue", even
        // though "the gauge" immediately follows it like a transitive verb's direct object would
        var segments = Paragraph("Continue monitoring the gauge.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("monitoring"));
    }

    /// <summary>
    ///     Test that an <c>-ing</c> word used as the object of a preposition, with no direct object
    ///     of its own, is not flagged (a nominal gerund, not a verbal one). Category E regression
    ///     test.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordAsObjectOfPreposition_NotFlagged()
    {
        // Arrange: "testing" is the object of "before", with no direct object of its own
        var segments = Paragraph("Check the gauge reading before testing.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("testing"));
    }

    /// <summary>
    ///     Test that an <c>-ing</c> word used as the object of a preposition is not flagged even
    ///     when it is itself immediately followed by its own direct object. Regression test for a
    ///     reported false positive where the transitive-object follow-on shortcut was gated only
    ///     for sentence starts and catenative complements, not for a preceding preposition, so a
    ///     gerund object of a preposition that itself took a direct object (e.g. "before
    ///     <c>testing</c> the gauge") fell through to that shortcut and was wrongly classified as a
    ///     verb.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordAsObjectOfPrepositionWithDirectObject_NotFlagged()
    {
        // Arrange: "testing" is the object of the preposition "before", but unlike the previous
        // preposition-object test, it is also immediately followed by its own direct object ("the
        // gauge"), which is the scenario that previously reached the transitive-object shortcut
        var segments = Paragraph("Confirm the valve is closed before testing the gauge for leaks.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("testing"));
    }

    /// <summary>
    ///     Test that a material noun ending in "-ing" (preceded by an article) is not flagged.
    ///     Category E regression test.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordAsMaterialNoun_NotFlagged()
    {
        // Arrange: "tubing" is a material noun, not a present-participle verb form
        var segments = Paragraph("Route the tubing away from the heat source.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("tubing"));
    }

    /// <summary>
    ///     Test that a participial adjective directly before a noun (modifying it) is not flagged.
    ///     Category E regression test.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordAsParticipialAdjectiveBeforeNoun_NotFlagged()
    {
        // Arrange: "moving" modifies the following noun "structure" as an adjective
        var segments = Paragraph("Keep hands clear of the moving structure.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("moving"));
    }

    /// <summary>
    ///     Test that an <c>-ing</c> word in a Markdown heading segment is not flagged, since
    ///     headings are short labels, not prose sentences. Category E/B regression test.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordInHeading_NotFlagged()
    {
        // Arrange: a heading-role segment whose text would otherwise resolve as a verb use
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Closing the Maintenance Log", 1, SegmentRole.Heading)];

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM");
    }

    /// <summary>
    ///     Test that an <c>-ing</c> word in a table header segment is not flagged, even when it
    ///     would otherwise resolve as a genuine verb use (followed by an article). Category B
    ///     regression test.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordInTableHeader_NotFlagged()
    {
        // Arrange: a table-header-role segment cell that would otherwise resolve as a verb use
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("Closing the Valve", 1, SegmentRole.TableHeader)];

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM");
    }

    /// <summary>
    ///     Test that an <c>-ing</c> word inside a quoted/cited document title is not flagged, even
    ///     though it would otherwise resolve as a genuine verb use (followed by a direct-object
    ///     pronoun). Category E regression test for cited document titles.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordInQuotedCitedTitle_NotFlagged()
    {
        // Arrange: "Closing it Quickly" is a quoted document title; "Closing" followed by the
        // object pronoun "it" would otherwise resolve as a genuine verb use
        var segments = Paragraph("The guide \"Closing it Quickly\" explains the steps.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("Closing"));
    }

    /// <summary>
    ///     Test that an <c>-ing</c> word inside an admonition label at the start of a block (for
    ///     example <c>**Caution.**</c>) is not flagged. Category B regression test.
    /// </summary>
    [Fact]
    public void Evaluate_IngWordInsideAdmonitionLabel_NotFlagged()
    {
        // Arrange: "Warning" as a bold admonition label at the start of the block
        var segments = Paragraph("**Warning.** Keep hands clear of the moving structure.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: "Warning" (the label) is not flagged, "moving" is unaffected by the label skip
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-INGFORM" && d.Message.Contains("Warning"));
    }

    /// <summary>
    ///     Test that a common stative/adjectival participle following "is"/"are" is not flagged as
    ///     passive voice - a predicate adjective describing a current state, not a passive
    ///     construction naming an action done by an implied agent. Category G regression test.
    /// </summary>
    [Fact]
    public void Evaluate_PassiveVoiceStativeParticiple_NotFlagged()
    {
        // Arrange: "Keep X while it is energized." - "energized" is a stative/adjectival participle
        var segments = Paragraph("Keep the cover closed while the circuit is energized.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-PASSIVE");
    }

    /// <summary>
    ///     Test that an "is/are + participle" construction in the complement clause of an imperative
    ///     main clause is not flagged as passive voice, even for a participle not in the common
    ///     stative-participle set, since the imperative instruction reads as a state to verify.
    ///     Category G regression test.
    /// </summary>
    [Fact]
    public void Evaluate_PassiveVoiceImperativeMainClauseComplement_NotFlagged()
    {
        // Arrange: "Confirm Y is seated." - an imperative lead verb governing a stative complement
        var segments = Paragraph("Confirm the gasket is seated before closing the cover.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-PASSIVE");
    }

    /// <summary>
    ///     Test that "is unobstructed" - a pure adjectival participle reading with no implied agent
    ///     - is never flagged as passive voice. Category G regression test.
    /// </summary>
    [Fact]
    public void Evaluate_PassiveVoiceIsUnobstructed_NeverFlagged()
    {
        // Arrange: "is unobstructed" - a predicate adjective, not a passive construction
        var segments = Paragraph("Confirm the exhaust path is unobstructed before starting the engine.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-PASSIVE");
    }

    /// <summary>
    ///     Test that an explicit "by &lt;agent&gt;" phrase overrides both the stative-participle and
    ///     imperative-main-clause exemptions, so a genuinely passive construction with a named agent
    ///     is still flagged even inside an imperative sentence. Recall-regression test for Category
    ///     G, ensuring the fix does not silently suppress genuine passive voice.
    /// </summary>
    [Fact]
    public void Evaluate_PassiveVoiceImperativeWithExplicitAgent_StillFlagged()
    {
        // Arrange: "is closed by the latch" names an explicit agent, so this is still passive
        var segments = Paragraph("Keep the panel closed while it is secured by the latch.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.Contains(diagnostics, d => d.RuleCode == "STE100-ADV-PASSIVE");
    }

    /// <summary>
    ///     Test that a genuine declarative passive-voice sentence (not an imperative instruction,
    ///     and not a common stative participle) is still flagged. Recall-regression test for
    ///     Category G, ensuring the imperative-main-clause exemption is scoped to imperative
    ///     sentences only.
    /// </summary>
    [Fact]
    public void Evaluate_PassiveVoiceDeclarativeSentence_StillFlagged()
    {
        // Arrange: a plain declarative sentence, not an imperative instruction
        var segments = Paragraph("The report is reviewed every week by the supervisor.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.Contains(diagnostics, d => d.RuleCode == "STE100-ADV-PASSIVE");
    }

    /// <summary>
    ///     Test that a sentence-initial "Do" is not treated as an imperative lead when it opens a
    ///     yes/no question, so the question's genuine "are inspected" passive construction is still
    ///     flagged rather than being wrongly exempted. Regression test for a reported bug where
    ///     every sentence-initial "do" was treated as imperative, even though "do" is also the
    ///     auxiliary that starts a yes/no question.
    /// </summary>
    [Fact]
    public void Evaluate_PassiveVoiceDoQuestionLead_StillFlagged()
    {
        // Arrange: "Do the covers...?" is a question, not an imperative instruction
        var segments = Paragraph("Do the covers remain closed while they are inspected?");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.Contains(diagnostics, d => d.RuleCode == "STE100-ADV-PASSIVE");
    }

    /// <summary>
    ///     Test that a sentence-initial "Do not" is still treated as an imperative lead (consistent
    ///     with other imperative leads such as "Keep"/"Confirm"), so its stative "is energized"
    ///     complement remains exempted rather than being flagged as passive voice.
    /// </summary>
    [Fact]
    public void Evaluate_PassiveVoiceDoNotImperativeLead_NotFlagged()
    {
        // Arrange: "Do not open..." is a genuine negative-imperative instruction; "checked" is
        // deliberately not in StativeParticiples, so only the imperative-lead exemption (not the
        // stative-participle exemption) can explain this remaining unflagged
        var segments = Paragraph("Do not open the cover while it is checked for damage.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-PASSIVE");
    }

    /// <summary>
    ///     Test that the imperative-lead passive-voice exemption is scoped to the clause the
    ///     imperative actually governs, not the whole sentence, so a later independent clause
    ///     introduced by a coordinating conjunction after a comma still gets a genuine passive-voice
    ///     advisory. Regression test for a reported bug where the exemption was applied sentence-
    ///     wide, suppressing the finding for the independent second clause.
    /// </summary>
    [Fact]
    public void Evaluate_PassiveVoiceImperativeLeadWithCoordinatedIndependentClause_StillFlagged()
    {
        // Arrange: "Keep X closed, but Y is checked weekly." - the "but"-clause is independent of
        // the imperative lead and names no stative participle, so it is a genuine passive
        var segments = Paragraph("Keep the valve closed, but the gauge is checked weekly.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.Contains(diagnostics, d => d.RuleCode == "STE100-ADV-PASSIVE");
    }

    /// <summary>
    ///     Test that the imperative-lead passive-voice exemption is scoped to the clause the
    ///     imperative actually governs even when the two clauses are joined by a bare semicolon
    ///     (not a comma plus coordinating conjunction), so a genuine passive construction in the
    ///     second independent clause still gets flagged. Regression test for a reported bug where
    ///     <see cref="StructuralRules"/>'s clause-boundary regex recognized only the comma plus
    ///     coordinating-conjunction boundary, not a semicolon, even though
    ///     <see cref="SentenceAnalyzer"/> keeps semicolon-separated clauses within a single
    ///     sentence - so the exemption wrongly extended across the semicolon and suppressed the
    ///     second clause's genuine passive construction as if it were still governed by the leading
    ///     imperative.
    /// </summary>
    [Fact]
    public void Evaluate_PassiveVoiceImperativeLeadWithSemicolonIndependentClause_StillFlagged()
    {
        // Arrange: "Keep X closed; Y is checked weekly." - the clause after the semicolon is
        // independent of the imperative lead and names no stative participle, so it is a genuine
        // passive construction that must not be exempted by the leading imperative
        var segments = Paragraph("Keep the cover closed; the gauge is checked weekly.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.Contains(diagnostics, d => d.RuleCode == "STE100-ADV-PASSIVE");
    }

    /// <summary>
    ///     Test that a subordinate clause introduced by "while" (not a coordinating conjunction)
    ///     remains part of the imperative-governed clause and keeps its passive-voice exemption,
    ///     confirming the clause-boundary fix does not regress the existing "while"-clause
    ///     behavior.
    /// </summary>
    [Fact]
    public void Evaluate_PassiveVoiceImperativeLeadWithSubordinateWhileClause_NotFlagged()
    {
        // Arrange: "Keep X closed while it is monitored." - "while" is subordinating, not
        // coordinating, so the whole sentence remains the imperative-governed clause
        var segments = Paragraph("Keep the valve closed while it is monitored.");

        // Act: execute the operation being tested
        var diagnostics = StructuralRules.Evaluate("file.md", segments, LintMode.Descriptive, new RulesConfig());

        // Assert: verify expected behavior
        Assert.DoesNotContain(diagnostics, d => d.RuleCode == "STE100-ADV-PASSIVE");
    }
}
