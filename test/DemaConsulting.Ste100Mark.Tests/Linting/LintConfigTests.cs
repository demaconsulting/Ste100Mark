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
            overrides:
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
            Assert.Single(config.Overrides);
            Assert.Equal("docs/user_guide/procedures/**/*.md", config.Overrides[0].Glob);
            Assert.Equal(LintMode.Procedure, config.Overrides[0].Mode);
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
    ///     Test that ResolveMode returns the default mode when no override matches.
    /// </summary>
    [Fact]
    public void ResolveMode_NoMatchingOverride_ReturnsDefaultMode()
    {
        // Arrange: a config with a default mode and one unrelated override
        var config = new LintConfig
        {
            DefaultMode = LintMode.Descriptive,
            Overrides = [new ModeOverride { Glob = "procedures/**/*.md", Mode = LintMode.Procedure }]
        };

        // Act: execute the operation being tested
        var mode = config.ResolveMode("docs/overview.md");

        // Assert: verify expected behavior
        Assert.Equal(LintMode.Descriptive, mode);
    }

    /// <summary>
    ///     Test that ResolveMode returns the overridden mode for a file matching an override glob.
    /// </summary>
    [Fact]
    public void ResolveMode_MatchingOverrideGlob_ReturnsOverriddenMode()
    {
        // Arrange: a config with a procedure-mode override for a specific folder
        var config = new LintConfig
        {
            DefaultMode = LintMode.Descriptive,
            Overrides = [new ModeOverride { Glob = "procedures/**/*.md", Mode = LintMode.Procedure }]
        };

        // Act: execute the operation being tested
        var mode = config.ResolveMode("procedures/install.md");

        // Assert: verify expected behavior
        Assert.Equal(LintMode.Procedure, mode);
    }

    /// <summary>
    ///     Test that ResolveMode uses the first matching override when multiple overrides are present.
    /// </summary>
    [Fact]
    public void ResolveMode_MultipleOverrides_UsesFirstMatch()
    {
        // Arrange: two overrides that could both match; the first declared should win
        var config = new LintConfig
        {
            DefaultMode = LintMode.Descriptive,
            Overrides =
            [
                new ModeOverride { Glob = "procedures/**/*.md", Mode = LintMode.Procedure },
                new ModeOverride { Glob = "**/*.md", Mode = LintMode.Descriptive }
            ]
        };

        // Act: execute the operation being tested
        var mode = config.ResolveMode("procedures/install.md");

        // Assert: verify expected behavior
        Assert.Equal(LintMode.Procedure, mode);
    }
}
