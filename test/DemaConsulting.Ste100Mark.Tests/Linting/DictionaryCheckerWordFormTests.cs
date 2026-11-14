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
        var prefixed = Run("The re-performed task is done.", dictionary, PartOfSpeech.Verb);
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
}
