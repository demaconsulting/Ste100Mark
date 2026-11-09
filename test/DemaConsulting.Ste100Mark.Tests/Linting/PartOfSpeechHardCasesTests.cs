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
///     A set of 26 hard cases (imperatives, noun-noun compounds, and modals) that, together with the
///     100-sentence corpus in <see cref="PartOfSpeechFalsePositiveCorpusTests"/>, exercise
///     <see cref="PartOfSpeechResolver"/> on the sentence shapes that mislead a part-of-speech tagger.
///     Each row is resolved through the real <c>DemaConsulting.PartOfSpeech</c> tagger
///     (<c>Tagger.Default</c>) at <see cref="PartOfSpeechResolver.MinimumConfidence"/>.
/// </summary>
public class PartOfSpeechHardCasesTests
{
    /// <summary>
    ///     The 26 hard cases: term, sentence, and the expected role ("verb", "noun", or "none" when the
    ///     tagger is below the minimum confidence and the answer is inconclusive).
    /// </summary>
    public static IEnumerable<object[]> HardCases() =>
    [
        // Imperatives (verb at the start of a procedure step)
        ["Check", "Check the valve before use.", "verb"],
        ["Test", "Test the unit daily.", "verb"],
        ["Log", "Log the reading in the record.", "verb"],
        ["Mount", "Mount the bracket on the frame.", "none"],
        ["Clamp", "Clamp the hose to the pipe.", "verb"],
        ["Record", "Record the pressure.", "verb"],
        ["Filter", "Filter the fluid before use.", "verb"],
        ["Seal", "Seal the joint with tape.", "verb"],

        // Noun-noun compounds (the term modifies a following noun)
        ["test", "Check the test fixture for damage.", "noun"],
        ["filter", "Replace the filter housing.", "noun"],
        ["test", "Inspect the pressure test port.", "noun"],
        ["load", "Read the load cell value.", "noun"],
        ["control", "Clean the control panel.", "noun"],
        ["seal", "Inspect the seal ring for cracks.", "noun"],
        ["brake", "Check the brake pad wear.", "noun"],
        ["control", "Check the flow control valve.", "noun"],
        ["test", "The test passed.", "noun"],

        // Modal auxiliaries (verb after can/must/may/will/shall/should)
        ["test", "The operator must test the unit.", "verb"],
        ["filter", "The pump can filter the fluid.", "verb"],
        ["record", "You may record the value.", "verb"],
        ["control", "The system will control the flow.", "verb"],
        ["check", "Technicians shall check the seal.", "verb"],
        ["seal", "The valve should seal the line.", "verb"],
        ["mount", "Engineers can mount the sensor.", "verb"],
        ["clamp", "The tool will clamp the part.", "verb"],

        // Noun after a possessive determiner
        ["control", "Replace the control knob.", "noun"]
    ];

    /// <summary>
    ///     Test that each hard case resolves to the expected role.
    /// </summary>
    [Theory]
    [MemberData(nameof(HardCases))]
    public void Resolve_HardCase_ReturnsExpectedRole(string term, string sentence, string expected)
    {
        // Arrange: a resolver using the real tagger
        var resolver = new PartOfSpeechResolver();

        // Act: resolve the first occurrence of the term
        var result = resolver.Resolve(sentence, sentence.IndexOf(term, StringComparison.Ordinal), term.Length);

        // Assert: the expected role
        PartOfSpeech? role = expected switch
        {
            "verb" => PartOfSpeech.Verb,
            "noun" => PartOfSpeech.Noun,
            _ => null
        };
        Assert.Equal(role, result);
    }

    /// <summary>
    ///     Test that the hard-case set contains exactly 26 cases.
    /// </summary>
    [Fact]
    public void HardCases_ContainsTwentySixCases()
    {
        // Act / Assert: the set size is as documented
        Assert.Equal(26, HardCases().Count());
    }
}
