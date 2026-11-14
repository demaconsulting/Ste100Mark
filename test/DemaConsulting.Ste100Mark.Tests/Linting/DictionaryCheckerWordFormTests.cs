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
///     Unit tests for inflected word-form matching in the DictionaryChecker class.
/// </summary>
public class DictionaryCheckerWordFormTests
{
    /// <summary>
    ///     Builds a dictionary with only the given inline entries (no embedded dictionary).
    /// </summary>
    private static LintDictionary Inline(
        Dictionary<string, List<DictionarySenseYaml>> disallow,
        bool matchWordForms = true,
        List<string>? allow = null)
    {
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                UseEmbedded = false,
                Disallow = disallow,
                Allow = allow,
                MatchWordForms = matchWordForms
            }
        };
        return LintDictionary.Load(config, Directory.GetCurrentDirectory());
    }

    /// <summary>
    ///     Builds one inline entry with a single sense.
    /// </summary>
    private static Dictionary<string, List<DictionarySenseYaml>> Entry(string term, PartOfSpeech pos, params string[] alternatives)
    {
        return new Dictionary<string, List<DictionarySenseYaml>>
        {
            [term] = [new DictionarySenseYaml { Pos = pos, Alternatives = [.. alternatives] }]
        };
    }

    /// <summary>
    ///     Evaluates one paragraph with a fixed resolver.
    /// </summary>
    private static IReadOnlyList<Diagnostic> Run(
        string text,
        LintDictionary dictionary,
        PartOfSpeech? role,
        IReadOnlyCollection<string>? extraAllowed = null,
        IReadOnlyCollection<string>? phrases = null)
    {
        IReadOnlyList<ProseSegment> segments = [new ProseSegment(text, 1, SegmentRole.Paragraph)];
        return DictionaryChecker.Evaluate(
            "file.md",
            segments,
            dictionary,
            extraAllowed,
            phrases,
            new FixedPartOfSpeechResolver(role));
    }

    /// <summary>
    ///     Test that every inflected form of a verb entry is flagged and its alternative inflected.
    /// </summary>
    [Theory]
    [InlineData("performs", "starts")]
    [InlineData("performed", "started")]
    [InlineData("performing", "starting")]
    public void Evaluate_VerbWordForm_FlaggedWithInflectedAlternative(string form, string expected)
    {
        // Arrange: an inline verb entry
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "start"));

        // Act: evaluate prose using the inflected form
        var diagnostics = Run($"The crew {form} the task.", dictionary, PartOfSpeech.Verb);

        // Assert: one finding naming the form and the inflected alternative
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal($"Avoid '{form}' (a form of 'perform'); use '{expected}' instead.", diagnostic.Message);
        Assert.Equal(expected, diagnostic.Suggestion);
    }

    /// <summary>
    ///     Test that inflected forms of the embedded "produce" entry are flagged.
    /// </summary>
    [Theory]
    [InlineData("produces")]
    [InlineData("produced")]
    [InlineData("producing")]
    public void Evaluate_EmbeddedProduceForms_Flagged(string form)
    {
        // Arrange: the embedded dictionary
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());

        // Act: evaluate prose using the inflected form as a verb
        var diagnostics = Run($"The pump {form} heat.", dictionary, PartOfSpeech.Verb);

        // Assert: flagged as a form of the entry
        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("(a form of 'produce')", diagnostic.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a noun-and-verb entry's forms follow the resolved role.
    /// </summary>
    [Fact]
    public void Evaluate_EmbeddedPermitForms_FollowResolvedRole()
    {
        // Arrange: the embedded "permit" entry has a noun sense and a verb sense
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());

        // Act: "permits" as a noun, as a verb, and with an inconclusive role
        var asNoun = Assert.Single(Run("The crew holds two permits.", dictionary, PartOfSpeech.Noun));
        var asVerb = Assert.Single(Run("The rule permits entry.", dictionary, PartOfSpeech.Verb));
        var unknown = Assert.Single(Run("The rule permits entry.", dictionary, null));
        var past = Assert.Single(Run("The rule permitted entry.", dictionary, PartOfSpeech.Verb));

        // Assert: role-specific alternatives, an ambiguous message when unknown
        Assert.Contains("licence" + "s", asNoun.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("'allows'", asNoun.Message, StringComparison.Ordinal);
        Assert.Contains("'allows'", asVerb.Message, StringComparison.Ordinal);
        Assert.StartsWith("Ambiguous part of speech for 'permits'", unknown.Message, StringComparison.Ordinal);
        Assert.Contains("(a form of 'permit')", past.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a base-form message is unchanged by word-form matching.
    /// </summary>
    [Fact]
    public void Evaluate_BaseForm_MessageUnchanged()
    {
        // Arrange: the embedded "utilize" entry
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());

        // Act: evaluate the base form
        var diagnostic = Assert.Single(Run("Please utilize the tool.", dictionary, PartOfSpeech.Verb));

        // Assert: the original wording, with no word-form note
        Assert.Equal("Avoid 'utilize'; use 'use' instead.", diagnostic.Message);
    }

    /// <summary>
    ///     Test that the inflected form of an embedded verb is flagged in a normal sentence with
    ///     the production resolver.
    /// </summary>
    [Fact]
    public void Evaluate_InflectedFormOfEmbeddedTerm_FlaggedWithProductionResolver()
    {
        // Arrange: the embedded "utilize" entry
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("The crew utilized the tool yesterday.", 1, SegmentRole.Paragraph)];

        // Act: evaluate with the default resolver
        var diagnostics = DictionaryChecker.Evaluate("file.md", segments, dictionary);

        // Assert: flagged with the inflected alternative
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Avoid 'utilized' (a form of 'utilize'); use 'used' instead.", diagnostic.Message);
    }

    /// <summary>
    ///     Test that an allowed inflected spelling is not flagged while its siblings still are.
    /// </summary>
    [Fact]
    public void Evaluate_AllowedInflectedSpelling_WinsOverGeneratedForm()
    {
        // Arrange: allow only "performed"
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "start"), allow: ["performed"]);

        // Act: evaluate the allowed and a non-allowed form
        var allowed = Run("The crew performed the task.", dictionary, PartOfSpeech.Verb);
        var other = Run("The crew performs the task.", dictionary, PartOfSpeech.Verb);

        // Assert: only the non-allowed form is flagged
        Assert.Empty(allowed);
        Assert.Single(other);
    }

    /// <summary>
    ///     Test that a per-file allowance of an inflected spelling suppresses only that spelling.
    /// </summary>
    [Fact]
    public void Evaluate_ExtraAllowedInflectedSpelling_WinsOverGeneratedForm()
    {
        // Arrange: a per-file allowance (as from a profile or a glossary variant)
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "start"));

        // Act: evaluate with the extra allowance
        var allowed = Run("The crew performs the task.", dictionary, PartOfSpeech.Verb, ["performs"]);
        var other = Run("The crew performed the task.", dictionary, PartOfSpeech.Verb, ["performs"]);

        // Assert: the allowed spelling is suppressed, the other form is flagged
        Assert.Empty(allowed);
        Assert.Single(other);
    }

    /// <summary>
    ///     Test that allowing the base term suppresses all of its generated forms.
    /// </summary>
    [Fact]
    public void Evaluate_AllowedBaseTerm_SuppressesAllForms()
    {
        // Arrange: allow the base term
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "start"), allow: ["perform"]);

        // Act: evaluate inflected forms
        var diagnostics = Run("The crew performed and performs and is performing.", dictionary, PartOfSpeech.Verb);

        // Assert: nothing is flagged
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that an allowed phrase suppresses an inflected match inside it.
    /// </summary>
    [Fact]
    public void Evaluate_InflectedFormInsideAllowedPhrase_NotFlagged()
    {
        // Arrange: an allowed phrase containing an inflected form
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "start"));

        // Act: evaluate the phrase and the form outside it
        var inside = Run("The performed work is listed.", dictionary, PartOfSpeech.Verb, null, ["performed work"]);
        var outside = Run("The crew performed the task.", dictionary, PartOfSpeech.Verb, null, ["performed work"]);

        // Assert: only the occurrence outside the phrase is flagged
        Assert.Empty(inside);
        Assert.Single(outside);
    }

    /// <summary>
    ///     Test that an explicit dictionary key for an inflected spelling wins over the generated form.
    /// </summary>
    [Fact]
    public void Evaluate_ExplicitInflectedKey_WinsOverGeneratedForm()
    {
        // Arrange: both "perform" and an explicit "performed" entry
        var disallow = Entry("perform", PartOfSpeech.Verb, "start");
        disallow["performed"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Verb, Alternatives = ["finished"] }];
        var dictionary = Inline(disallow);

        // Act: evaluate the explicit spelling
        var diagnostic = Assert.Single(Run("The crew performed the task.", dictionary, PartOfSpeech.Verb));

        // Assert: the explicit entry's wording is used, with no word-form note
        Assert.Equal("Avoid 'performed'; use 'finished' instead.", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a noun entry's plural is flagged for a noun and not for a verb.
    /// </summary>
    [Fact]
    public void Evaluate_NounPlural_GatedByResolvedRole()
    {
        // Arrange: a noun-only entry
        var dictionary = Inline(Entry("gadget", PartOfSpeech.Noun, "tool"));

        // Act: evaluate the plural under both roles
        var asNoun = Run("The crew carries gadgets.", dictionary, PartOfSpeech.Noun);
        var asVerb = Run("The crew carries gadgets.", dictionary, PartOfSpeech.Verb);

        // Assert: flagged only as a noun
        var diagnostic = Assert.Single(asNoun);
        Assert.Equal("Avoid 'gadgets' (a form of 'gadget'); use 'tools' instead.", diagnostic.Message);
        Assert.Empty(asVerb);
    }

    /// <summary>
    ///     Test that a verb entry's forms are not flagged where the word is used as a noun.
    /// </summary>
    [Fact]
    public void Evaluate_VerbForm_NotFlaggedWhenResolvedAsNoun()
    {
        // Arrange: a verb-only entry
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "start"));

        // Act: evaluate a form resolved as a noun
        var diagnostics = Run("The performs are listed.", dictionary, PartOfSpeech.Noun);

        // Assert: not flagged
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that only the head verb of a phrasal verb inflects.
    /// </summary>
    [Fact]
    public void Evaluate_PhrasalVerbForms_HeadVerbInflects()
    {
        // Arrange: a phrasal-verb entry
        var dictionary = Inline(Entry("carry out", PartOfSpeech.Verb, "do"));

        // Act: evaluate the forms, one across a line break, and the head alone
        var carried = Run("The crew carried out the task.", dictionary, PartOfSpeech.Verb);
        var carries = Run("The crew carries\nout the task.", dictionary, PartOfSpeech.Verb);
        var carrying = Run("The crew is carrying out the task.", dictionary, PartOfSpeech.Verb);
        var headOnly = Run("The crew carried the task.", dictionary, PartOfSpeech.Verb);

        // Assert: phrase forms flagged, the head alone not
        Assert.Single(carried);
        Assert.Single(carries);
        Assert.Single(carrying);
        Assert.Empty(headOnly);
    }

    /// <summary>
    ///     Test that an alternative that cannot be inflected safely falls back to its base form
    ///     and the message says so, while citations keep the base alternatives.
    /// </summary>
    [Fact]
    public void Evaluate_IrregularAlternative_FallsBackToBaseAndSaysSo()
    {
        // Arrange: "give" has different past and past-participle spellings
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "give"));

        // Act: evaluate the form that is both past and past participle
        var diagnostic = Assert.Single(Run("The crew performed the task.", dictionary, PartOfSpeech.Verb));

        // Assert: base alternative shown with an explanation, citation is the base term
        Assert.Contains("use 'give' instead", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("shown in its base form", diagnostic.Message, StringComparison.Ordinal);
        var citation = Assert.Single(diagnostic.Citations!);
        Assert.Equal("give", citation.Term);
    }

    /// <summary>
    ///     Test that an irregular alternative inflects when the form is unambiguous.
    /// </summary>
    [Fact]
    public void Evaluate_IrregularAlternative_InflectsForUnambiguousForm()
    {
        // Arrange: "take" has an irregular -ing and third-person form only
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "take"));

        // Act: evaluate the -ing form
        var diagnostic = Assert.Single(Run("The crew is performing the task.", dictionary, PartOfSpeech.Verb));

        // Assert: inflected alternative, no fallback sentence
        Assert.Equal("Avoid 'performing' (a form of 'perform'); use 'taking' instead.", diagnostic.Message);
    }

    /// <summary>
    ///     Test that a participial form is not flagged in a noun or adjective role.
    /// </summary>
    [Theory]
    [InlineData("Adjective")]
    [InlineData("Noun")]
    [InlineData("Adverb")]
    public void Evaluate_ParticipleInNonVerbRole_NotFlagged(string roleName)
    {
        var role = Enum.Parse<PartOfSpeech>(roleName);
        // Arrange: a verb entry
        var dictionary = Inline(Entry("produce", PartOfSpeech.Verb, "make"));

        // Act: evaluate participial forms in a confident non-verb role
        var past = Run("The produced item is ready.", dictionary, role);
        var gerund = Run("The producing unit is ready.", dictionary, role);

        // Assert: nothing flagged
        Assert.Empty(past);
        Assert.Empty(gerund);
    }

    /// <summary>
    ///     Test that regular and irregular participial forms are flagged for a confident verb.
    /// </summary>
    [Theory]
    [InlineData("produced", "made")]
    [InlineData("producing", "making")]
    public void Evaluate_ParticipleResolvedAsVerb_Flagged(string form, string expected)
    {
        // Arrange: a verb entry
        var dictionary = Inline(Entry("produce", PartOfSpeech.Verb, "make"));

        // Act: evaluate with a confident verb
        var diagnostic = Assert.Single(Run($"The crew {form} the part.", dictionary, PartOfSpeech.Verb));

        // Assert: flagged with the inflected alternative
        Assert.Contains($"use '{expected}' instead", diagnostic.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that irregular past and past-participle forms need a confident verb.
    /// </summary>
    [Theory]
    [InlineData("took")]
    [InlineData("taken")]
    [InlineData("taking")]
    public void Evaluate_IrregularParticiple_FlaggedOnlyAsVerb(string form)
    {
        // Arrange: an irregular verb entry
        var dictionary = Inline(Entry("take", PartOfSpeech.Verb, "pick"));

        // Act: evaluate as verb, with an inconclusive role, and as an adjective
        var asVerb = Run($"The crew {form} the part.", dictionary, PartOfSpeech.Verb);
        var unknown = Run($"The crew {form} the part.", dictionary, null);
        var asAdjective = Run($"The {form} part is ready.", dictionary, PartOfSpeech.Adjective);

        // Assert: flagged only for the confident verb
        Assert.Single(asVerb);
        Assert.Empty(unknown);
        Assert.Empty(asAdjective);
    }

    /// <summary>
    ///     Test that regular -ed and -ing forms are not flagged when the role is inconclusive.
    /// </summary>
    [Theory]
    [InlineData("produced")]
    [InlineData("producing")]
    public void Evaluate_ParticipleWithInconclusiveRole_NotFlagged(string form)
    {
        // Arrange: a verb entry
        var dictionary = Inline(Entry("produce", PartOfSpeech.Verb, "make"));

        // Act: evaluate with an inconclusive role
        var diagnostics = Run($"The {form} item is ready.", dictionary, null);

        // Assert: not flagged
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that third-person forms and the base form still report with an inconclusive role.
    /// </summary>
    [Fact]
    public void Evaluate_ThirdPersonAndBaseWithInconclusiveRole_StillFlagged()
    {
        // Arrange: a verb entry
        var dictionary = Inline(Entry("produce", PartOfSpeech.Verb, "make"));

        // Act: evaluate the third-person and base forms with an inconclusive role
        var third = Assert.Single(Run("The pump produces heat.", dictionary, null));
        var baseForm = Assert.Single(Run("The pump will produce heat.", dictionary, null));

        // Assert: still reported
        Assert.Contains("'produces'", third.Message, StringComparison.Ordinal);
        Assert.Contains("'produce'", baseForm.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that with a noun-and-verb entry and an inconclusive role only non-participial
    ///     origins survive.
    /// </summary>
    [Fact]
    public void Evaluate_NounVerbEntryWithInconclusiveRole_ParticipialOriginDropped()
    {
        // Arrange: noun and verb senses of the same term
        var dictionary = Inline(new Dictionary<string, List<DictionarySenseYaml>>
        {
            ["box"] =
            [
                new DictionarySenseYaml { Pos = PartOfSpeech.Noun, Alternatives = ["tool"] },
                new DictionarySenseYaml { Pos = PartOfSpeech.Verb, Alternatives = ["fit"] }
            ]
        });

        // Act: evaluate a participle and a plural/third-person form
        var participle = Run("The boxed part is ready.", dictionary, null);
        var plural = Assert.Single(Run("The crew has boxes.", dictionary, null));

        // Assert: the participle is dropped, the plural keeps reporting
        Assert.Empty(participle);
        Assert.StartsWith("Ambiguous part of speech for 'boxes'", plural.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a phrasal participial form resolves on its head word.
    /// </summary>
    [Fact]
    public void Evaluate_PhrasalParticiple_FlaggedOnlyForVerbHead()
    {
        // Arrange: a phrasal-verb entry
        var dictionary = Inline(Entry("carry out", PartOfSpeech.Verb, "do"));

        // Act: evaluate with a verb, an inconclusive role and the base phrase
        var asVerb = Run("The crew carried out the task.", dictionary, PartOfSpeech.Verb);
        var unknown = Run("The crew carried out the task.", dictionary, null);
        var asAdjective = Run("The carried out task is done.", dictionary, PartOfSpeech.Adjective);
        var baseUnknown = Run("The crew carry out the task.", dictionary, null);

        // Assert: participle needs the verb; the base phrase is unchanged
        Assert.Single(asVerb);
        Assert.Empty(unknown);
        Assert.Empty(asAdjective);
        Assert.Single(baseUnknown);
    }

    /// <summary>
    ///     Test that an attributive adjective use is not flagged but a verb use is, with the
    ///     production resolver.
    /// </summary>
    [Fact]
    public void Evaluate_ParticipleWithProductionResolver_VerbFlaggedAdjectiveNot()
    {
        // Arrange: the embedded "utilize" entry
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());
        IReadOnlyList<ProseSegment> verbUse = [new ProseSegment("The crew utilized the tool yesterday.", 1, SegmentRole.Paragraph)];
        IReadOnlyList<ProseSegment> adjectiveUse = [new ProseSegment("The utilized tool is old.", 1, SegmentRole.Paragraph)];

        // Act: evaluate both with the default resolver
        var verbDiagnostics = DictionaryChecker.Evaluate("file.md", verbUse, dictionary);
        var adjectiveDiagnostics = DictionaryChecker.Evaluate("file.md", adjectiveUse, dictionary);

        // Assert: only the verb use is flagged
        Assert.Single(verbDiagnostics);
        Assert.Empty(adjectiveDiagnostics);
    }

    /// <summary>
    ///     Test that turning off word-form matching restores exact-term matching.
    /// </summary>
    [Fact]
    public void Evaluate_MatchWordFormsOff_OnlyBaseTermMatches()
    {
        // Arrange: the same entry with and without word-form matching
        var on = Inline(Entry("perform", PartOfSpeech.Verb, "start"));
        var off = Inline(Entry("perform", PartOfSpeech.Verb, "start"), matchWordForms: false);

        // Act: evaluate an inflected form and the base form
        var inflectedOn = Run("The crew performed the task.", on, PartOfSpeech.Verb);
        var inflectedOff = Run("The crew performed the task.", off, PartOfSpeech.Verb);
        var baseOff = Run("The crew must perform the task.", off, PartOfSpeech.Verb);

        // Assert: only the switch-off inflected form is silent
        Assert.Single(inflectedOn);
        Assert.Empty(inflectedOff);
        Assert.Single(baseOff);
    }

    /// <summary>
    ///     Test that inline code and hyphenated compounds are not matched, and case is ignored.
    /// </summary>
    [Fact]
    public void Evaluate_InflectedFormBoundaries_RespectedAndCaseInsensitive()
    {
        // Arrange: a verb entry
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "start"));

        // Act: evaluate code, hyphenated and capitalized text
        var code = Run("Use `performed` as written.", dictionary, PartOfSpeech.Verb);
        var prefixed = Run("The non-performed task is done.", dictionary, PartOfSpeech.Verb);
        var suffixed = Run("The performed-task is done.", dictionary, PartOfSpeech.Verb);
        var capital = Run("Performed tasks are listed.", dictionary, PartOfSpeech.Verb);

        // Assert: code and compounds are ignored; case does not matter
        Assert.Empty(code);
        Assert.Empty(prefixed);
        Assert.Empty(suffixed);
        Assert.Single(capital);
    }

    /// <summary>
    ///     Test that adjective, adverb and any-role senses are never inflected.
    /// </summary>
    [Theory]
    [InlineData("Adjective")]
    [InlineData("Adverb")]
    [InlineData("Any")]
    public void Evaluate_NonNounVerbSense_NotInflected(string posName)
    {
        // Arrange: an entry whose only sense is not a noun or verb
        var pos = Enum.Parse<PartOfSpeech>(posName);
        var dictionary = Inline(Entry("perform", pos, "start"));

        // Act: evaluate an inflected spelling
        var diagnostics = Run("The crew performed the task.", dictionary, null);

        // Assert: nothing is flagged
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that a very large segment still completes and is matched.
    /// </summary>
    [Fact]
    public void Evaluate_LargeSegment_CompletesAndMatches()
    {
        // Arrange: a long segment with one inflected form at the end
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "start"));
        var text = string.Concat(Enumerable.Repeat("The crew checks the unit. ", 20000)) + "It performed well.";

        // Act: evaluate
        var diagnostics = Run(text, dictionary, PartOfSpeech.Verb);

        // Assert: exactly one finding
        Assert.Single(diagnostics);
    }

    /// <summary>
    ///     Evaluates one segment of a given role with a fixed resolver.
    /// </summary>
    private static IReadOnlyList<Diagnostic> RunSegment(
        string text,
        string segmentRole,
        LintDictionary dictionary,
        PartOfSpeech? role)
    {
        IReadOnlyList<ProseSegment> segments = [new ProseSegment(text, 1, Enum.Parse<SegmentRole>(segmentRole))];
        return DictionaryChecker.Evaluate("file.md", segments, dictionary, null, null, new FixedPartOfSpeechResolver(role));
    }

    /// <summary>
    ///     Test double that records the text and span it was asked to resolve.
    /// </summary>
    private sealed class RecordingResolver : IPartOfSpeechResolver
    {
        /// <summary>Gets the text of the last request.</summary>
        public string? Text { get; private set; }

        /// <summary>Gets the matched text of the last request.</summary>
        public string? Span { get; private set; }

        /// <inheritdoc />
        public PartOfSpeech? Resolve(string segmentText, int matchIndex, int matchLength)
        {
            Text = segmentText;
            Span = segmentText.Substring(matchIndex, matchLength);
            return PartOfSpeech.Verb;
        }
    }

    /// <summary>
    ///     Test that a participial form that is the whole heading text is not flagged.
    /// </summary>
    [Theory]
    [InlineData("Performed checks")]
    [InlineData("Performing the checks")]
    public void Evaluate_ParticipleInHeadingWithoutAuxiliary_NotFlagged(string heading)
    {
        // Arrange: a verb entry and a heading that is a label
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "start"));

        // Act: evaluate the heading with a resolver that says verb
        var diagnostics = RunSegment(heading, "Heading", dictionary, PartOfSpeech.Verb);

        // Assert: nothing is flagged
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that a participial form that is a whole table cell is not flagged.
    /// </summary>
    [Fact]
    public void Evaluate_ParticipleInTableCellWithoutAuxiliary_NotFlagged()
    {
        // Arrange: a verb entry and a table cell with a bare participle
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "start"));

        // Act: evaluate the cell
        var diagnostics = RunSegment("Performed", "TableRow", dictionary, PartOfSpeech.Verb);

        // Assert: nothing is flagged
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that a participial form in a heading or cell is flagged when an auxiliary word is present.
    /// </summary>
    [Theory]
    [InlineData("The checks were performed", "Heading")]
    [InlineData("Checks will be performed", "TableRow")]
    public void Evaluate_ParticipleInHeadingWithAuxiliary_Flagged(string text, string segmentRole)
    {
        // Arrange: a verb entry
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "start"));

        // Act: evaluate a clause in a heading or cell
        var diagnostics = RunSegment(text, segmentRole, dictionary, PartOfSpeech.Verb);

        // Assert: the verb use is flagged
        Assert.Single(diagnostics);
    }

    /// <summary>
    ///     Test that the fragment guard does not apply to paragraphs or list items.
    /// </summary>
    [Theory]
    [InlineData("Paragraph")]
    [InlineData("ListItem")]
    public void Evaluate_ParticipleInParagraphWithoutAuxiliary_StillFlagged(string segmentRole)
    {
        // Arrange: a verb entry
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "start"));

        // Act: evaluate the same bare participle outside a heading or cell
        var diagnostics = RunSegment("Performed checks", segmentRole, dictionary, PartOfSpeech.Verb);

        // Assert: it is flagged
        Assert.Single(diagnostics);
    }

    /// <summary>
    ///     Test that base and third-person forms are still flagged in a heading or cell.
    /// </summary>
    [Theory]
    [InlineData("Perform the checks", "Heading")]
    [InlineData("The crew performs", "Heading")]
    [InlineData("Performs", "TableRow")]
    public void Evaluate_BaseAndThirdPersonInHeading_StillFlagged(string text, string segmentRole)
    {
        // Arrange: a verb entry
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "start"));

        // Act: evaluate base or third-person text with an inconclusive resolver
        var diagnostics = RunSegment(text, segmentRole, dictionary, null);

        // Assert: the guard does not touch these forms, which keep reporting
        Assert.Single(diagnostics);
    }

    /// <summary>
    ///     Test that a split phrasal verb is flagged with the full span and an inflected alternative.
    /// </summary>
    [Theory]
    [InlineData("The crew must carry the test out today.", "carry the test out", "start")]
    [InlineData("The crew carries the final test out today.", "carries the final test out", "starts")]
    [InlineData("The crew carried the old test out today.", "carried the old test out", "started")]
    public void Evaluate_SplitPhrasalVerb_FlaggedWithFullSpan(string text, string span, string suggestion)
    {
        // Arrange: a phrasal verb entry with a single-word alternative
        var dictionary = Inline(Entry("carry out", PartOfSpeech.Verb, "start"));

        // Act: evaluate prose with the particle separated from the verb
        var diagnostic = Assert.Single(Run(text, dictionary, PartOfSpeech.Verb));

        // Assert: the message quotes the full span and the alternative is inflected
        Assert.Equal($"Avoid '{span}' (a split form of 'carry out'); use '{suggestion}' instead.", diagnostic.Message);
        Assert.Equal(suggestion, diagnostic.Suggestion);
    }

    /// <summary>
    ///     Test that a phrasal alternative is shown contiguous and flagged as not inflected.
    /// </summary>
    [Fact]
    public void Evaluate_SplitPhrasalVerbWithPhrasalAlternative_ShownInBaseForm()
    {
        // Arrange: a phrasal entry whose alternative is itself a phrasal verb
        var dictionary = Inline(Entry("carry out", PartOfSpeech.Verb, "start up"));

        // Act: evaluate an inflected split form
        var diagnostic = Assert.Single(Run("The crew carried the test out.", dictionary, PartOfSpeech.Verb));

        // Assert: the alternative is shown in its base form with the fallback note
        Assert.Contains("use 'start up' instead.", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("shown in its base form", diagnostic.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a gap of more than three words, or a gap with a stop word, is not a split match.
    /// </summary>
    [Theory]
    [InlineData("The crew must carry one two three four out today.")]
    [InlineData("The crew must carry the load and the test out today.")]
    [InlineData("The crew must carry the test to the pad out today.")]
    public void Evaluate_SplitPhrasalGapLimits_FourWordsNotFlagged(string text)
    {
        // Arrange: a phrasal verb entry
        var dictionary = Inline(Entry("carry out", PartOfSpeech.Verb, "start"));

        // Act: evaluate text whose gap is too long or holds a stop word
        var diagnostics = Run(text, dictionary, PartOfSpeech.Verb);

        // Assert: nothing is flagged
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that the longest allowed gap (three words) is still flagged.
    /// </summary>
    [Fact]
    public void Evaluate_SplitPhrasalGapOfThreeWords_Flagged()
    {
        // Arrange: a phrasal verb entry
        var dictionary = Inline(Entry("carry out", PartOfSpeech.Verb, "start"));

        // Act: evaluate a three-word gap
        var diagnostics = Run("The crew must carry one two three out today.", dictionary, PartOfSpeech.Verb);

        // Assert: one finding
        Assert.Single(diagnostics);
    }

    /// <summary>
    ///     Test that sentence punctuation in the gap prevents a split match.
    /// </summary>
    [Theory]
    [InlineData("We carry the test. The crew goes out.")]
    [InlineData("We carry the test, then go out.")]
    [InlineData("We carry (the test) out.")]
    public void Evaluate_SplitPhrasalWithSentencePunctuationInGap_NotFlagged(string text)
    {
        // Arrange: a phrasal verb entry
        var dictionary = Inline(Entry("carry out", PartOfSpeech.Verb, "start"));

        // Act: evaluate text with punctuation between the verb and the particle
        var diagnostics = Run(text, dictionary, null);

        // Assert: nothing is flagged
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that a multi-word term that is not a phrasal verb is never split.
    /// </summary>
    [Fact]
    public void Evaluate_MultiWordNonPhrasalTerm_NotSplit()
    {
        // Arrange: a three-word term
        var dictionary = Inline(Entry("at the time of", PartOfSpeech.Verb, "when"));

        // Act: evaluate the contiguous term and a split form of it
        var contiguous = Run("It failed at the time of the test.", dictionary, PartOfSpeech.Verb);
        var split = Run("It failed at the old time of the test.", dictionary, PartOfSpeech.Verb);

        // Assert: only the contiguous term is flagged
        Assert.Single(contiguous);
        Assert.Empty(split);
    }

    /// <summary>
    ///     Test that a split match does not also report its head word through a shorter entry.
    /// </summary>
    [Fact]
    public void Evaluate_SplitPhrasalOverlap_ReportsHeadWordOnce()
    {
        // Arrange: a phrasal entry and a single-word entry for its verb
        var disallow = Entry("carry out", PartOfSpeech.Verb, "start");
        disallow["carry"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Verb, Alternatives = ["move"] }];
        var dictionary = Inline(disallow);

        // Act: evaluate a split form
        var diagnostics = Run("The crew must carry the test out today.", dictionary, PartOfSpeech.Verb);

        // Assert: one finding, for the phrasal entry
        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("'carry out'", diagnostic.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an allowed prefix before a verb form is flagged as a prefixed form.
    /// </summary>
    [Theory]
    [InlineData("re-perform", "start")]
    [InlineData("re-performed", "started")]
    [InlineData("pre-performs", "starts")]
    [InlineData("cross-perform", "start")]
    [InlineData("over-performing", "starting")]
    [InlineData("under-performed", "started")]
    [InlineData("out-performs", "starts")]
    [InlineData("mis-performed", "started")]
    [InlineData("co-perform", "start")]
    [InlineData("Re-Perform", "start")]
    public void Evaluate_HyphenatedPrefixForms_Flagged(string token, string suggestion)
    {
        // Arrange: a verb entry
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "start"));

        // Act: evaluate prose with a prefixed verb form
        var diagnostic = Assert.Single(Run($"The crew must {token} the task.", dictionary, PartOfSpeech.Verb));

        // Assert: the whole hyphenated token is named
        Assert.Equal($"Avoid '{token}' (a prefixed form of 'perform'); use '{suggestion}' instead.", diagnostic.Message);
    }

    /// <summary>
    ///     Test that the role of a hyphenated form is resolved on its stem, with the prefix left out.
    /// </summary>
    [Fact]
    public void Evaluate_HyphenatedPrefix_ResolvesOnStem()
    {
        // Arrange: a verb entry and a resolver that records what it is asked
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "start"));
        var resolver = new RecordingResolver();
        IReadOnlyList<ProseSegment> segments = [new ProseSegment("The crew will re-performed the task.", 1, SegmentRole.Paragraph)];

        // Act: evaluate
        DictionaryChecker.Evaluate("file.md", segments, dictionary, null, null, resolver);

        // Assert: the resolver saw the sentence without the prefix and the stem as the span
        Assert.Equal("The crew will performed the task.", resolver.Text);
        Assert.Equal("performed", resolver.Span);
    }

    /// <summary>
    ///     Test that a hyphenated participle is dropped when the role is inconclusive, but a base form is kept.
    /// </summary>
    [Fact]
    public void Evaluate_HyphenatedParticipleWithInconclusiveRole_OnlyBaseFlagged()
    {
        // Arrange: a verb entry
        var dictionary = Inline(Entry("perform", PartOfSpeech.Verb, "start"));

        // Act: evaluate a participle and a base form with an inconclusive resolver
        var participle = Run("The re-performed task is done.", dictionary, null);
        var baseForm = Run("The crew must re-perform the task.", dictionary, null);

        // Assert: the base form is flagged, the ambiguous participle is not
        Assert.Empty(participle);
        Assert.Single(baseForm);
    }

    /// <summary>
    ///     Test that a prefix that is not on the allowlist is not a form of the verb.
    /// </summary>
    [Fact]
    public void Evaluate_HyphenatedUnlistedPrefix_NotFlagged()
    {
        // Arrange: a verb entry
        var dictionary = Inline(Entry("process", PartOfSpeech.Verb, "handle"));

        // Act: evaluate unlisted prefixes and a hyphenated compound that ends with the term
        var diagnostics = Run(
            "The in-process check, the non-processed data, a self-process step and process-time are listed.",
            dictionary,
            PartOfSpeech.Verb);

        // Assert: nothing is flagged
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     Test that a prefixed spelling that is allowed, or an explicit entry, is not flagged as a form.
    /// </summary>
    [Fact]
    public void Evaluate_HyphenatedAllowedSpelling_NotFlagged()
    {
        // Arrange: an entry, an allowed prefixed spelling and a separate explicit entry for another one
        var disallow = Entry("perform", PartOfSpeech.Verb, "start");
        disallow["pre-perform"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Verb, Alternatives = ["prepare"] }];
        var dictionary = Inline(disallow, allow: ["re-perform"]);

        // Act: evaluate the allowed spelling, the explicit entry and another prefixed form
        var allowed = Run("The crew must re-perform the task.", dictionary, PartOfSpeech.Verb);
        var explicitEntry = Run("The crew must pre-perform the task.", dictionary, PartOfSpeech.Verb);
        var other = Run("The crew must co-perform the task.", dictionary, PartOfSpeech.Verb);

        // Assert: the allowed spelling is silent, the explicit entry reports once under its own term
        Assert.Empty(allowed);
        var explicitDiagnostic = Assert.Single(explicitEntry);
        Assert.Contains("use 'prepare' instead", explicitDiagnostic.Message, StringComparison.Ordinal);
        Assert.Single(other);
    }

    /// <summary>
    ///     Test that a hyphenated form is not matched for a noun-only entry or when word forms are off.
    /// </summary>
    [Fact]
    public void Evaluate_HyphenatedPrefixOnNounEntryOrWithFormsOff_NotFlagged()
    {
        // Arrange: a noun-only entry and a verb entry with word forms off
        var nounEntry = Inline(Entry("report", PartOfSpeech.Noun, "document"));
        var formsOff = Inline(Entry("perform", PartOfSpeech.Verb, "start"), matchWordForms: false);

        // Act: evaluate prefixed text
        var noun = Run("The co-report is ready.", nounEntry, null);
        var off = Run("The crew must re-perform the task.", formsOff, PartOfSpeech.Verb);

        // Assert: neither is flagged
        Assert.Empty(noun);
        Assert.Empty(off);
    }

    /// <summary>
    ///     Test that the compound irregular past participle is flagged with the stem-based forms.
    /// </summary>
    [Fact]
    public void Evaluate_CompoundIrregularParticiple_FlaggedAsWithstood()
    {
        // Arrange: a compound irregular verb entry
        var dictionary = Inline(Entry("withstand", PartOfSpeech.Verb, "resist"));

        // Act: evaluate the irregular form and the wrong regular spelling
        var diagnostic = Assert.Single(Run("The valve withstood the load.", dictionary, PartOfSpeech.Verb));
        var regular = Run("The valve " + "withstand" + "ed the load.", dictionary, PartOfSpeech.Verb);

        // Assert: the irregular form is flagged and inflects the alternative; the regular one is not a word
        Assert.Equal("Avoid 'withstood' (a form of 'withstand'); use 'resisted' instead.", diagnostic.Message);
        Assert.Empty(regular);
    }

    /// <summary>
    ///     Test the behavior of a small generic corpus for each form type and context.
    /// </summary>
    [Theory]
    [InlineData("The crew must perform the check.", "Paragraph", 1)]
    [InlineData("The crew performs the check.", "Paragraph", 1)]
    [InlineData("The crew performed the check.", "Paragraph", 1)]
    [InlineData("The crew is performing the check.", "Paragraph", 1)]
    [InlineData("The check was performed.", "ListItem", 1)]
    [InlineData("Performed checks", "ListItem", 1)]
    [InlineData("Performed checks", "Heading", 0)]
    [InlineData("Performing checks", "TableRow", 0)]
    [InlineData("The checks were performed", "Heading", 1)]
    [InlineData("Perform checks", "Heading", 1)]
    [InlineData("The crew must carry the check out.", "Paragraph", 1)]
    [InlineData("The crew must carry out the check.", "Paragraph", 1)]
    [InlineData("The crew carried the whole check out.", "Paragraph", 1)]
    [InlineData("The crew carry the check, and go out.", "Paragraph", 0)]
    [InlineData("The crew must re-perform the check.", "Paragraph", 1)]
    [InlineData("The crew must pre-perform the check.", "Paragraph", 1)]
    [InlineData("The in-process check is done.", "Paragraph", 0)]
    [InlineData("The valve withstood the load.", "Paragraph", 1)]
    [InlineData("The valve withstands the load.", "Paragraph", 1)]
    [InlineData("The valve " + "withstand" + "ed the load.", "Paragraph", 0)]
    [InlineData("The valve is listed.", "Paragraph", 0)]
    public void Evaluate_GenericCorpus_MeetsPerFormExpectations(string text, string segmentRole, int expected)
    {
        // Arrange: three entries covering a regular verb, a phrasal verb and a compound irregular verb
        var disallow = Entry("perform", PartOfSpeech.Verb, "start");
        disallow["carry out"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Verb, Alternatives = ["do"] }];
        disallow["withstand"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Verb, Alternatives = ["resist"] }];
        var dictionary = Inline(disallow);

        // Act: evaluate the sentence as a verb
        var diagnostics = RunSegment(text, segmentRole, dictionary, PartOfSpeech.Verb);

        // Assert: the finding count matches the expectation
        Assert.Equal(expected, diagnostics.Count);
    }
}
