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

using System.Text.Json;
using DemaConsulting.Ste100Mark.Linting;

namespace DemaConsulting.Ste100Mark.Tests.Linting;

/// <summary>
///     Unit tests for the RuleCatalog class.
/// </summary>
[Collection("Sequential")]
public class RuleCatalogTests
{
    /// <summary>
    ///     The complete, exhaustive set of rule codes the linter can emit (cross-checked against
    ///     the "STE100-..." literals in StructuralRules.cs and DictionaryChecker.cs).
    /// </summary>
    private static readonly string[] ExpectedCodes =
    [
        "STE100-4.1", "STE100-8.1", "STE100-4.2", "STE100-DICT",
        "STE100-ADV-PARA", "STE100-ADV-PASSIVE", "STE100-ADV-COMPLEXVERB", "STE100-ADV-INGFORM"
    ];

    /// <summary>
    ///     Test that the catalog contains exactly every rule code the linter can emit.
    /// </summary>
    [Fact]
    public void RuleCatalog_Entries_ContainsEveryEmittedRuleCode()
    {
        // Act: execute the operation being tested
        var codes = RuleCatalog.Entries.Select(e => e.Code).ToHashSet();

        // Assert: verify expected behavior
        Assert.Equal(ExpectedCodes.ToHashSet(), codes);
    }

    /// <summary>
    ///     Test that mechanical rules are classified as official and advisory heuristics are not.
    /// </summary>
    [Fact]
    public void RuleCatalog_Entries_ClassifiesOfficialAndAdvisoryRulesCorrectly()
    {
        // Arrange: the codes expected to be official versus advisory
        string[] officialCodes = ["STE100-4.1", "STE100-8.1", "STE100-4.2", "STE100-DICT"];
        string[] advisoryCodes =
            ["STE100-ADV-PARA", "STE100-ADV-PASSIVE", "STE100-ADV-COMPLEXVERB", "STE100-ADV-INGFORM"];

        // Act & Assert: verify expected behavior
        foreach (var code in officialCodes)
        {
            var entry = RuleCatalog.Entries.Single(e => e.Code == code);
            Assert.True(entry.Official, $"{code} should be classified as official");
        }

        foreach (var code in advisoryCodes)
        {
            var entry = RuleCatalog.Entries.Single(e => e.Code == code);
            Assert.False(entry.Official, $"{code} should be classified as advisory");
        }
    }

    /// <summary>
    ///     Test that STE100-DICT is classified as a citation-form suggestion and every other rule
    ///     is classified as prose advice.
    /// </summary>
    [Fact]
    public void RuleCatalog_Entries_ClassifiesSuggestionKindCorrectly()
    {
        // Act & Assert: verify expected behavior
        foreach (var entry in RuleCatalog.Entries)
        {
            var expectedKind = entry.Code == "STE100-DICT" ? "citationForm" : "advice";
            Assert.Equal(expectedKind, entry.SuggestionKind);
        }
    }

    /// <summary>
    ///     Test that every rule's modes include both procedure and descriptive.
    /// </summary>
    [Fact]
    public void RuleCatalog_Entries_ModesIncludeBothWritingModes()
    {
        // Act & Assert: verify expected behavior
        foreach (var entry in RuleCatalog.Entries)
        {
            Assert.Contains("procedure", entry.Modes);
            Assert.Contains("descriptive", entry.Modes);
        }
    }

    /// <summary>
    ///     Test that ToJson produces a valid, camelCase JSON array with one entry per rule code.
    /// </summary>
    [Fact]
    public void RuleCatalog_ToJson_ProducesValidCamelCaseJsonArray()
    {
        // Act: execute the operation being tested
        var json = RuleCatalog.ToJson();
        using var document = JsonDocument.Parse(json);

        // Assert: verify expected behavior
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Equal(ExpectedCodes.Length, document.RootElement.GetArrayLength());

        var first = document.RootElement[0];
        Assert.True(first.TryGetProperty("code", out _));
        Assert.True(first.TryGetProperty("title", out _));
        Assert.True(first.TryGetProperty("official", out _));
        Assert.True(first.TryGetProperty("suggestionKind", out _));
        Assert.True(first.TryGetProperty("modes", out _));
    }
}
