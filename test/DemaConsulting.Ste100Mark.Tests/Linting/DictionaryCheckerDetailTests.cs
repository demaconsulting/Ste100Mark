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
///     Unit tests for the detail (evidence) collection of the DictionaryChecker class.
/// </summary>
public class DictionaryCheckerDetailTests
{
    /// <summary>
    ///     Test double that returns scripted evidence.
    /// </summary>
    /// <param name="evidence">The evidence to return for every match.</param>
    private sealed class EvidenceResolver(RoleEvidence evidence) : IPartOfSpeechResolver
    {
        /// <inheritdoc />
        public PartOfSpeech? Resolve(string segmentText, int matchIndex, int matchLength) => evidence.Role;

        /// <inheritdoc />
        public RoleEvidence ResolveWithEvidence(string segmentText, int matchIndex, int matchLength) => evidence;
    }

    /// <summary>
    ///     Builds an inline-only dictionary with one verb entry.
    /// </summary>
    private static LintDictionary Dictionary(string term, string alternative, PartOfSpeech pos = PartOfSpeech.Verb)
    {
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                UseEmbedded = false,
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    [term] = [new DictionarySenseYaml { Pos = pos, Alternatives = [alternative] }]
                }
            }
        };
        return LintDictionary.Load(config, Directory.GetCurrentDirectory());
    }

    /// <summary>
    ///     Evaluates one segment with the given resolver.
    /// </summary>
    private static IReadOnlyList<Diagnostic> Run(
        string text,
        LintDictionary dictionary,
        IPartOfSpeechResolver resolver,
        bool detail,
        SegmentRole role = SegmentRole.Paragraph)
    {
        return DictionaryChecker.Evaluate(
            "file.md",
            [new ProseSegment(text, 1, role)],
            dictionary,
            null,
            null,
            resolver,
            detail);
    }

    /// <summary>
    ///     Builds confident verb evidence.
    /// </summary>
    private static RoleEvidence Confident(double margin = 0.90)
    {
        return new RoleEvidence(PartOfSpeech.Verb, "resolved", "verb", 0.97, margin, true, false, "noun", 0.07, true);
    }

    /// <summary>
    ///     Test that detail collection is off by default and leaves the diagnostic unchanged.
    /// </summary>
    [Fact]
    public void Evaluate_DetailOff_NoEvidenceAttached()
    {
        // Act: evaluate without detail
        var diagnostic = Assert.Single(Run("The crew will perform the task.", Dictionary("perform", "carry out"), new EvidenceResolver(Confident()), false));

        // Assert: no evidence
        Assert.Null(diagnostic.Evidence);
    }

    /// <summary>
    ///     Test that detail collection attaches the match, senses, suggestions and tagger evidence
    ///     without changing the message or suggestion.
    /// </summary>
    [Fact]
    public void Evaluate_DetailOn_AttachesEvidenceAndKeepsDefaultFields()
    {
        // Arrange: a dictionary entry and a confident verb use
        var dictionary = Dictionary("perform", "carry out");
        var resolver = new EvidenceResolver(Confident());

        // Act: evaluate with and without detail
        var plain = Assert.Single(Run("The crew will perform the task.", dictionary, resolver, false));
        var detailed = Assert.Single(Run("The crew will perform the task.", dictionary, resolver, true));

        // Assert: identical visible fields, and evidence on the detailed one
        Assert.Equal(plain.Message, detailed.Message);
        Assert.Equal(plain.Suggestion, detailed.Suggestion);
        Assert.Equal(plain.Citations, detailed.Citations);
        var evidence = detailed.Evidence;
        Assert.NotNull(evidence);
        Assert.Equal("perform", evidence.Surface);
        Assert.Equal("perform", evidence.BaseTerm);
        Assert.Equal(["base"], evidence.FormKinds);
        Assert.False(evidence.IsGeneratedForm);
        Assert.Equal("verb", evidence.SensePos);
        Assert.Equal(1, evidence.CandidateCount);
        Assert.Equal("verb", Assert.Single(evidence.Senses).Pos);
        var suggestion = Assert.Single(evidence.Suggestions);
        Assert.Equal("carry out", suggestion.Text);
        Assert.Equal("verb", evidence.Tagger.Tag);
        Assert.Equal(FindingDetail.RuleNotApplicable, evidence.ParticipleRule);
        Assert.Equal(FindingDetail.RuleNotApplicable, evidence.FragmentRule);
    }

    /// <summary>
    ///     Test that generated forms report their grammatical form kinds and an inflected suggestion.
    /// </summary>
    [Theory]
    [InlineData("performs", "third-person", "carries out")]
    [InlineData("performed", "past", "carried out")]
    [InlineData("performing", "gerund", "carrying out")]
    public void Evaluate_DetailOn_FormKindsAndInflectedSuggestion(string form, string expectedKind, string expectedSuggestion)
    {
        // Arrange: a verb entry with a phrasal alternative
        var dictionary = Dictionary("perform", "carry out");

        // Act: evaluate with detail
        var diagnostic = Assert.Single(Run($"The crew {form} the task.", dictionary, new EvidenceResolver(Confident()), true));

        // Assert: generated form flagged with the kind
        var evidence = diagnostic.Evidence;
        Assert.NotNull(evidence);
        Assert.True(evidence.IsGeneratedForm);
        Assert.Contains(expectedKind, evidence.FormKinds);
        Assert.Equal(expectedSuggestion, Assert.Single(evidence.Suggestions).Text);
    }

    /// <summary>
    ///     Test that a split phrasal verb is reported as split with the base term.
    /// </summary>
    [Fact]
    public void Evaluate_DetailOn_SplitPhrasalMarkedSplit()
    {
        // Arrange: a phrasal entry
        var dictionary = Dictionary("carry out", "do");

        // Act: evaluate prose with the particle separated from the verb
        var diagnostics = Run("The crew carry the test out now.", dictionary, new EvidenceResolver(Confident()), true);

        // Assert: one split finding when the checker matches split verbs
        var diagnostic = Assert.Single(diagnostics);
        Assert.NotNull(diagnostic.Evidence);
        Assert.True(diagnostic.Evidence.IsSplit);
        Assert.Equal("carry out", diagnostic.Evidence.BaseTerm);
    }

    /// <summary>
    ///     Test that a hyphen-prefixed form is reported as hyphenated.
    /// </summary>
    [Fact]
    public void Evaluate_DetailOn_HyphenPrefixMarkedHyphenated()
    {
        // Arrange: a verb entry
        var dictionary = Dictionary("perform", "do");

        // Act: evaluate prose with a hyphen prefix
        var diagnostics = Run("The crew must re-perform the task.", dictionary, new EvidenceResolver(Confident()), true);

        // Assert: hyphenated evidence
        var diagnostic = Assert.Single(diagnostics);
        Assert.NotNull(diagnostic.Evidence);
        Assert.True(diagnostic.Evidence.IsHyphenated);
    }

    /// <summary>
    ///     Test that a participial form kept as a verb records the participle rule.
    /// </summary>
    [Fact]
    public void Evaluate_DetailOn_ParticipleKeptAsConfidentVerb_RecordsRule()
    {
        // Arrange: a noun-and-verb style entry; the participle "performed" used as a confident verb
        var dictionary = Dictionary("perform", "do");

        // Act: evaluate
        var diagnostic = Assert.Single(Run("The crew performed the task.", dictionary, new EvidenceResolver(Confident(0.30)), true));

        // Assert: the tagger margin is carried
        Assert.NotNull(diagnostic.Evidence);
        Assert.Equal(0.30, diagnostic.Evidence.Tagger.Margin);
    }

    /// <summary>
    ///     Test that an inconclusive role is carried into the evidence with its outcome.
    /// </summary>
    [Fact]
    public void Evaluate_DetailOn_InconclusiveRole_RecordsOutcome()
    {
        // Arrange: a resolver that cannot decide because the tag confidence is too low
        var resolver = new EvidenceResolver(new RoleEvidence(null, "below-threshold", "noun", 0.55, 0.10, true, false, "verb", 0.45, false));
        var dictionary = Dictionary("perform", "do");

        // Act: evaluate
        var diagnostic = Assert.Single(Run("The crew perform the task.", dictionary, resolver, true));

        // Assert: the outcome and threshold flag are kept
        Assert.NotNull(diagnostic.Evidence);
        Assert.Equal("below-threshold", diagnostic.Evidence.Tagger.Outcome);
        Assert.False(diagnostic.Evidence.Tagger.ThresholdMet);
        Assert.Null(diagnostic.Evidence.Tagger.Role);
    }
}
