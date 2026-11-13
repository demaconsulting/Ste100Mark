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

using DemaConsulting.PartOfSpeech;
using DemaConsulting.Ste100Mark.Linting;

namespace DemaConsulting.Ste100Mark.Tests.Linting;

/// <summary>
///     Unit tests for the LintGlossary class. Every fixture uses generic made-up terms.
/// </summary>
[Collection("Sequential")]
public sealed class LintGlossaryTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("ste100mark-glossary-test-");

    /// <summary>
    ///     Removes the temporary directory.
    /// </summary>
    public void Dispose() => _dir.Delete(true);

    /// <summary>
    ///     Writes a file under the temporary directory and returns its full path.
    /// </summary>
    private string Write(string relative, string content)
    {
        var path = Path.Combine(_dir.FullName, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    ///     Writes <c>.ste100mark.yaml</c> with the given <c>dictionary:</c> body and loads the glossary.
    /// </summary>
    private LintGlossary Load(string dictionaryBody)
    {
        var configPath = Write(".ste100mark.yaml", "dictionary:\n" + dictionaryBody);
        var config = LintConfig.Load(configPath);
        return LintGlossary.Load(config, configPath, _dir.FullName);
    }

    /// <summary>
    ///     Test that a configuration without glossary keys gives an empty glossary.
    /// </summary>
    [Fact]
    public void Load_NoGlossaryConfig_ReturnsEmptyGlossary()
    {
        // Act
        var glossary = LintGlossary.Load(new LintConfig(), null, _dir.FullName);

        // Assert
        Assert.Empty(glossary.Terms);
        Assert.Empty(glossary.Deprecated);
        Assert.Null(glossary.Tagging);
    }

    /// <summary>
    ///     Test that an inline string entry is a definition-only noun.
    /// </summary>
    [Fact]
    public void Load_InlineStringEntry_DefaultsToNoun()
    {
        // Act
        var glossary = Load("  glossary:\n    Test Mode: A mode for testing.\n");

        // Assert
        var term = Assert.Single(glossary.Terms);
        Assert.Equal("Test Mode", term.Term);
        Assert.Equal("A mode for testing.", term.Definition);
        Assert.Equal(UposSet.Of(Upos.Noun), term.Pos);
        Assert.True(term.Visible);
        Assert.NotNull(glossary.Tagging);
    }

    /// <summary>
    ///     Test that an inline object entry exposes definition, pos, variants and visible.
    /// </summary>
    [Fact]
    public void Load_InlineObjectEntry_ReadsDefinitionPosVariantsVisible()
    {
        // Act
        var glossary = Load(
            "  glossary:\n    Product A:\n      definition: The first product.\n      pos: proper-noun\n" +
            "      variants: [Product As]\n      visible: false\n");

        // Assert
        var term = Assert.Single(glossary.Terms);
        Assert.Equal("The first product.", term.Definition);
        Assert.Equal(UposSet.Of(Upos.Propn), term.Pos);
        Assert.Equal(["Product As"], term.Variants);
        Assert.False(term.Visible);
    }

    /// <summary>
    ///     Test that a pos list maps to a tag set.
    /// </summary>
    [Fact]
    public void Load_PosList_MapsToUposSet()
    {
        // Act
        var glossary = Load("  glossary:\n    bleed:\n      pos: [Noun, verb]\n");

        // Assert
        Assert.Equal(UposSet.Of(Upos.Noun, Upos.Verb), Assert.Single(glossary.Terms).Pos);
    }

    /// <summary>
    ///     Test that an unsupported pos value is an error naming the term.
    /// </summary>
    [Theory]
    [InlineData("any")]
    [InlineData("determiner")]
    public void Load_InvalidPos_ThrowsNamingTerm(string pos)
    {
        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Load($"  glossary:\n    Zorbix:\n      pos: {pos}\n"));
        Assert.Contains("Zorbix", ex.Message);
        Assert.Contains("pos", ex.Message);
    }

    /// <summary>
    ///     Test that an unknown entry key is an error naming the term and key.
    /// </summary>
    [Fact]
    public void Load_UnknownEntryKey_ThrowsNamingTerm()
    {
        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Load("  glossary:\n    Zorbix:\n      hidden: false\n"));
        Assert.Contains("Zorbix", ex.Message);
        Assert.Contains("hidden", ex.Message);
    }

    /// <summary>
    ///     Test that glossary-file accepts both one path and a list.
    /// </summary>
    [Fact]
    public void Load_GlossaryFileScalarAndList_BothAccepted()
    {
        // Arrange
        Write("a.yaml", "Zorbix: A made-up term.\n");
        Write("b.yaml", "Acme Hub: A made-up place.\n");

        // Act
        var scalar = Load("  glossary-file: a.yaml\n");
        var list = Load("  glossary-file:\n    - a.yaml\n    - b.yaml\n");

        // Assert
        Assert.Single(scalar.Terms);
        Assert.Equal(2, list.Terms.Count);
    }

    /// <summary>
    ///     Test that a relative path resolves against the configuration directory, not the current one.
    /// </summary>
    [Fact]
    public void Load_RelativePath_ResolvesAgainstConfigDirectory()
    {
        // Arrange
        Write("docs/glossary/terms.yaml", "Zorbix: A made-up term.\n");

        // Act
        var glossary = Load("  glossary-file: docs/glossary/terms.yaml\n");

        // Assert
        Assert.Equal(Path.Combine(_dir.FullName, "docs", "glossary", "terms.yaml"), Assert.Single(glossary.Terms).Source);
    }

    /// <summary>
    ///     Test that an absolute path is used as is.
    /// </summary>
    [Fact]
    public void Load_AbsolutePath_IsUsedAsIs()
    {
        // Arrange
        var path = Write("elsewhere/terms.yaml", "Zorbix: A made-up term.\n");
        var config = new LintConfig { Dictionary = new DictionaryConfig { GlossaryFile = path } };
        var unrelated = Path.Combine(_dir.FullName, "unrelated");
        Directory.CreateDirectory(unrelated);

        // Act
        var glossary = LintGlossary.Load(config, null, unrelated);

        // Assert
        Assert.Equal(Path.GetFullPath(path), Assert.Single(glossary.Terms).Source);
    }

    /// <summary>
    ///     Test that a UNC-style path is not combined with the configuration directory (string-level
    ///     check; the share does not exist, so the failure must say unreachable).
    /// </summary>
    [Fact]
    public void Load_UncStylePath_IsResolvedWithoutCombiningWithConfigDirectory()
    {
        // Arrange
        var config = new LintConfig
        {
            Dictionary = new DictionaryConfig { GlossaryFile = @"\\ste100mark-no-such-host\share\terms.yaml" }
        };
        Assert.SkipUnless(OperatingSystem.IsWindows(), "UNC paths are a Windows concept.");

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => LintGlossary.Load(config, null, _dir.FullName));

        // Assert
        Assert.Contains("ste100mark-no-such-host", ex.Message);
        Assert.DoesNotContain(_dir.FullName, ex.Message);
        Assert.Contains("unreachable", ex.Message);
    }

    /// <summary>
    ///     Test that a glob merges all matched files.
    /// </summary>
    [Fact]
    public void Load_GlobPattern_MergesAllMatches()
    {
        // Arrange
        Write("g/one.yaml", "Zorbix: One.\n");
        Write("g/two.yaml", "Acme Hub: Two.\n");

        // Act
        var glossary = Load("  glossary-file: g/*.yaml\n");

        // Assert
        Assert.Equal(2, glossary.Terms.Count);
    }

    /// <summary>
    ///     Test that a glob matching nothing is an error naming the pattern.
    /// </summary>
    [Fact]
    public void Load_GlobMatchingNothing_ThrowsNamingPattern()
    {
        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() => Load("  glossary-file: nothing/*.yaml\n"));
        Assert.Contains("nothing/*.yaml", ex.Message);
        Assert.Contains("matched no files", ex.Message);
    }

    /// <summary>
    ///     Test that a missing file in an existing directory is reported as not found.
    /// </summary>
    [Fact]
    public void Load_MissingFile_ThrowsNotFound()
    {
        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() => Load("  glossary-file: missing.yaml\n"));
        Assert.Contains("not found", ex.Message);
        Assert.Contains("missing.yaml", ex.Message);
    }

    /// <summary>
    ///     Test that a missing directory is reported as unreachable.
    /// </summary>
    [Fact]
    public void Load_MissingDirectory_ThrowsUnreachable()
    {
        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() => Load("  glossary-file: no-dir/terms.yaml\n"));
        Assert.Contains("unreachable", ex.Message);
    }

    /// <summary>
    ///     Test that a ${VAR} reference is expanded from the environment.
    /// </summary>
    [Fact]
    public void Load_EnvironmentVariable_IsExpanded()
    {
        // Arrange
        const string name = "STE100MARK_TEST_GLOSSARY_ROOT_A";
        Write("shared/terms.yaml", "Zorbix: A made-up term.\n");
        Environment.SetEnvironmentVariable(name, Path.Combine(_dir.FullName, "shared").Replace('\\', '/'));
        try
        {
            // Act
            var glossary = Load("  glossary-file: ${" + name + "}/terms.yaml\n");

            // Assert
            Assert.Single(glossary.Terms);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    /// <summary>
    ///     Test that an undefined environment variable is an error naming the variable.
    /// </summary>
    [Fact]
    public void Load_UndefinedEnvironmentVariable_Throws()
    {
        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Load("  glossary-file: ${STE100MARK_TEST_UNDEFINED_VARIABLE}/terms.yaml\n"));
        Assert.Contains("STE100MARK_TEST_UNDEFINED_VARIABLE", ex.Message);
    }

    /// <summary>
    ///     Test that an empty file is valid and yields no terms.
    /// </summary>
    [Fact]
    public void Load_EmptyFile_YieldsNoTerms()
    {
        // Arrange
        Write("empty.yaml", "# nothing here\n");

        // Act
        var glossary = Load("  glossary-file: empty.yaml\n");

        // Assert
        Assert.Empty(glossary.Terms);
        Assert.Null(glossary.Tagging);
    }

    /// <summary>
    ///     Test that identical duplicates (case-insensitive, extra whitespace) are accepted in either order.
    /// </summary>
    [Theory]
    [InlineData("a.yaml", "b.yaml")]
    [InlineData("b.yaml", "a.yaml")]
    public void Load_IdenticalDuplicateAcrossFiles_Accepted(string first, string second)
    {
        // Arrange
        Write("a.yaml", "Test Mode: A mode.\n");
        Write("b.yaml", "test   mode: A mode.\n");

        // Act
        var glossary = Load($"  glossary-file: [{first}, {second}]\n");

        // Assert
        Assert.Single(glossary.Terms);
    }

    /// <summary>
    ///     Test that a whitespace-only difference in the term collapses to one term.
    /// </summary>
    [Fact]
    public void Load_DuplicateAfterWhitespaceNormalization_Deduplicated()
    {
        // Arrange
        Write("a.yaml", "Test Mode: A mode.\n");

        // Act
        var glossary = Load("  glossary:\n    \"Test  Mode\": A mode.\n  glossary-file: a.yaml\n");

        // Assert
        Assert.Equal("Test Mode", Assert.Single(glossary.Terms).Term);
    }

    /// <summary>
    ///     Test that any differing field between two files is a conflict naming the term and both paths.
    /// </summary>
    [Theory]
    [InlineData("Zorbix: One.\n", "Zorbix: Two.\n", "definition")]
    [InlineData("Zorbix:\n  pos: noun\n", "Zorbix:\n  pos: verb\n", "pos")]
    [InlineData("Zorbix:\n  variants: [Zorbixes]\n", "Zorbix:\n  variants: [Zorbixen]\n", "variants")]
    [InlineData("Zorbix:\n  visible: true\n", "Zorbix:\n  visible: false\n", "visible")]
    public void Load_DifferingField_Throws(string contentA, string contentB, string field)
    {
        // Arrange
        var a = Write("a.yaml", contentA);
        var b = Write("b.yaml", contentB);

        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() => Load("  glossary-file: [a.yaml, b.yaml]\n"));
        Assert.Contains("'Zorbix'", ex.Message);
        Assert.Contains(field, ex.Message);
        Assert.Contains(a, ex.Message);
        Assert.Contains(b, ex.Message);
    }

    /// <summary>
    ///     Test that the conflict message does not depend on the order the files are listed.
    /// </summary>
    [Fact]
    public void Load_ConflictOrderIndependent_SameMessage()
    {
        // Arrange
        Write("a.yaml", "Zorbix: One.\n");
        Write("b.yaml", "Zorbix: Two.\n");

        // Act
        var forward = Assert.Throws<InvalidOperationException>(() => Load("  glossary-file: [a.yaml, b.yaml]\n"));
        var backward = Assert.Throws<InvalidOperationException>(() => Load("  glossary-file: [b.yaml, a.yaml]\n"));

        // Assert
        Assert.Equal(forward.Message, backward.Message);
    }

    /// <summary>
    ///     Test that a term both approved and deprecated is a conflict.
    /// </summary>
    [Fact]
    public void Load_TermBothApprovedAndDeprecated_Throws()
    {
        // Arrange
        Write("terms.yaml", "Zorbix: A made-up term.\n");
        Write("dep.yaml", "zorbix:\n  use: Acme Hub\n");

        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Load("  glossary-file: terms.yaml\n  deprecated-file: dep.yaml\n"));
        Assert.Contains("both approved", ex.Message);
        Assert.Contains("deprecated", ex.Message);
        Assert.Contains("Zorbix", ex.Message);
    }

    /// <summary>
    ///     Test that an invisible term is still tagged and allowed.
    /// </summary>
    [Fact]
    public void Load_InvisibleTerm_StillTaggedAndAllowed()
    {
        // Act
        var glossary = Load("  glossary:\n    Zorbix:\n      visible: false\n");

        // Assert
        Assert.NotNull(glossary.Tagging);
        Assert.Contains("Zorbix", glossary.GetAllowedTerms());
    }

    /// <summary>
    ///     Test that a deprecated file maps use and where.
    /// </summary>
    [Fact]
    public void Load_DeprecatedFile_MapsUseAndWhere()
    {
        // Arrange
        Write("dep.yaml", "Old Name:\n  use: New Name\n  where: Legacy pages\n");

        // Act
        var glossary = Load("  deprecated-file: dep.yaml\n");

        // Assert
        var term = Assert.Single(glossary.Deprecated);
        Assert.Equal("Old Name", term.Term);
        Assert.Equal("New Name", term.Use);
        Assert.Equal("Legacy pages", term.Where);
    }

    /// <summary>
    ///     Test that a deprecated entry without use is an error.
    /// </summary>
    [Fact]
    public void Load_DeprecatedMissingUse_Throws()
    {
        // Arrange
        Write("dep.yaml", "Old Name:\n  where: Legacy pages\n");

        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() => Load("  deprecated-file: dep.yaml\n"));
        Assert.Contains("'use'", ex.Message);
    }

    /// <summary>
    ///     Test that a term that is an effective dictionary key is a conflict naming the source.
    /// </summary>
    [Fact]
    public void ValidateAgainst_TermIsDictionaryKey_ThrowsNamingTermAndSource()
    {
        // Arrange: "utilize" is an entry of the embedded dictionary
        var glossary = Load("  glossary:\n    utilize: A made-up meaning.\n");
        var dictionary = LintDictionary.Load(new LintConfig(), _dir.FullName);

        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() => glossary.ValidateAgainst(dictionary));
        Assert.Contains("'utilize'", ex.Message);
        Assert.Contains("embedded default dictionary", ex.Message);
    }

    /// <summary>
    ///     Test that a variant that is a dictionary key is a conflict.
    /// </summary>
    [Fact]
    public void ValidateAgainst_VariantIsDictionaryKey_Throws()
    {
        // Arrange
        var glossary = Load("  glossary:\n    utilise:\n      variants: [utilize]\n");
        var dictionary = LintDictionary.Load(new LintConfig(), _dir.FullName);

        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() => glossary.ValidateAgainst(dictionary));
        Assert.Contains("variant 'utilize'", ex.Message);
    }

    /// <summary>
    ///     Test that a term removed by allow is not a dictionary key and so does not conflict.
    /// </summary>
    [Fact]
    public void ValidateAgainst_TermRemovedByAllow_NoConflict()
    {
        // Arrange
        var configPath = Write(
            ".ste100mark.yaml",
            "dictionary:\n  allow: [utilize]\n  glossary:\n    utilize: A made-up meaning.\n");
        var config = LintConfig.Load(configPath);
        var glossary = LintGlossary.Load(config, configPath, _dir.FullName);
        var dictionary = LintDictionary.Load(config, _dir.FullName, glossary.Deprecated);

        // Act / Assert
        glossary.ValidateAgainst(dictionary);
    }

    /// <summary>
    ///     Test that the glossary mode setting reaches the package glossary options.
    /// </summary>
    [Theory]
    [InlineData("hard", GlossaryMode.Hard)]
    [InlineData("soft", GlossaryMode.Soft)]
    public void Load_HardAndSoftMode_PassedToGlossaryOptions(string mode, GlossaryMode expected)
    {
        // Act
        var glossary = Load($"  glossary-mode: {mode}\n  glossary:\n    Zorbix: A made-up term.\n");

        // Assert
        Assert.Equal(expected, glossary.Tagging!.Options.Mode);
    }

    /// <summary>
    ///     Test that a package rejection is wrapped with the term name.
    /// </summary>
    [Fact]
    public void Load_PackageRejectsEntry_WrappedWithTermName()
    {
        // Act / Assert: a variant with a different token count is rejected by the package
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Load("  glossary:\n    Test Mode:\n      variants: [Test]\n"));
        Assert.Contains("Test Mode", ex.Message);
    }
}
