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
///     Unit tests for the LintDictionary class.
/// </summary>
[Collection("Sequential")]
public class LintDictionaryTests
{
    /// <summary>
    ///     Test that loading with an all-defaults configuration includes the embedded illustrative
    ///     dictionary's entries.
    /// </summary>
    [Fact]
    public void Load_DefaultConfig_IncludesEmbeddedEntries()
    {
        // Act: execute the operation being tested
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());

        // Assert: the embedded illustrative dictionary is non-empty and includes a known
        // single-sense, verb-only entry.
        Assert.NotEmpty(dictionary.Entries);
        Assert.True(dictionary.TryGetEntry("utilize", out var entry));
        var sense = Assert.Single(entry!.Senses);
        Assert.Equal(PartOfSpeech.Verb, sense.Pos);
        Assert.Contains("use", sense.Alternatives);
    }

    /// <summary>
    ///     Test that the embedded dictionary's multi-sense "impact" term exposes exactly two
    ///     senses with the expected part of speech and alternatives.
    /// </summary>
    [Fact]
    public void Load_MultiSenseEmbeddedTerm_IncludesBothSenses()
    {
        // Act: execute the operation being tested
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());

        // Assert: "impact" has a noun sense (effect) and a verb sense (affect)
        Assert.True(dictionary.TryGetEntry("impact", out var entry));
        Assert.Equal(2, entry!.Senses.Count);

        var nounSense = Assert.Single(entry.Senses, s => s.Pos == PartOfSpeech.Noun);
        Assert.Contains("effect", nounSense.Alternatives);

        var verbSense = Assert.Single(entry.Senses, s => s.Pos == PartOfSpeech.Verb);
        Assert.Contains("affect", verbSense.Alternatives);
    }

    /// <summary>
    ///     Test that setting Dictionary.UseEmbedded=false excludes the embedded entries.
    /// </summary>
    [Fact]
    public void Load_UseEmbeddedFalse_ExcludesEmbeddedEntries()
    {
        // Arrange: configuration explicitly disabling the embedded dictionary
        var config = new LintConfig { Dictionary = new DictionaryConfig { UseEmbedded = false } };

        // Act: execute the operation being tested
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());

        // Assert: verify expected behavior
        Assert.False(dictionary.TryGetEntry("utilize", out _));
    }

    /// <summary>
    ///     Test that an inline disallow entry is added to the merged dictionary.
    /// </summary>
    [Fact]
    public void Load_InlineDisallowEntry_AddedToMergedDictionary()
    {
        // Arrange: an inline disallow entry not present in the embedded dictionary
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    ["gizmo"] = [new DictionarySenseYaml { Pos = PartOfSpeech.Any, Alternatives = ["device"] }]
                }
            }
        };

        // Act: execute the operation being tested
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());

        // Assert: verify expected behavior
        Assert.True(dictionary.TryGetEntry("gizmo", out var entry));
        var sense = Assert.Single(entry!.Senses);
        Assert.Contains("device", sense.Alternatives);
    }

    /// <summary>
    ///     Test that an inline disallow entry fully replaces an embedded entry's sense list with
    ///     the same term, including collapsing a multi-sense embedded term down to a single
    ///     inline-supplied sense ("last writer wins by term" semantics for the new sense-list
    ///     shape).
    /// </summary>
    [Fact]
    public void Load_InlineDisallowOverridesEmbeddedTerm_ReplacesAllSenses()
    {
        // Arrange: override the embedded two-sense "impact" entry with a single any-pos sense
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig
            {
                Disallow = new Dictionary<string, List<DictionarySenseYaml>>
                {
                    ["impact"] =
                    [
                        new DictionarySenseYaml { Pos = PartOfSpeech.Any, Alternatives = ["consequence"] }
                    ]
                }
            }
        };

        // Act: execute the operation being tested
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());

        // Assert: the two-sense embedded entry is fully replaced by the single inline sense
        Assert.True(dictionary.TryGetEntry("impact", out var entry));
        var sense = Assert.Single(entry!.Senses);
        Assert.Equal(PartOfSpeech.Any, sense.Pos);
        Assert.Equal(["consequence"], sense.Alternatives);
    }

    /// <summary>
    ///     Test that an "allow" term removes a matching embedded entry from the merged dictionary.
    /// </summary>
    [Fact]
    public void Load_AllowListedTerm_RemovedFromMergedDictionary()
    {
        // Arrange: allow-list the embedded "utilize" term
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig { Allow = ["utilize"] }
        };

        // Act: execute the operation being tested
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());

        // Assert: verify expected behavior
        Assert.False(dictionary.TryGetEntry("utilize", out _));
    }

    /// <summary>
    ///     Test that an "ignore" term removes a matching embedded entry from the merged dictionary,
    ///     the same as "allow".
    /// </summary>
    [Fact]
    public void Load_IgnoreListedTerm_RemovedFromMergedDictionary()
    {
        // Arrange: ignore-list the embedded "utilize" term
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig { Ignore = ["utilize"] }
        };

        // Act: execute the operation being tested
        var dictionary = LintDictionary.Load(config, Directory.GetCurrentDirectory());

        // Assert: verify expected behavior
        Assert.False(dictionary.TryGetEntry("utilize", out _));
    }

    /// <summary>
    ///     Test that a project-supplied dictionary file's entries are merged in, overriding embedded
    ///     entries with the same term and adding new ones.
    /// </summary>
    [Fact]
    public void Load_ProjectDictionaryFile_MergedOverEmbedded()
    {
        // Arrange: a temporary project dictionary file overriding one term and adding another
        var tempDir = Directory.CreateTempSubdirectory("ste100mark-dict-test-");
        try
        {
            var dictFile = Path.Combine(tempDir.FullName, "project-dictionary.yaml");
            File.WriteAllText(
                dictFile,
                "utilize:\n  - pos: verb\n    alternatives: [employ]\n" +
                "froobnicate:\n  - pos: verb\n    alternatives: [configure]\n");

            var config = new LintConfig
            {
                Dictionary = new DictionaryConfig { File = "project-dictionary.yaml" }
            };

            // Act: execute the operation being tested
            var dictionary = LintDictionary.Load(config, tempDir.FullName);

            // Assert: verify expected behavior
            Assert.True(dictionary.TryGetEntry("utilize", out var utilizeEntry));
            var utilizeSense = Assert.Single(utilizeEntry!.Senses);
            Assert.Equal(["employ"], utilizeSense.Alternatives);
            Assert.True(dictionary.TryGetEntry("froobnicate", out var newEntry));
            var newSense = Assert.Single(newEntry!.Senses);
            Assert.Contains("configure", newSense.Alternatives);
        }
        finally
        {
            Directory.Delete(tempDir.FullName, true);
        }
    }

    /// <summary>
    ///     Test that a missing project dictionary file throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void Load_MissingProjectDictionaryFile_ThrowsInvalidOperationException()
    {
        // Arrange: configuration pointing at a dictionary file that does not exist
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig { File = "does-not-exist.yaml" }
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => LintDictionary.Load(config, Directory.GetCurrentDirectory()));
    }

    /// <summary>
    ///     Test that TryGetEntry lookup is case-insensitive.
    /// </summary>
    [Fact]
    public void TryGetEntry_DifferentCasing_MatchesEntry()
    {
        // Act: execute the operation being tested
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());

        // Assert: verify expected behavior
        Assert.True(dictionary.TryGetEntry("UTILIZE", out _));
    }

    /// <summary>
    ///     Test that the embedded dictionary was substantially expanded and has not accidentally
    ///     shrunk back toward its original small illustrative size.
    /// </summary>
    [Fact]
    public void Load_DefaultConfig_HasSubstantiallyExpandedEntryCount()
    {
        // Act: execute the operation being tested
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());

        // Assert: the embedded dictionary has at least 300 entries
        Assert.True(
            dictionary.Entries.Count >= 300,
            $"Expected at least 300 embedded dictionary entries, found {dictionary.Entries.Count}.");
    }

    /// <summary>
    ///     Test that a new single-sense embedded term ("obtain") is loaded with its expected
    ///     verb-only sense and alternative.
    /// </summary>
    [Fact]
    public void Load_SingleSenseEmbeddedTerm_ObtainHasExpectedAlternative()
    {
        // Act: execute the operation being tested
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());

        // Assert: "obtain" is a single-sense, verb-only entry suggesting "get"
        Assert.True(dictionary.TryGetEntry("obtain", out var entry));
        var sense = Assert.Single(entry!.Senses);
        Assert.Equal(PartOfSpeech.Verb, sense.Pos);
        Assert.Contains("get", sense.Alternatives);
    }

    /// <summary>
    ///     Test that a new multi-alternative embedded term ("aforementioned") exposes all three
    ///     configured alternatives for its single sense.
    /// </summary>
    [Fact]
    public void Load_MultiAlternativeEmbeddedTerm_AforementionedHasThreeAlternatives()
    {
        // Act: execute the operation being tested
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());

        // Assert: "aforementioned" has one sense with three alternatives
        Assert.True(dictionary.TryGetEntry("aforementioned", out var entry));
        var sense = Assert.Single(entry!.Senses);
        Assert.Equal(["this", "that", "earlier"], sense.Alternatives);
    }

    /// <summary>
    ///     Test that the new multi-sense embedded term ("process") exposes both a noun sense and a
    ///     verb sense with distinct alternatives, exercising the POS-disambiguation schema beyond
    ///     the pre-existing "impact"/"function" examples.
    /// </summary>
    [Fact]
    public void Load_MultiSenseEmbeddedTerm_ProcessIncludesNounAndVerbSenses()
    {
        // Act: execute the operation being tested
        var dictionary = LintDictionary.Load(new LintConfig(), Directory.GetCurrentDirectory());

        // Assert: "process" has a noun sense (steps/method) and a verb sense (handle/deal with)
        Assert.True(dictionary.TryGetEntry("process", out var entry));
        Assert.Equal(2, entry!.Senses.Count);

        var nounSense = Assert.Single(entry.Senses, s => s.Pos == PartOfSpeech.Noun);
        Assert.Contains("steps", nounSense.Alternatives);

        var verbSense = Assert.Single(entry.Senses, s => s.Pos == PartOfSpeech.Verb);
        Assert.Contains("handle", verbSense.Alternatives);
    }
}
