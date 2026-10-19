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
///     A set of 26 hard cases (imperatives, noun-noun compounds, and modals) that, together with the
///     100-sentence corpus in <see cref="PartOfSpeechFalsePositiveCorpusTests"/>, were used to choose
///     <see cref="PartOfSpeechResolver.MinimumConfidence"/>. Each row is resolved through the real
///     <c>DemaConsulting.WordSense.Basic</c> tagger.
/// </summary>
public class PartOfSpeechHardCasesTests
{
    /// <summary>
    ///     The 26 hard cases: term, sentence, and whether the term is a verb in that sentence.
    /// </summary>
    public static IEnumerable<object[]> HardCases() =>
    [
        // Imperatives (verb at the start of a procedure step)
        ["Check", "Check the valve before use.", true],
        ["Test", "Test the unit daily.", true],
        ["Log", "Log the reading in the record.", true],
        ["Mount", "Mount the bracket on the frame.", true],
        ["Clamp", "Clamp the hose to the pipe.", true],
        ["Record", "Record the pressure.", true],
        ["Filter", "Filter the fluid before use.", true],
        ["Seal", "Seal the joint with tape.", true],

        // Noun-noun compounds (the term modifies a following noun)
        ["test", "Check the test fixture for damage.", false],
        ["filter", "Replace the filter housing.", false],
        ["test", "Inspect the pressure test port.", false],
        ["load", "Read the load cell value.", false],
        ["control", "Clean the control panel.", false],
        ["seal", "Inspect the seal ring for cracks.", false],
        ["brake", "Check the brake pad wear.", false],
        ["control", "Check the flow control valve.", false],
        ["test", "The test passed.", false],

        // Modal auxiliaries (verb after can/must/may/will/shall/should)
        ["test", "The operator must test the unit.", true],
        ["filter", "The pump can filter the fluid.", true],
        ["record", "You may record the value.", true],
        ["control", "The system will control the flow.", true],
        ["check", "Technicians shall check the seal.", true],
        ["seal", "The valve should seal the line.", true],
        ["mount", "Engineers can mount the sensor.", true],
        ["clamp", "The tool will clamp the part.", true],

        // Noun after a possessive determiner
        ["control", "Replace the control knob.", false]
    ];

    /// <summary>
    ///     Test that each hard case resolves to the expected role.
    /// </summary>
    [Theory]
    [MemberData(nameof(HardCases))]
    public void Resolve_HardCase_ReturnsExpectedRole(string term, string sentence, bool isVerb)
    {
        // Arrange: a resolver using the real Basic tagger
        var resolver = new PartOfSpeechResolver();

        // Act: resolve the first occurrence of the term
        var result = resolver.Resolve(sentence, sentence.IndexOf(term, StringComparison.Ordinal), term.Length);

        // Assert: the expected role
        Assert.Equal(isVerb ? PartOfSpeech.Verb : PartOfSpeech.Noun, result);
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
