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

using System.Runtime.InteropServices;
using DemaConsulting.Ste100Mark.Cli;
using DemaConsulting.Ste100Mark.Utilities;
using DemaConsulting.TestResults.IO;

namespace DemaConsulting.Ste100Mark.SelfTest;

/// <summary>
///     Provides self-validation functionality for the Ste100Mark.
/// </summary>
internal static class Validation
{
    /// <summary>
    ///     Runs self-validation tests and optionally writes results to a file.
    /// </summary>
    /// <param name="context">The context containing command line arguments and program state.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <remarks>
    ///     If any self-test fails, <c>context.WriteError</c> is called for each failure, which sets
    ///     <c>context.ExitCode</c> to 1 as a side-effect. If a results file is requested and its
    ///     extension is unsupported, <c>context.WriteError</c> is also called, resulting in a
    ///     non-zero exit code.
    /// </remarks>
    public static void Run(Context context)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(context);

        // Print validation header
        PrintValidationHeader(context);

        // Create test results collection
        var testResults = new DemaConsulting.TestResults.TestResults
        {
            Name = "Ste100Mark Self-Validation"
        };

        // Run core functionality tests
        RunVersionTest(context, testResults);
        RunHelpTest(context, testResults);
        RunLintCleanFileTest(context, testResults);
        RunLintViolationFileTest(context, testResults);
        RunLintJsonOutputTest(context, testResults);

        // Calculate totals
        var totalTests = testResults.Results.Count;
        var passedTests = testResults.Results.Count(t => t.Outcome == DemaConsulting.TestResults.TestOutcome.Passed);
        var failedTests = testResults.Results.Count(t => t.Outcome == DemaConsulting.TestResults.TestOutcome.Failed);

        // Print summary
        context.WriteLine("");
        context.WriteLine($"Total Tests: {totalTests}");
        context.WriteLine($"Passed: {passedTests}");
        if (failedTests > 0)
        {
            context.WriteError($"Failed: {failedTests}");
        }
        else
        {
            context.WriteLine($"Failed: {failedTests}");
        }

        // Write results file if requested
        if (context.ResultsFile != null)
        {
            WriteResultsFile(context, testResults);
        }
    }

    /// <summary>
    ///     Prints the validation header with system information.
    /// </summary>
    /// <param name="context">The context for output.</param>
    private static void PrintValidationHeader(Context context)
    {
        var heading = new string('#', context.HeadingDepth);
        context.WriteLine($"{heading} DEMA Consulting Ste100Mark");
        context.WriteLine("");
        context.WriteLine("| Information         | Value                                              |");
        context.WriteLine("| :------------------ | :------------------------------------------------- |");
        context.WriteLine($"| Tool Version        | {Program.Version,-50} |");
        context.WriteLine($"| Machine Name        | {Environment.MachineName,-50} |");
        context.WriteLine($"| OS Version          | {RuntimeInformation.OSDescription,-50} |");
        context.WriteLine($"| DotNet Runtime      | {RuntimeInformation.FrameworkDescription,-50} |");
        context.WriteLine($"| Time Stamp          | {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC{"",-29} |");
        context.WriteLine("");
    }

    /// <summary>
    ///     Runs a test for version display functionality.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunVersionTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult("Ste100Mark_VersionDisplay");

        try
        {
            using var tempDir = new TemporaryDirectory();
            var logFile = PathHelpers.SafePathCombine(tempDir.DirectoryPath, "version-test.log");

            // Build command line arguments
            var args = new List<string>
            {
                "--silent",
                "--log", logFile,
                "--version"
            };

            // Run the program
            int exitCode;
            using (var testContext = Context.Create([.. args]))
            {
                Program.Run(testContext);
                exitCode = testContext.ExitCode;
            }

            // Check if execution succeeded
            if (exitCode == 0)
            {
                // Read log content
                var logContent = File.ReadAllText(logFile);

                // Verify version string is in log (version contains dots like 0.0.0)
                var versionPattern = new System.Text.RegularExpressions.Regex(
                    @"\b\d+\.\d+\.\d+",
                    System.Text.RegularExpressions.RegexOptions.None,
                    TimeSpan.FromSeconds(1));
                if (!string.IsNullOrWhiteSpace(logContent) &&
                    versionPattern.IsMatch(logContent))
                {
                    test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                    context.WriteLine($"✓ Ste100Mark_VersionDisplay - Passed");
                }
                else
                {
                    test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                    test.ErrorMessage = "Version string not found in log";
                    context.WriteError($"✗ Ste100Mark_VersionDisplay - Failed: Version string not found in log");
                }
            }
            else
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = $"Program exited with code {exitCode}";
                context.WriteError($"✗ Ste100Mark_VersionDisplay - Failed: Exit code {exitCode}");
            }
        }
        // Generic catch is justified here as this is a test framework - any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, "Ste100Mark_VersionDisplay", ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }

    /// <summary>
    ///     Runs a test for help display functionality.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunHelpTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult("Ste100Mark_HelpDisplay");

        try
        {
            using var tempDir = new TemporaryDirectory();
            var logFile = PathHelpers.SafePathCombine(tempDir.DirectoryPath, "help-test.log");

            // Build command line arguments
            var args = new List<string>
            {
                "--silent",
                "--log", logFile,
                "--help"
            };

            // Run the program
            int exitCode;
            using (var testContext = Context.Create([.. args]))
            {
                Program.Run(testContext);
                exitCode = testContext.ExitCode;
            }

            // Check if execution succeeded
            if (exitCode == 0)
            {
                // Read log content
                var logContent = File.ReadAllText(logFile);

                // Verify help text is in log
                if (logContent.Contains("Usage:") && logContent.Contains("Options:"))
                {
                    test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                    context.WriteLine($"✓ Ste100Mark_HelpDisplay - Passed");
                }
                else
                {
                    test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                    test.ErrorMessage = "Help text not found in log";
                    context.WriteError($"✗ Ste100Mark_HelpDisplay - Failed: Help text not found in log");
                }
            }
            else
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = $"Program exited with code {exitCode}";
                context.WriteError($"✗ Ste100Mark_HelpDisplay - Failed: Exit code {exitCode}");
            }
        }
        // Generic catch is justified here as this is a test framework - any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, "Ste100Mark_HelpDisplay", ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }

    /// <summary>
    ///     Runs a test asserting a clean Markdown file produces no diagnostics when linted.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunLintCleanFileTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult("Ste100Mark_LintCleanFileNoDiagnostics");

        try
        {
            using var tempDir = new TemporaryDirectory();
            var mdFile = PathHelpers.SafePathCombine(tempDir.DirectoryPath, "clean.md");
            var logFile = PathHelpers.SafePathCombine(tempDir.DirectoryPath, "clean-test.log");

            File.WriteAllText(mdFile, "# Clean Document\n\nThis is a short sentence.\n");

            // Build command line arguments (relative filename; CWD is temporarily switched to the
            // temp directory below since Ste100Mark resolves globs relative to the current
            // directory)
            var args = new List<string>
            {
                "clean.md",
                "--silent",
                "--log", logFile
            };

            // Run the program with the current directory switched to the temp directory
            int exitCode;
            var originalDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(tempDir.DirectoryPath);
            try
            {
                using var testContext = Context.Create([.. args]);
                Program.Run(testContext);
                exitCode = testContext.ExitCode;
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
            }

            // Check if execution succeeded
            if (exitCode == 0)
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                context.WriteLine($"✓ Ste100Mark_LintCleanFileNoDiagnostics - Passed");
            }
            else
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = $"Program exited with code {exitCode}";
                context.WriteError($"✗ Ste100Mark_LintCleanFileNoDiagnostics - Failed: Exit code {exitCode}");
            }
        }
        // Generic catch is justified here as this is a test framework - any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, "Ste100Mark_LintCleanFileNoDiagnostics", ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }

    /// <summary>
    ///     Runs a test asserting a Markdown file with multiple violations produces diagnostics for
    ///     each expected rule code and a non-zero exit code.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunLintViolationFileTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult("Ste100Mark_LintViolationFileDetectsIssues");

        try
        {
            using var tempDir = new TemporaryDirectory();
            var mdFile = PathHelpers.SafePathCombine(tempDir.DirectoryPath, "violations.md");
            var logFile = PathHelpers.SafePathCombine(tempDir.DirectoryPath, "violations-test.log");

            var longSentence = string.Join(' ', Enumerable.Repeat("word", 26)) + ".";
            File.WriteAllText(
                mdFile,
                "# Violations Document\n\n" +
                $"{longSentence}\n\n" +
                "Open the panel; then close it.\n\n" +
                "We don't allow this.\n\n" +
                "Please utilize the tool.\n");

            // Build command line arguments (relative filename; CWD is temporarily switched to the
            // temp directory below since Ste100Mark resolves globs relative to the current
            // directory)
            var args = new List<string>
            {
                "violations.md",
                "--silent",
                "--log", logFile
            };

            // Run the program with the current directory switched to the temp directory
            int exitCode;
            var originalDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(tempDir.DirectoryPath);
            try
            {
                using var testContext = Context.Create([.. args]);
                Program.Run(testContext);
                exitCode = testContext.ExitCode;
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
            }
            var logContent = File.Exists(logFile) ? File.ReadAllText(logFile) : string.Empty;
            var expectedCodes = new[] { "STE100-4.1", "STE100-8.1", "STE100-4.2", "STE100-DICT" };
            var missingCodes = expectedCodes.Where(code => !logContent.Contains(code)).ToList();

            if (exitCode != 0 && missingCodes.Count == 0)
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                context.WriteLine($"✓ Ste100Mark_LintViolationFileDetectsIssues - Passed");
            }
            else
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = exitCode == 0
                    ? "Program exited with code 0; expected a non-zero exit code for violations"
                    : $"Missing expected rule codes in log: {string.Join(", ", missingCodes)}";
                context.WriteError($"✗ Ste100Mark_LintViolationFileDetectsIssues - Failed: {test.ErrorMessage}");
            }
        }
        // Generic catch is justified here as this is a test framework - any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, "Ste100Mark_LintViolationFileDetectsIssues", ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }

    /// <summary>
    ///     Runs a test asserting that <c>--format json</c> output is valid, parseable JSON.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunLintJsonOutputTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult("Ste100Mark_LintJsonOutputIsValidJson");

        try
        {
            using var tempDir = new TemporaryDirectory();
            var mdFile = PathHelpers.SafePathCombine(tempDir.DirectoryPath, "json-output.md");
            var logFile = PathHelpers.SafePathCombine(tempDir.DirectoryPath, "json-output-test.log");

            File.WriteAllText(mdFile, "# JSON Output Document\n\nPlease utilize the tool.\n");

            // Build command line arguments (relative filename; CWD is temporarily switched to the
            // temp directory below since Ste100Mark resolves globs relative to the current
            // directory)
            var args = new List<string>
            {
                "json-output.md",
                "--silent",
                "--log", logFile,
                "--format", "json"
            };

            // Run the program with the current directory switched to the temp directory
            var originalDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(tempDir.DirectoryPath);
            try
            {
                using var testContext = Context.Create([.. args]);
                Program.Run(testContext);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
            }

            // Read log content and confirm it parses as JSON
            var logContent = File.ReadAllText(logFile);
            using var _ = System.Text.Json.JsonDocument.Parse(logContent);

            test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
            context.WriteLine($"✓ Ste100Mark_LintJsonOutputIsValidJson - Passed");
        }
        // Generic catch is justified here as this is a test framework - any exception (including
        // JsonException on parse failure) should be recorded as a test failure to ensure robust
        // test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, "Ste100Mark_LintJsonOutputIsValidJson", ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }

    /// <summary>
    ///     Writes test results to a file in TRX or JUnit format.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results to write.</param>
    private static void WriteResultsFile(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        if (context.ResultsFile == null)
        {
            return;
        }

        try
        {
            var extension = Path.GetExtension(context.ResultsFile).ToLowerInvariant();
            string content;

            if (extension == ".trx")
            {
                content = TrxSerializer.Serialize(testResults);
            }
            else if (extension == ".xml")
            {
                // Assume JUnit format for .xml extension
                content = JUnitSerializer.Serialize(testResults);
            }
            else
            {
                context.WriteError($"Error: Unsupported results file format '{extension}'. Use .trx or .xml extension.");
                return;
            }

            File.WriteAllText(context.ResultsFile, content);
            context.WriteLine($"Results written to {context.ResultsFile}");
        }
        // Generic catch is justified here as a top-level handler to log file write errors
        catch (Exception ex)
        {
            context.WriteError($"Error: Failed to write results file: {ex.Message}");
        }
    }

    /// <summary>
    ///     Creates a new test result object with common properties.
    /// </summary>
    /// <param name="testName">The name of the test.</param>
    /// <returns>A new test result object.</returns>
    private static DemaConsulting.TestResults.TestResult CreateTestResult(string testName)
    {
        return new DemaConsulting.TestResults.TestResult
        {
            Name = testName,
            ClassName = "Validation",
            CodeBase = "Ste100Mark"
        };
    }

    /// <summary>
    ///     Finalizes a test result by setting its duration and adding it to the collection.
    /// </summary>
    /// <param name="test">The test result to finalize.</param>
    /// <param name="startTime">The start time of the test.</param>
    /// <param name="testResults">The test results collection to add to.</param>
    private static void FinalizeTestResult(
        DemaConsulting.TestResults.TestResult test,
        DateTime startTime,
        DemaConsulting.TestResults.TestResults testResults)
    {
        test.Duration = DateTime.UtcNow - startTime;
        testResults.Results.Add(test);
    }

    /// <summary>
    ///     Handles test exceptions by setting failure information and logging the error.
    /// </summary>
    /// <param name="test">The test result to update.</param>
    /// <param name="context">The context for output.</param>
    /// <param name="testName">The name of the test for error messages.</param>
    /// <param name="ex">The exception that occurred.</param>
    private static void HandleTestException(
        DemaConsulting.TestResults.TestResult test,
        Context context,
        string testName,
        Exception ex)
    {
        test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
        test.ErrorMessage = $"Exception: {ex.Message}";
        context.WriteError($"✗ {testName} - FAILED: {ex.Message}");
    }

    /// <summary>
    ///     Represents a temporary directory that is automatically deleted when disposed.
    /// </summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        /// <summary>
        ///     Gets the path to the temporary directory.
        /// </summary>
        public string DirectoryPath { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TemporaryDirectory"/> class.
        /// </summary>
        public TemporaryDirectory()
        {
            DirectoryPath = PathHelpers.SafePathCombine(Path.GetTempPath(), $"ste100mark_validation_{Guid.NewGuid()}");

            try
            {
                Directory.CreateDirectory(DirectoryPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new InvalidOperationException($"Failed to create temporary directory: {ex.Message}", ex);
            }
        }

        /// <summary>
        ///     Deletes the temporary directory and all its contents.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Ignore cleanup errors during disposal
            }
        }
    }
}
