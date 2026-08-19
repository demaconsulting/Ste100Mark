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
///     Unit tests for the LintConfig class.
/// </summary>
[Collection("Sequential")]
public class LintConfigTests
{
    /// <summary>
    ///     Test that Load with a null path returns an all-defaults configuration.
    /// </summary>
    [Fact]
    public void Load_NullPath_ReturnsDefaultConfiguration()
    {
        // Act: execute the operation being tested
        var config = LintConfig.Load(null);

        // Assert: verify expected behavior
        Assert.Empty(config.Include);
        Assert.Empty(config.Exclude);
        Assert.Equal(LintMode.Descriptive, config.DefaultMode);
        Assert.Equal(20, config.Rules.MaxWordsProcedure);
        Assert.Equal(25, config.Rules.MaxWordsDescriptive);
    }

    /// <summary>
    ///     Test that Load with a non-existent explicit path throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void Load_NonExistentPath_ThrowsInvalidOperationException()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => LintConfig.Load("does-not-exist.yaml"));
    }

    /// <summary>
    ///     Test that Load parses a full configuration file per the documented schema.
    /// </summary>
    [Fact]
    public void Load_FullConfigurationFile_ParsesAllSections()
    {
        // Arrange: a representative configuration file covering every top-level section
        var tempFile = Path.Combine(Path.GetTempPath(), $"ste100mark-config-{Guid.NewGuid()}.yaml");
        File.WriteAllText(tempFile, """
            include: ["docs/**/*.md", "README.md"]
            exclude: ["**/generated/**"]
            default-mode: descriptive
            profiles:
              - glob: "docs/user_guide/procedures/**/*.md"
                mode: procedure
            rules:
              max-words-procedure: 18
              max-words-descriptive: 22
              allow-semicolons: true
              allow-contractions: true
              max-sentences-paragraph: 4
              passive-voice: error
            dictionary:
              disallow:
                utilize:
                  - pos: verb
                    alternatives: [use]
              allow: [abrasive]
              ignore: [SomeProductName]
            """);

        try
        {
            // Act: execute the operation being tested
            var config = LintConfig.Load(tempFile);

            // Assert: verify expected behavior
            Assert.Equal(["docs/**/*.md", "README.md"], config.Include);
            Assert.Equal(["**/generated/**"], config.Exclude);
            Assert.Equal(LintMode.Descriptive, config.DefaultMode);
            Assert.Single(config.Profiles);
            Assert.Equal("docs/user_guide/procedures/**/*.md", config.Profiles[0].Glob);
            Assert.Equal(LintMode.Procedure, config.Profiles[0].Mode);
            Assert.Equal(18, config.Rules.MaxWordsProcedure);
            Assert.Equal(22, config.Rules.MaxWordsDescriptive);
            Assert.True(config.Rules.AllowSemicolons);
            Assert.True(config.Rules.AllowContractions);
            Assert.Equal(4, config.Rules.MaxSentencesParagraph);
            Assert.Equal(Severity.Error, config.Rules.PassiveVoice);
            Assert.NotNull(config.Dictionary);
            Assert.Contains("utilize", config.Dictionary.Disallow!.Keys);
            Assert.Contains("use", config.Dictionary.Disallow["utilize"][0].Alternatives!);
            Assert.Equal(["abrasive"], config.Dictionary.Allow);
            Assert.Equal(["SomeProductName"], config.Dictionary.Ignore);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Test that Load with malformed YAML throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void Load_MalformedYaml_ThrowsInvalidOperationException()
    {
        // Arrange: a file containing invalid YAML
        var tempFile = Path.Combine(Path.GetTempPath(), $"ste100mark-config-{Guid.NewGuid()}.yaml");
        File.WriteAllText(tempFile, "rules: [this is not a mapping: - broken");

        try
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => LintConfig.Load(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///     Test that ResolveMode returns the default mode when no profile matches.
    /// </summary>
    [Fact]
    public void ResolveMode_NoMatchingProfile_ReturnsDefaultMode()
    {
        // Arrange: a config with a default mode and one unrelated profile
        var config = new LintConfig
        {
            DefaultMode = LintMode.Descriptive,
            Profiles = [new Profile { Glob = "procedures/**/*.md", Mode = LintMode.Procedure }]
        };

        // Act: execute the operation being tested
        var mode = config.ResolveMode("docs/overview.md");

        // Assert: verify expected behavior
        Assert.Equal(LintMode.Descriptive, mode);
    }

    /// <summary>
    ///     Test that ResolveMode returns the overridden mode for a file matching a profile glob.
    /// </summary>
    [Fact]
    public void ResolveMode_MatchingProfileGlob_ReturnsOverriddenMode()
    {
        // Arrange: a config with a procedure-mode profile for a specific folder
        var config = new LintConfig
        {
            DefaultMode = LintMode.Descriptive,
            Profiles = [new Profile { Glob = "procedures/**/*.md", Mode = LintMode.Procedure }]
        };

        // Act: execute the operation being tested
        var mode = config.ResolveMode("procedures/install.md");

        // Assert: verify expected behavior
        Assert.Equal(LintMode.Procedure, mode);
    }

    /// <summary>
    ///     Test that ResolveMode uses the first matching profile that specifies a mode when
    ///     multiple profiles are present.
    /// </summary>
    [Fact]
    public void ResolveMode_MultipleProfiles_UsesFirstMatch()
    {
        // Arrange: two profiles that could both match; the first declared should win
        var config = new LintConfig
        {
            DefaultMode = LintMode.Descriptive,
            Profiles =
            [
                new Profile { Glob = "procedures/**/*.md", Mode = LintMode.Procedure },
                new Profile { Glob = "**/*.md", Mode = LintMode.Descriptive }
            ]
        };

        // Act: execute the operation being tested
        var mode = config.ResolveMode("procedures/install.md");

        // Assert: verify expected behavior
        Assert.Equal(LintMode.Procedure, mode);
    }

    /// <summary>
    ///     Test that ResolveMode skips a matching profile that carries only rule/dictionary deltas
    ///     (a null Mode), falling through to the default mode or a later matching profile.
    /// </summary>
    [Fact]
    public void ResolveMode_MatchingProfileWithNullMode_FallsThroughToDefault()
    {
        // Arrange: a requirements profile with no Mode set, only a Rules delta
        var config = new LintConfig
        {
            DefaultMode = LintMode.Descriptive,
            Profiles = [new Profile { Glob = "docs/requirements/**/*.md", Rules = new RulesOverride { PassiveVoice = Severity.Off } }]
        };

        // Act: execute the operation being tested
        var mode = config.ResolveMode("docs/requirements/spec.md");

        // Assert: the profile matches but has no Mode, so the default mode applies
        Assert.Equal(LintMode.Descriptive, mode);
    }

    /// <summary>
    ///     Test that ResolveRules returns the unmodified global rules when no profile matches.
    /// </summary>
    [Fact]
    public void ResolveRules_NoMatchingProfile_ReturnsGlobalRules()
    {
        // Arrange: a config with global rules and one unrelated profile
        var config = new LintConfig
        {
            Rules = new RulesConfig { PassiveVoice = Severity.Warn },
            Profiles = [new Profile { Glob = "procedures/**/*.md", Rules = new RulesOverride { PassiveVoice = Severity.Off } }]
        };

        // Act: execute the operation being tested
        var rules = config.ResolveRules("docs/overview.md");

        // Assert: verify expected behavior
        Assert.Equal(Severity.Warn, rules.PassiveVoice);
    }

    /// <summary>
    ///     Test that ResolveRules layers a single matching profile's rule delta on top of the
    ///     global rules, leaving unspecified knobs at their global value.
    /// </summary>
    [Fact]
    public void ResolveRules_MatchingProfile_LayersDeltaOverGlobalRules()
    {
        // Arrange: a requirements profile that only disables the passive-voice heuristic
        var config = new LintConfig
        {
            Rules = new RulesConfig { PassiveVoice = Severity.Warn, MaxWordsDescriptive = 25 },
            Profiles = [new Profile { Glob = "docs/requirements/**/*.md", Rules = new RulesOverride { PassiveVoice = Severity.Off } }]
        };

        // Act: execute the operation being tested
        var rules = config.ResolveRules("docs/requirements/spec.md");

        // Assert: the specified knob is overridden; the unspecified knob keeps its global value
        Assert.Equal(Severity.Off, rules.PassiveVoice);
        Assert.Equal(25, rules.MaxWordsDescriptive);
    }

    /// <summary>
    ///     Test that ResolveRules layers every matching profile's delta in declaration order, so a
    ///     file matching two profiles picks up both, with the later profile winning on conflicts.
    /// </summary>
    [Fact]
    public void ResolveRules_MultipleMatchingProfiles_LayersAllDeltasInOrder()
    {
        // Arrange: two profiles that both match "docs/requirements/spec.md"
        var config = new LintConfig
        {
            Rules = new RulesConfig { PassiveVoice = Severity.Warn, MaxSentencesParagraph = 6 },
            Profiles =
            [
                new Profile { Glob = "docs/requirements/**/*.md", Rules = new RulesOverride { PassiveVoice = Severity.Off } },
                new Profile { Glob = "**/*.md", Rules = new RulesOverride { MaxSentencesParagraph = 10, PassiveVoice = Severity.Error } }
            ]
        };

        // Act: execute the operation being tested
        var rules = config.ResolveRules("docs/requirements/spec.md");

        // Assert: both deltas apply; the later profile's PassiveVoice value wins
        Assert.Equal(Severity.Error, rules.PassiveVoice);
        Assert.Equal(10, rules.MaxSentencesParagraph);
    }

    /// <summary>
    ///     Test that ResolveAllowedTerms returns only the global allow/ignore terms when no
    ///     profile matches.
    /// </summary>
    [Fact]
    public void ResolveAllowedTerms_NoMatchingProfile_ReturnsGlobalTermsOnly()
    {
        // Arrange: a global allow list plus one unrelated profile
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig { Allow = ["ste100mark"] },
            Profiles = [new Profile { Glob = "docs/requirements/**/*.md", Dictionary = new DictionaryOverride { Allow = ["shall"] } }]
        };

        // Act: execute the operation being tested
        var allowed = config.ResolveAllowedTerms("docs/overview.md");

        // Assert: verify expected behavior
        Assert.Equal(["ste100mark"], allowed);
    }

    /// <summary>
    ///     Test that ResolveAllowedTerms unions a matching profile's dictionary allowance with the
    ///     global allow list, case-insensitively - the "shall" for requirements documents scenario.
    /// </summary>
    [Fact]
    public void ResolveAllowedTerms_MatchingProfile_UnionsWithGlobalAllowList()
    {
        // Arrange: a global allow list plus a requirements profile allowing "shall"
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig { Allow = ["ste100mark"] },
            Profiles = [new Profile { Glob = "docs/requirements/**/*.md", Dictionary = new DictionaryOverride { Allow = ["shall"] } }]
        };

        // Act: execute the operation being tested
        var allowed = config.ResolveAllowedTerms("docs/requirements/spec.md");

        // Assert: both the global and profile-specific terms are allowed
        Assert.Contains("ste100mark", allowed);
        Assert.Contains("Shall", allowed, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(2, allowed.Count);
    }

    /// <summary>
    ///     Test that ResolveAllowedPhrases returns only the global allow-in-phrase list when no
    ///     profile matches.
    /// </summary>
    [Fact]
    public void ResolveAllowedPhrases_NoMatchingProfile_ReturnsGlobalPhrasesOnly()
    {
        // Arrange: a global allow-in-phrase list plus one unrelated profile
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig { AllowInPhrase = ["swish mix"] },
            Profiles =
            [
                new Profile
                {
                    Glob = "docs/requirements/**/*.md",
                    Dictionary = new DictionaryOverride { AllowInPhrase = ["motion profile"] }
                }
            ]
        };

        // Act: execute the operation being tested
        var allowed = config.ResolveAllowedPhrases("docs/overview.md");

        // Assert: verify expected behavior
        Assert.Equal(["swish mix"], allowed);
    }

    /// <summary>
    ///     Test that ResolveAllowedPhrases unions a matching profile's phrase allowance with the
    ///     global allow-in-phrase list, case-insensitively.
    /// </summary>
    [Fact]
    public void ResolveAllowedPhrases_MatchingProfile_UnionsWithGlobalPhraseList()
    {
        // Arrange: a global allow-in-phrase list plus a requirements profile allowing an
        // additional phrase
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig { AllowInPhrase = ["swish mix"] },
            Profiles =
            [
                new Profile
                {
                    Glob = "docs/requirements/**/*.md",
                    Dictionary = new DictionaryOverride { AllowInPhrase = ["motion profile"] }
                }
            ]
        };

        // Act: execute the operation being tested
        var allowed = config.ResolveAllowedPhrases("docs/requirements/spec.md");

        // Assert: both the global and profile-specific phrases are allowed
        Assert.Contains("swish mix", allowed);
        Assert.Contains("Motion Profile", allowed, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(2, allowed.Count);
    }
}
