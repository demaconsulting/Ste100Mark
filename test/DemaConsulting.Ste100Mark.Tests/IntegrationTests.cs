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

using DemaConsulting.Ste100Mark.Utilities;

namespace DemaConsulting.Ste100Mark.Tests;

/// <summary>
///     System-level integration tests that run the Ste100Mark application via dotnet.
/// </summary>
[Collection("Sequential")]
public class IntegrationTests
{
    private readonly string _dllPath;

    /// <summary>
    ///     Initialize test by locating the Ste100Mark DLL.
    /// </summary>
    public IntegrationTests()
    {
        // The DLL should be in the same directory as the test assembly
        // because the test project references the main project
        var baseDir = AppContext.BaseDirectory;
        _dllPath = PathHelpers.SafePathCombine(baseDir, "DemaConsulting.Ste100Mark.dll");

        Assert.True(File.Exists(_dllPath), $"Could not find Ste100Mark DLL at {_dllPath}");
    }

    /// <summary>
    ///     Test that version flag outputs version information.
    /// </summary>
    [Fact]
    public void Ste100Mark_VersionFlag_Provided_OutputsVersion()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with version flag
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--version");

        // Assert: version string is printed; no banner or errors
        Assert.Equal(0, exitCode);
        Assert.Matches(@"\d+\.\d+\.\d+", output);
        Assert.DoesNotContain("Error", output);
        Assert.DoesNotContain("Copyright", output);
    }

    /// <summary>
    ///     Test that help flag outputs usage information.
    /// </summary>
    [Fact]
    public void Ste100Mark_HelpFlag_Provided_OutputsUsageInformation()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with help flag
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--help");

        // Assert: usage text contains required sections and key flags
        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output);
        Assert.Contains("Options:", output);
        Assert.Contains("--version", output);
        Assert.Contains("--help", output);
    }

    /// <summary>
    ///     Test that no arguments displays the tool banner and runs default logic.
    /// </summary>
    /// <remarks>
    ///     Runs in an isolated working directory containing one deterministic, ASD-STE100-compliant
    ///     Markdown file, so the default <c>**/*.md</c> include pattern matches exactly that file and
    ///     the run genuinely exercises "no arguments" end-to-end (default file selection, a clean
    ///     lint pass, exit code 0) rather than opting out of the default file-selection behavior via
    ///     <c>--allow-empty</c>.
    /// </remarks>
    [Fact]
    public void Ste100Mark_NoArguments_Invoked_DisplaysBanner()
    {
        // Arrange: an isolated working directory containing one compliant Markdown file
        var workingDirectory = Directory.CreateTempSubdirectory("ste100mark-integration-").FullName;
        try
        {
            File.WriteAllText(
                Path.Combine(workingDirectory, "doc.md"),
                "# Title\n\nOpen the panel.\n");

            // Act: run the tool with no arguments
            var exitCode = Runner.RunInDirectory(out var output, workingDirectory, "dotnet", _dllPath);

            // Assert: banner is displayed with tool name and copyright; exit code is success
            Assert.Equal(0, exitCode);
            Assert.Contains("Ste100Mark version", output);
            Assert.Contains("Copyright", output);
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
        }
    }

    /// <summary>
    ///     Test that validate flag runs self-validation and outputs summary.
    /// </summary>
    [Fact]
    public void Ste100Mark_ValidateFlag_Provided_RunsValidation()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with validate flag
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--validate");

        // Assert: validation summary is present; exit code is success
        Assert.Equal(0, exitCode);
        Assert.Contains("Total Tests:", output);
        Assert.Contains("Passed:", output);
    }

    /// <summary>
    ///     Test that validate with --results flag generates a TRX file.
    /// </summary>
    [Fact]
    public void Ste100Mark_ValidateWithTrxResults_Requested_GeneratesTrxFile()
    {
        // Arrange: temporary TRX results file path
        var resultsFile = Path.Combine(Path.GetTempPath(), $"integration_test_{Guid.NewGuid()}.trx");

        try
        {
            // Act: run validation with TRX results output
            var exitCode = Runner.Run(
                out var _,
                "dotnet",
                _dllPath,
                "--validate",
                "--results",
                resultsFile);

            // Assert: results file is created with valid TRX structure
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(resultsFile), "Results file was not created");

            var trxContent = File.ReadAllText(resultsFile);
            Assert.Contains("<TestRun", trxContent);
            Assert.Contains("</TestRun>", trxContent);
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that validate with --result (legacy alias) flag generates a results file.
    /// </summary>
    [Fact]
    public void Ste100Mark_ResultAlias_LegacyFlag_WritesResultsFile()
    {
        // Arrange: temporary TRX results file path; use legacy --result alias
        var resultsFile = Path.Combine(Path.GetTempPath(), $"integration_test_{Guid.NewGuid()}.trx");

        try
        {
            // Act: run validation with legacy --result alias
            var exitCode = Runner.Run(
                out var _,
                "dotnet",
                _dllPath,
                "--validate",
                "--result",
                resultsFile);

            // Assert: results file is created; exit code is success
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(resultsFile), "Results file was not created for legacy --result alias");
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that validate with an unsupported results extension returns a non-zero exit code.
    /// </summary>
    [Fact]
    public void Ste100Mark_ValidateWithBadExtension_ExtensionInvalid_ReturnsNonZero()
    {
        // Arrange: unsupported results file extension triggers WriteError → ExitCode 1
        var resultsFile = Path.Combine(Path.GetTempPath(), $"integration_test_{Guid.NewGuid()}.bad");

        try
        {
            // Act: run validation with unsupported extension
            var exitCode = Runner.Run(
                out var _,
                "dotnet",
                _dllPath,
                "--validate",
                "--results",
                resultsFile);

            // Assert: non-zero exit code indicates the error path was triggered
            Assert.NotEqual(0, exitCode);
            Assert.False(File.Exists(resultsFile), "No file should be created for unsupported extension");
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that silent flag suppresses output.
    /// </summary>
    [Fact]
    public void Ste100Mark_SilentFlag_Provided_SuppressesOutput()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with --version and --silent to produce deterministic silent output
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--version",
            "--silent");

        // Assert: no console output in silent mode
        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(output), $"Expected no output in silent mode but got: {output}");
    }

    /// <summary>
    ///     Test that log flag writes output to a file.
    /// </summary>
    /// <remarks>
    ///     <c>--allow-empty</c> is supplied because this test asserts on the log-file/console
    ///     behavior, not on lint results, and the process working directory used by
    ///     <see cref="Runner.Run"/> may not contain any Markdown files matching the default
    ///     <c>**/*.md</c> include pattern; without it, a zero-matched selection would now correctly
    ///     produce exit code 2, which is unrelated to what this test verifies.
    /// </remarks>
    [Fact]
    public void Ste100Mark_LogFlag_Provided_WritesOutputToFile()
    {
        // Arrange: temporary log file path
        var logFile = Path.GetTempFileName();

        try
        {
            // Act: run the tool with log flag, accepting an empty file selection
            var exitCode = Runner.Run(
                out var output,
                "dotnet",
                _dllPath,
                "--log",
                logFile,
                "--allow-empty");

            // Assert: log file is created and contains tool output; console output matches
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(logFile), "Log file was not created");

            var logContent = File.ReadAllText(logFile);
            Assert.Contains("Ste100Mark version", logContent);
            Assert.Contains("Ste100Mark version", output);
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test that validate with --results flag generates a JUnit XML file.
    /// </summary>
    [Fact]
    public void Ste100Mark_ValidateWithXmlResults_Requested_GeneratesJUnitFile()
    {
        // Arrange: temporary XML results file path
        var resultsFile = Path.Combine(Path.GetTempPath(), $"integration_test_{Guid.NewGuid()}.xml");

        try
        {
            // Act: run validation with JUnit XML results output
            var exitCode = Runner.Run(
                out var _,
                "dotnet",
                _dllPath,
                "--validate",
                "--results",
                resultsFile);

            // Assert: results file is created with valid JUnit structure
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(resultsFile), "Results file was not created");

            var xmlContent = File.ReadAllText(resultsFile);
            Assert.Contains("<testsuites", xmlContent);
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that an unknown argument causes an error message and non-zero exit code.
    /// </summary>
    [Fact]
    public void Ste100Mark_UnknownArgument_Provided_ReturnsError()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with an unknown argument
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--unknown");

        // Assert: non-zero exit code and error message naming the unrecognized flag
        Assert.NotEqual(0, exitCode);
        Assert.Contains("Error", output);
        Assert.Contains("--unknown", output);
    }

    /// <summary>
    ///     Test that validate with depth flag outputs headings at the specified depth.
    /// </summary>
    [Fact]
    public void Ste100Mark_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run validation with heading depth 3
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--validate",
            "--depth",
            "3");

        // Assert: output contains level-3 markdown headings
        Assert.Equal(0, exitCode);
        Assert.Contains("###", output);
    }

    /// <summary>
    ///     Test that linting a Markdown file with a semicolon violation exits non-zero and reports
    ///     the violation in text format.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintFileWithSemicolon_ReportsErrorAndReturnsNonZero()
    {
        // Arrange: an isolated working directory containing a non-compliant Markdown file
        var workingDirectory = Directory.CreateTempSubdirectory("ste100mark-integration-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(workingDirectory, "doc.md"), "# Title\n\nOpen the panel; then close it.\n");

            // Act: run the linter against the file
            var exitCode = Runner.RunInDirectory(out var output, workingDirectory, "dotnet", _dllPath, "doc.md");

            // Assert: verify expected behavior
            Assert.NotEqual(0, exitCode);
            Assert.Contains("STE100-8.1", output);
            Assert.Contains("[ERROR]", output);
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
        }
    }

    /// <summary>
    ///     Test that dictionary part-of-speech resolution through the real linter separates a
    ///     disallowed verb usage from a legal noun usage of the same word, and reports every sense
    ///     as ambiguous when the tagger is not confident.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintDictionaryTermWithPartOfSpeechResolution_ReportsOnlyDisallowedOrAmbiguousUsage()
    {
        // Arrange: a configuration with a verb-only entry ("check") and a two-sense entry ("block"),
        // and a document with a noun usage, a verb usage, and a usage the tagger answers with low
        // confidence ("Block 0" is tagged as a proper noun) on separate lines
        var workingDirectory = Directory.CreateTempSubdirectory("ste100mark-integration-").FullName;
        try
        {
            File.WriteAllText(
                Path.Combine(workingDirectory, ".ste100mark.yaml"),
                "dictionary:\n" +
                "  use-embedded: false\n" +
                "  disallow:\n" +
                "    check:\n" +
                "      - pos: verb\n" +
                "        alternatives: [inspect]\n" +
                "    block:\n" +
                "      - pos: noun\n" +
                "        alternatives: [valve]\n" +
                "      - pos: verb\n" +
                "        alternatives: [open]\n");
            File.WriteAllText(
                Path.Combine(workingDirectory, "doc.md"),
                "# Title\n\n" +
                "The check passed.\n\n" +
                "Check the valve before use.\n\n" +
                "Block 0 failed the self-test.\n");

            // Act: run the linter against the file
            var exitCode = Runner.RunInDirectory(out var output, workingDirectory, "dotnet", _dllPath, "doc.md");

            // Assert: the noun usage (line 3) is not reported, the verb usage (line 5) is reported
            // with its verb alternative, and the low-confidence usage (line 7) is reported as ambiguous
            Assert.NotEqual(0, exitCode);
            Assert.DoesNotContain("doc.md:3", output);
            Assert.Contains("doc.md:5", output);
            Assert.Contains("use 'inspect' instead", output);
            Assert.Contains("doc.md:7", output);
            Assert.Contains("Ambiguous part of speech for 'Block'", output);
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
        }
    }

    /// <summary>
    ///     Test that an inflected spelling of a disallowed term is flagged by the CLI by default and
    ///     not when match-word-forms is false.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintInflectedDictionaryTerm_FlaggedUnlessWordFormsDisabled()
    {
        // Arrange: a verb entry and a document that uses an inflected form of it
        var workingDirectory = Directory.CreateTempSubdirectory("ste100mark-integration-").FullName;
        try
        {
            const string entry = "dictionary:\n  use-embedded: false\n  disallow:\n    perform:\n      - pos: verb\n        alternatives: [start]\n";
            File.WriteAllText(Path.Combine(workingDirectory, "doc.md"), "# Title\n\nThe crew performed the task.\n");
            File.WriteAllText(Path.Combine(workingDirectory, ".ste100mark.yaml"), entry);

            // Act: run with the default setting, then with word-form matching disabled
            var defaultExit = Runner.RunInDirectory(out var defaultOutput, workingDirectory, "dotnet", _dllPath, "doc.md");
            File.WriteAllText(Path.Combine(workingDirectory, ".ste100mark.yaml"), entry + "  match-word-forms: false\n");
            var offExit = Runner.RunInDirectory(out var offOutput, workingDirectory, "dotnet", _dllPath, "doc.md");

            // Assert: flagged with the inflected alternative by default, clean when disabled
            Assert.NotEqual(0, defaultExit);
            Assert.Contains("Avoid 'performed' (a form of 'perform'); use 'started' instead.", defaultOutput);
            Assert.Equal(0, offExit);
            Assert.DoesNotContain("STE100-DICT", offOutput);
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
        }
    }

    /// <summary>
    ///     Test that the CLI flags split phrasal verbs, hyphenated prefix forms and compound irregular
    ///     forms, and does not flag a participle used as a heading fragment.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintWordFormCoverage_FlagsSplitHyphenAndCompoundForms()
    {
        // Arrange: three verb entries and a document that uses the extra forms
        var workingDirectory = Directory.CreateTempSubdirectory("ste100mark-integration-").FullName;
        try
        {
            const string config = "dictionary:\n  use-embedded: false\n  disallow:\n    carry out:\n      - pos: verb\n        alternatives: [start]\n    perform:\n      - pos: verb\n        alternatives: [do]\n    withstand:\n      - pos: verb\n        alternatives: [resist]\n";
            const string document = "# Performed checks\n\nThe crew carried the old test out today.\n\nThe team will re-perform the check.\n\nThe valve withstood the load.\n";
            File.WriteAllText(Path.Combine(workingDirectory, "doc.md"), document);
            File.WriteAllText(Path.Combine(workingDirectory, ".ste100mark.yaml"), config);

            // Act: run the linter
            var exitCode = Runner.RunInDirectory(out var output, workingDirectory, "dotnet", _dllPath, "doc.md");

            // Assert: each Part A form is reported and the heading fragment is not
            Assert.NotEqual(0, exitCode);
            Assert.Contains("carried the old test out", output);
            Assert.Contains("re-perform", output);
            Assert.Contains("withstood", output);
            Assert.DoesNotContain("'Performed'", output);
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
        }
    }

    /// <summary>
    ///     Test that linting a clean Markdown file exits zero.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintCleanFile_ReturnsZeroExitCode()
    {
        // Arrange: an isolated working directory containing a compliant Markdown file
        var workingDirectory = Directory.CreateTempSubdirectory("ste100mark-integration-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(workingDirectory, "doc.md"), "# Title\n\nOpen the panel.\n");

            // Act: run the linter against the file
            var exitCode = Runner.RunInDirectory(out var output, workingDirectory, "dotnet", _dllPath, "doc.md");

            // Assert: verify expected behavior
            Assert.Equal(0, exitCode);
            Assert.Contains("0 error(s)", output);
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
        }
    }

    /// <summary>
    ///     Test that linting with --format json produces a single valid JSON document on stdout,
    ///     with no banner text mixed in.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintWithJsonFormat_ProducesSingleValidJsonDocument()
    {
        // Arrange: an isolated working directory containing a non-compliant Markdown file
        var workingDirectory = Directory.CreateTempSubdirectory("ste100mark-integration-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(workingDirectory, "doc.md"), "# Title\n\nOpen the panel; then close it.\n");

            // Act: run the linter against the file requesting JSON output
            var exitCode = Runner.RunInDirectory(out var output, workingDirectory, "dotnet", _dllPath, "doc.md", "--format", "json");

            // Assert: the entire output parses as a single JSON document (no banner text mixed in)
            Assert.NotEqual(0, exitCode);
            using var document = System.Text.Json.JsonDocument.Parse(output);
            Assert.True(document.RootElement.GetProperty("errorCount").GetInt32() >= 1);
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
        }
    }

    /// <summary>
    ///     Test that the published CLI's JSON output includes a <c>files</c> array listing every
    ///     file the resolved selection checked, with its relative path and <c>"checked"</c>
    ///     status, so a caller can learn the effective file scope without reimplementing the
    ///     tool's glob resolution.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintWithJsonFormat_ReportsCheckedFilesArray()
    {
        // Arrange: an isolated working directory containing two compliant Markdown files
        var workingDirectory = Directory.CreateTempSubdirectory("ste100mark-integration-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(workingDirectory, "clean.md"), "# Title\n\nOpen the panel.\n");
            File.WriteAllText(Path.Combine(workingDirectory, "other.md"), "# Title\n\nClose the panel.\n");

            // Act: run the linter against both files requesting JSON output
            var exitCode = Runner.RunInDirectory(out var output, workingDirectory, "dotnet", _dllPath, "*.md", "--format", "json");

            // Assert: the files array lists both checked files as "checked"
            Assert.Equal(0, exitCode);
            using var document = System.Text.Json.JsonDocument.Parse(output);
            var files = document.RootElement.GetProperty("files").EnumerateArray().ToList();
            Assert.Equal(2, files.Count);
            Assert.All(files, f => Assert.Equal("checked", f.GetProperty("status").GetString()));
            var paths = files.Select(f => f.GetProperty("path").GetString()).ToList();
            Assert.Contains("clean.md", paths);
            Assert.Contains("other.md", paths);
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
        }
    }

    /// <summary>
    ///     Test that --strict promotes an otherwise-passing advisory finding to a non-zero exit code.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintWithStrictFlag_PromotesWarningsToFailure()
    {
        // Arrange: an isolated working directory containing a file with only an advisory finding
        var workingDirectory = Directory.CreateTempSubdirectory("ste100mark-integration-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(workingDirectory, "doc.md"), "# Title\n\nThe report was written by the team.\n");

            // Act: run without and with --strict
            var withoutStrict = Runner.RunInDirectory(out _, workingDirectory, "dotnet", _dllPath, "doc.md");
            var withStrict = Runner.RunInDirectory(out _, workingDirectory, "dotnet", _dllPath, "doc.md", "--strict");

            // Assert: verify expected behavior
            Assert.Equal(0, withoutStrict);
            Assert.NotEqual(0, withStrict);
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
        }
    }

    /// <summary>
    ///     Test that an explicit --config pointing at a missing file reports an error and returns
    ///     non-zero without crashing the process.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintWithMissingConfigFile_ReturnsNonZeroWithErrorMessage()
    {
        // Arrange: an isolated working directory with no configuration file present
        var workingDirectory = Directory.CreateTempSubdirectory("ste100mark-integration-").FullName;
        try
        {
            // Act: run the linter with an explicit, non-existent configuration file
            var exitCode = Runner.RunInDirectory(out var output, workingDirectory, "dotnet", _dllPath, "--config", "missing.yaml");

            // Assert: verify expected behavior
            Assert.NotEqual(0, exitCode);
            Assert.Contains("missing.yaml", output);
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
        }
    }

    /// <summary>
    ///     Test that running the published CLI with a glob matching zero files returns the distinct
    ///     exit code 2 and text output that plainly states nothing was checked, rather than the
    ///     ambiguous zero-count "clean pass" wording.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintWithNoMatchingFiles_ReturnsNoFilesMatchedExitCode()
    {
        // Arrange: an isolated, empty working directory with no Markdown files
        var workingDirectory = Directory.CreateTempSubdirectory("ste100mark-integration-").FullName;
        try
        {
            // Act: run the linter against a glob that matches nothing on disk
            var exitCode = Runner.RunInDirectory(out var output, workingDirectory, "dotnet", _dllPath, "no-such-file-*.md");

            // Assert: verify expected behavior
            Assert.Equal(2, exitCode);
            Assert.Contains("No files matched the configured file selection", output);
            Assert.DoesNotContain("Checked 0 file(s): 0 error(s), 0 warning(s).", output);
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
        }
    }

    /// <summary>
    ///     Test that <c>--allow-empty</c> opts a zero-matched glob selection back into the ordinary
    ///     success exit code (0) for the published CLI.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintWithNoMatchingFilesAndAllowEmpty_ReturnsZeroExitCode()
    {
        // Arrange: the same empty working directory and zero-match glob as above
        var workingDirectory = Directory.CreateTempSubdirectory("ste100mark-integration-").FullName;
        try
        {
            // Act: run the linter with --allow-empty against a glob that matches nothing
            var exitCode = Runner.RunInDirectory(out _, workingDirectory, "dotnet", _dllPath, "no-such-file-*.md", "--allow-empty");

            // Assert: verify expected behavior
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
        }
    }

    /// <summary>
    ///     Test that JSON-format output reports <c>noFilesMatched: true</c> and exit code 2 when the
    ///     file selection matches zero files, disambiguating <c>filesChecked: 0</c> for JSON
    ///     consumers of the published CLI.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintWithNoMatchingFilesJsonFormat_ReportsNoFilesMatchedTrue()
    {
        // Arrange: an isolated, empty working directory with no Markdown files
        var workingDirectory = Directory.CreateTempSubdirectory("ste100mark-integration-").FullName;
        try
        {
            // Act: run the linter with --format json against a glob that matches nothing
            var exitCode = Runner.RunInDirectory(out var output, workingDirectory, "dotnet", _dllPath, "no-such-file-*.md", "--format", "json");

            // Assert: the entire output parses as a single JSON document with noFilesMatched true
            Assert.Equal(2, exitCode);
            using var document = System.Text.Json.JsonDocument.Parse(output);
            Assert.True(document.RootElement.GetProperty("noFilesMatched").GetBoolean());
            Assert.Equal(0, document.RootElement.GetProperty("filesChecked").GetInt32());
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
        }
    }

    /// <summary>
    ///     Test that <c>--list-rules</c> outputs the rule catalog as JSON and exits successfully,
    ///     without requiring any Markdown files to exist in the working directory.
    /// </summary>
    [Fact]
    public void Ste100Mark_ListRulesFlag_Provided_OutputsRuleCatalogJson()
    {
        // Arrange: an isolated, empty working directory with no Markdown files
        var workingDirectory = Directory.CreateTempSubdirectory("ste100mark-integration-").FullName;
        try
        {
            // Act: run the linter with --list-rules and no other arguments
            var exitCode = Runner.RunInDirectory(out var output, workingDirectory, "dotnet", _dllPath, "--list-rules");

            // Assert: the entire output parses as a single JSON array containing every expected rule code
            Assert.Equal(0, exitCode);
            using var document = System.Text.Json.JsonDocument.Parse(output);
            Assert.Equal(System.Text.Json.JsonValueKind.Array, document.RootElement.ValueKind);
            var codes = document.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("code").GetString())
                .ToHashSet();
            var expectedCodes = new[]
            {
                "STE100-4.1", "STE100-8.1", "STE100-4.2", "STE100-DICT",
                "STE100-ADV-PARA", "STE100-ADV-PASSIVE", "STE100-ADV-COMPLEXVERB", "STE100-ADV-INGFORM"
            };
            foreach (var expectedCode in expectedCodes)
            {
                Assert.Contains(expectedCode, codes);
            }
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
        }
    }

    /// <summary>
    ///     Runs the tool on a document in a temporary directory populated with the given files.
    /// </summary>
    /// <param name="files">Relative path to content for every fixture file.</param>
    /// <returns>The exit code and combined output.</returns>
    private (int ExitCode, string Output) RunWithFiles(Dictionary<string, string> files)
    {
        var workingDirectory = Directory.CreateTempSubdirectory("ste100mark-integration-").FullName;
        try
        {
            foreach (var (relative, content) in files)
            {
                var path = Path.Combine(workingDirectory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, content);
            }

            var exitCode = Runner.RunInDirectory(out var output, workingDirectory, "dotnet", _dllPath, "doc.md");
            return (exitCode, output);
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
        }
    }

    /// <summary>
    ///     Test that a glossary file lets a multi-word term pass and the run exits zero, while the
    ///     same document without the glossary is flagged.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintWithGlossaryFile_TermNotFlaggedAndExitZero()
    {
        // Arrange: a two-sense "process" entry; "Process Control" is ambiguous without a glossary
        const string dictionary =
            "  use-embedded: false\n  disallow:\n    process:\n      - pos: verb\n        alternatives: [do]\n      - pos: adjective\n        alternatives: [automatic]\n";
        const string document = "# Title\n\nProcess control is easy.\n";

        // Act
        var without = RunWithFiles(new()
        {
            [".ste100mark.yaml"] = "dictionary:\n" + dictionary,
            ["doc.md"] = document
        });
        var with = RunWithFiles(new()
        {
            [".ste100mark.yaml"] = "dictionary:\n" + dictionary + "  glossary-file: glossary/*.yaml\n",
            ["glossary/terms.yaml"] = "Process Control: A made-up control loop.\n",
            ["doc.md"] = document
        });

        // Assert
        Assert.NotEqual(0, without.ExitCode);
        Assert.Contains("STE100-DICT", without.Output);
        Assert.Equal(0, with.ExitCode);
        Assert.DoesNotContain("STE100-DICT", with.Output);
    }

    /// <summary>
    ///     Test that a deprecated-terms file reports the replacement.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintWithDeprecatedFile_ReportsUseSuggestion()
    {
        // Act
        var result = RunWithFiles(new()
        {
            [".ste100mark.yaml"] = "dictionary:\n  use-embedded: false\n  deprecated-file: deprecated.yaml\n",
            ["deprecated.yaml"] = "Product A:\n  use: Product B\n",
            ["doc.md"] = "# Title\n\nOpen Product A now.\n"
        });

        // Assert
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("use 'Product B' instead", result.Output);
    }

    /// <summary>
    ///     Test that conflicting glossary files fail the run and name the term and both paths.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintWithConflictingGlossaryFiles_ReturnsNonZeroNamingTermAndPaths()
    {
        // Act
        var result = RunWithFiles(new()
        {
            [".ste100mark.yaml"] = "dictionary:\n  glossary-file: g/*.yaml\n",
            ["g/a.yaml"] = "Zorbix: First meaning.\n",
            ["g/b.yaml"] = "Zorbix: Second meaning.\n",
            ["doc.md"] = "# Title\n\nOpen the panel.\n"
        });

        // Assert
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("'Zorbix'", result.Output);
        Assert.Contains("a.yaml", result.Output);
        Assert.Contains("b.yaml", result.Output);
    }

    /// <summary>
    ///     Test that a glossary glob matching nothing fails the run.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintWithEmptyGlossaryGlob_ReturnsNonZero()
    {
        // Act
        var result = RunWithFiles(new()
        {
            [".ste100mark.yaml"] = "dictionary:\n  glossary-file: nothing/*.yaml\n",
            ["doc.md"] = "# Title\n\nOpen the panel.\n"
        });

        // Assert
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("matched no files", result.Output);
    }

    /// <summary>
    ///     Test that an absolute glossary path outside the working directory works.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintWithGlossaryAbsolutePath_Works()
    {
        // Arrange: a glossary in a separate directory referenced by absolute path
        var shared = Directory.CreateTempSubdirectory("ste100mark-shared-glossary-").FullName;
        try
        {
            var glossaryPath = Path.Combine(shared, "terms.yaml").Replace('\\', '/');
            File.WriteAllText(Path.Combine(shared, "terms.yaml"), "Zorbix: A made-up term.\n");

            // Act
            var result = RunWithFiles(new()
            {
                [".ste100mark.yaml"] = $"dictionary:\n  glossary-file: '{glossaryPath}'\n",
                ["doc.md"] = "# Title\n\nOpen the Zorbix panel.\n"
            });

            // Assert
            Assert.Equal(0, result.ExitCode);
        }
        finally
        {
            Directory.Delete(shared, true);
        }
    }

    /// <summary>
    ///     Test that the default text output, default JSON output and rule listing of a fixed
    ///     generic scenario stay byte-identical to reference outputs captured before any later
    ///     output-format work.
    /// </summary>
    [Fact]
    public void Ste100Mark_LintDefaultOutputs_Unchanged()
    {
        // Arrange: a dictionary entry, a document with a heading, a table, a structural finding
        // and word-form dictionary findings, and the literal reference outputs
        const string expectedText = "doc.md:3: [ERROR] STE100-8.1 \u2014 Semicolons are not permitted in ASD-STE100 prose. (Suggestion: Split into two separate sentences.)\n" +
                                    "doc.md:3: [ERROR] STE100-DICT \u2014 Avoid 'performed' (a form of 'perform'); use 'started' instead. (Suggestion: started)\n" +
                                    "doc.md:7: [ERROR] STE100-DICT \u2014 Avoid 'performs' (a form of 'perform'); use 'starts' instead. (Suggestion: starts)\n" +
                                    "Checked 1 file(s): 3 error(s), 0 warning(s).\n" +
                                    "Ste100Mark found linting issues (3 error(s)).";
        const string expectedJson = """
            {
              "filesChecked": 1,
              "noFilesMatched": false,
              "errorCount": 3,
              "warningCount": 0,
              "files": [
                {
                  "path": "doc.md",
                  "status": "checked"
                }
              ],
              "diagnostics": [
                {
                  "file": "doc.md",
                  "line": 3,
                  "column": null,
                  "ruleCode": "STE100-8.1",
                  "severity": "error",
                  "message": "Semicolons are not permitted in ASD-STE100 prose.",
                  "suggestion": "Split into two separate sentences.",
                  "citations": null
                },
                {
                  "file": "doc.md",
                  "line": 3,
                  "column": null,
                  "ruleCode": "STE100-DICT",
                  "severity": "error",
                  "message": "Avoid \u0027performed\u0027 (a form of \u0027perform\u0027); use \u0027started\u0027 instead.",
                  "suggestion": "started",
                  "citations": [
                    {
                      "term": "start",
                      "pos": "verb"
                    }
                  ]
                },
                {
                  "file": "doc.md",
                  "line": 7,
                  "column": null,
                  "ruleCode": "STE100-DICT",
                  "severity": "error",
                  "message": "Avoid \u0027performs\u0027 (a form of \u0027perform\u0027); use \u0027starts\u0027 instead.",
                  "suggestion": "starts",
                  "citations": [
                    {
                      "term": "start",
                      "pos": "verb"
                    }
                  ]
                }
              ]
            }
            """;
        const string expectedRules = """
            [
              {
                "code": "STE100-4.1",
                "title": "Sentence word-count limit (Rules 4.1, 8.4-8.7).",
                "classification": "official",
                "suggestionKind": "advice",
                "modes": [
                  "procedure",
                  "descriptive"
                ]
              },
              {
                "code": "STE100-8.1",
                "title": "No semicolons (Rule 8.1).",
                "classification": "official",
                "suggestionKind": "advice",
                "modes": [
                  "procedure",
                  "descriptive"
                ]
              },
              {
                "code": "STE100-4.2",
                "title": "No contractions (Rule 4.2).",
                "classification": "official",
                "suggestionKind": "advice",
                "modes": [
                  "procedure",
                  "descriptive"
                ]
              },
              {
                "code": "STE100-DICT",
                "title": "Dictionary/vocabulary enforcement against the effective (default or configured) dictionary.",
                "classification": "mechanical",
                "suggestionKind": "citationForm",
                "modes": [
                  "procedure",
                  "descriptive"
                ]
              },
              {
                "code": "STE100-ADV-PARA",
                "title": "Paragraph sentence-count cap (advisory heuristic, not an official STE100 rule).",
                "classification": "advisory",
                "suggestionKind": "advice",
                "modes": [
                  "procedure",
                  "descriptive"
                ]
              },
              {
                "code": "STE100-ADV-PASSIVE",
                "title": "Passive-voice detection (advisory heuristic, not an official STE100 rule).",
                "classification": "advisory",
                "suggestionKind": "advice",
                "modes": [
                  "procedure",
                  "descriptive"
                ]
              },
              {
                "code": "STE100-ADV-COMPLEXVERB",
                "title": "Perfect/modal-perfect tense detection (advisory heuristic, not an official STE100 rule).",
                "classification": "advisory",
                "suggestionKind": "advice",
                "modes": [
                  "procedure",
                  "descriptive"
                ]
              },
              {
                "code": "STE100-ADV-INGFORM",
                "title": "-ing form detection (advisory heuristic, not an official STE100 rule).",
                "classification": "advisory",
                "suggestionKind": "advice",
                "modes": [
                  "procedure",
                  "descriptive"
                ]
              }
            ]
            """;
        var workingDirectory = Directory.CreateTempSubdirectory("ste100mark-integration-").FullName;
        try
        {
            File.WriteAllText(
                Path.Combine(workingDirectory, ".ste100mark.yaml"),
                "dictionary:\n  use-embedded: false\n  disallow:\n    perform:\n      - pos: verb\n        alternatives: [start]\n");
            File.WriteAllText(
                Path.Combine(workingDirectory, "doc.md"),
                "# Report Overview\n\nThe crew performed the task; the valve opened.\n\n| Item | Note |\n| --- | --- |\n| Valve | The crew performs the check. |\n");

            // Act: run the default text, default JSON and rule listing outputs
            Runner.RunInDirectory(out var textOutput, workingDirectory, "dotnet", _dllPath, "doc.md");
            Runner.RunInDirectory(out var jsonOutput, workingDirectory, "dotnet", _dllPath, "doc.md", "--format", "json");
            Runner.RunInDirectory(out var rulesOutput, workingDirectory, "dotnet", _dllPath, "--list-rules");

            // Assert: each output equals its golden (banner lines and line endings normalized)
            // The em dash separator may be encoded as a hyphen when output is redirected
            var textLines = textOutput.Replace("\u2014", "-").Replace("\r\n", "\n").Split('\n')
                .Where(line => line.Length > 0 &&
                               !line.StartsWith("Ste100Mark version ", StringComparison.Ordinal) &&
                               !line.StartsWith("Copyright (c)", StringComparison.Ordinal));
            Assert.Equal(expectedText.Replace("\u2014", "-"), string.Join("\n", textLines));
            Assert.Equal(expectedJson.Replace("\r\n", "\n"), jsonOutput.Replace("\r\n", "\n").TrimEnd());
            Assert.Equal(expectedRules.Replace("\r\n", "\n"), rulesOutput.Replace("\r\n", "\n").TrimEnd());
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
        }
    }
}
