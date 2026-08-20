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

using DemaConsulting.Ste100Mark.Cli;
using DemaConsulting.Ste100Mark.Linting;

namespace DemaConsulting.Ste100Mark.Tests.Linting;

/// <summary>
///     Unit tests for the Linter orchestrator class.
/// </summary>
/// <remarks>
///     Each test runs against an isolated temporary directory made the current directory for the
///     duration of the test, since <see cref="Linter"/> resolves globs and the default
///     <c>.ste100mark.yaml</c> path relative to <see cref="Directory.GetCurrentDirectory"/>. Tests
///     are sequential (shared process-wide current directory) via the "Sequential" collection.
/// </remarks>
[Collection("Sequential")]
public sealed class LinterTests : IDisposable
{
    private readonly string _originalDirectory;
    private readonly DirectoryInfo _tempDirectory;

    /// <summary>
    ///     Creates an isolated temporary working directory and makes it current for the test.
    /// </summary>
    public LinterTests()
    {
        _originalDirectory = Directory.GetCurrentDirectory();
        _tempDirectory = Directory.CreateTempSubdirectory("ste100mark-linter-test-");
        Directory.SetCurrentDirectory(_tempDirectory.FullName);
    }

    /// <summary>
    ///     Restores the original current directory and removes the temporary directory.
    /// </summary>
    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        _tempDirectory.Delete(true);
    }

    /// <summary>
    ///     Test that linting a clean Markdown file (no violations) produces exit code 0.
    /// </summary>
    [Fact]
    public void Run_CleanMarkdownFile_ProducesSuccessExitCode()
    {
        // Arrange: a short, compliant Markdown file
        File.WriteAllText(Path.Combine(_tempDirectory.FullName, "clean.md"), "# Title\n\nOpen the panel.\n");
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["clean.md", "--silent"]);

            // Act: execute the operation being tested
            Linter.Run(context);

            // Assert: verify expected behavior
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that linting a Markdown file with a semicolon produces a non-zero exit code.
    /// </summary>
    [Fact]
    public void Run_FileWithSemicolon_ProducesFailureExitCode()
    {
        // Arrange: a Markdown file violating the no-semicolons rule
        File.WriteAllText(Path.Combine(_tempDirectory.FullName, "bad.md"), "# Title\n\nOpen the panel; then close it.\n");
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["bad.md", "--silent"]);

            // Act: execute the operation being tested
            Linter.Run(context);

            // Assert: verify expected behavior
            Assert.Equal(1, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that a warn-only finding (passive voice) does not fail the build without --strict.
    /// </summary>
    [Fact]
    public void Run_WarnOnlyFinding_WithoutStrict_ProducesSuccessExitCode()
    {
        // Arrange: a Markdown file matching only the advisory passive-voice heuristic
        File.WriteAllText(Path.Combine(_tempDirectory.FullName, "passive.md"), "# Title\n\nThe report was written by the team.\n");
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["passive.md", "--silent"]);

            // Act: execute the operation being tested
            Linter.Run(context);

            // Assert: verify expected behavior
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that --strict promotes a warn-only finding to a failure exit code.
    /// </summary>
    [Fact]
    public void Run_WarnOnlyFinding_WithStrict_ProducesFailureExitCode()
    {
        // Arrange: the same advisory-only file, this time with --strict
        File.WriteAllText(Path.Combine(_tempDirectory.FullName, "passive.md"), "# Title\n\nThe report was written by the team.\n");
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["passive.md", "--strict", "--silent"]);

            // Act: execute the operation being tested
            Linter.Run(context);

            // Assert: verify expected behavior
            Assert.Equal(1, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that positional glob arguments override the configuration file's include/exclude
    ///     patterns.
    /// </summary>
    [Fact]
    public void Run_PositionalGlobs_OverrideConfigInclude()
    {
        // Arrange: a config that would only include "other.md", but a positional glob for "clean.md"
        File.WriteAllText(Path.Combine(_tempDirectory.FullName, "clean.md"), "# Title\n\nOpen the panel.\n");
        File.WriteAllText(Path.Combine(_tempDirectory.FullName, ".ste100mark.yaml"), "include: [\"other.md\"]\n");
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["clean.md"]);

            // Act: execute the operation being tested
            Linter.Run(context);

            // Assert: the explicitly-globbed file was checked, not the (non-existent) configured one
            var output = outWriter.ToString();
            Assert.Contains("Checked 1 file(s)", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that an explicit --config pointing at a missing file reports an error rather than
    ///     throwing out of Run.
    /// </summary>
    [Fact]
    public void Run_MissingExplicitConfigFile_ReportsErrorWithoutThrowing()
    {
        // Arrange: (none additional — no config file created)
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            using var context = Context.Create(["--config", "missing.yaml", "--silent"]);

            // Act: execute the operation being tested; this must not throw
            var exception = Record.Exception(() => Linter.Run(context));

            // Assert: verify expected behavior
            Assert.Null(exception);
            Assert.Equal(1, context.ExitCode);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that the procedure mode override glob in configuration is honored, flagging a
    ///     sentence that exceeds the 20-word procedure limit but not the 25-word descriptive limit.
    /// </summary>
    [Fact]
    public void Run_ProcedureModeOverride_AppliesStricterWordLimit()
    {
        // Arrange: a 22-word sentence, under a procedure-mode override for the "procedures" folder
        Directory.CreateDirectory(Path.Combine(_tempDirectory.FullName, "procedures"));
        var sentence = string.Join(' ', Enumerable.Repeat("word", 22)) + ".";
        File.WriteAllText(Path.Combine(_tempDirectory.FullName, "procedures", "step.md"), $"# Title\n\n{sentence}\n");
        File.WriteAllText(
            Path.Combine(_tempDirectory.FullName, ".ste100mark.yaml"),
            "default-mode: descriptive\nprofiles:\n  - glob: \"procedures/**/*.md\"\n    mode: procedure\n");

        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["procedures/step.md"]);

            // Act: execute the operation being tested
            Linter.Run(context);

            // Assert: the word-limit rule fired because procedure mode (20-word limit) applied
            var output = outWriter.ToString();
            Assert.Contains("STE100-4.1", output);
            Assert.Equal(1, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that a profile's dictionary allow-list delta permits a term for files matching its
    ///     glob (a requirements-documents profile allowing "shall"), while the same term is still
    ///     flagged for a file outside that profile - proving the allowance is scoped to the profile,
    ///     not applied globally.
    /// </summary>
    [Fact]
    public void Run_ProfileDictionaryAllowList_PermitsTermOnlyWithinProfileGlob()
    {
        // Arrange: a project-wide disallowed "shall" term, allowed only under docs/requirements/
        Directory.CreateDirectory(Path.Combine(_tempDirectory.FullName, "requirements"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory.FullName, "docs"));
        File.WriteAllText(
            Path.Combine(_tempDirectory.FullName, "requirements", "spec.md"),
            "# Title\n\nThe system shall report every error.\n");
        File.WriteAllText(
            Path.Combine(_tempDirectory.FullName, "docs", "overview.md"),
            "# Title\n\nThe system shall not be used here.\n");
        File.WriteAllText(
            Path.Combine(_tempDirectory.FullName, ".ste100mark.yaml"),
            """
            dictionary:
              disallow:
                shall:
                  - pos: verb
                    alternatives: [will]
            profiles:
              - glob: "requirements/**/*.md"
                dictionary:
                  allow: [shall]
            """);

        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["requirements/spec.md", "docs/overview.md"]);

            // Act: execute the operation being tested
            Linter.Run(context);

            // Assert: "shall" is not flagged in the requirements profile, but is flagged elsewhere
            var output = outWriter.ToString();
            Assert.Contains("docs/overview.md", output);
            Assert.DoesNotContain("requirements/spec.md", output);
            Assert.Equal(1, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that <c>dictionary.enabled: false</c> disables the dictionary/vocabulary check
    ///     entirely, while structural/mechanical checks (here, the semicolon rule) still run.
    /// </summary>
    [Fact]
    public void Run_DictionaryDisabled_SuppressesDictionaryFindingsButKeepsStructuralChecks()
    {
        // Arrange: a file that violates both the embedded dictionary ("utilize") and the
        // structural semicolon rule, with the dictionary check disabled via configuration.
        File.WriteAllText(
            Path.Combine(_tempDirectory.FullName, "doc.md"),
            "# Title\n\nPlease utilize the tool; then store it.\n");
        File.WriteAllText(
            Path.Combine(_tempDirectory.FullName, ".ste100mark.yaml"),
            """
            dictionary:
              enabled: false
            """);

        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["doc.md"]);

            // Act: execute the operation being tested
            Linter.Run(context);

            // Assert: the dictionary finding is suppressed, but the semicolon finding still fires
            var output = outWriter.ToString();
            Assert.DoesNotContain("STE100-DICT", output, StringComparison.Ordinal);
            Assert.Contains("STE100-8.1", output, StringComparison.Ordinal);
            Assert.Equal(1, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that the dictionary check runs by default (<c>dictionary.enabled</c> defaults to
    ///     <see langword="true"/>) when no <c>dictionary:</c> section is present at all.
    /// </summary>
    [Fact]
    public void Run_NoDictionaryConfigSection_DictionaryCheckRunsByDefault()
    {
        // Arrange: a file violating the embedded dictionary, with no dictionary configuration
        File.WriteAllText(
            Path.Combine(_tempDirectory.FullName, "doc.md"),
            "# Title\n\nPlease utilize the tool.\n");

        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["doc.md"]);

            // Act: execute the operation being tested
            Linter.Run(context);

            // Assert: the dictionary finding still fires
            var output = outWriter.ToString();
            Assert.Contains("STE100-DICT", output, StringComparison.Ordinal);
            Assert.Equal(1, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
