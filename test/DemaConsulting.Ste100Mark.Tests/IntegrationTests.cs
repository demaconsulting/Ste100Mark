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
}

