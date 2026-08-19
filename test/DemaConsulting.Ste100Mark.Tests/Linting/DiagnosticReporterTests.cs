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
///     Unit tests for the DiagnosticReporter class.
/// </summary>
[Collection("Sequential")]
public class DiagnosticReporterTests
{
    /// <summary>
    ///     Sample diagnostic list reused across tests.
    /// </summary>
    private static readonly IReadOnlyList<Diagnostic> SampleDiagnostics =
    [
        new Diagnostic("docs/sample.md", 3, null, "STE100-8.1", Severity.Error, "Semicolons are not permitted.", "Split into two sentences."),
        new Diagnostic("docs/sample.md", 5, null, "STE100-ADV-PASSIVE", Severity.Warn, "Possible passive voice.", null)
    ];

    /// <summary>
    ///     Test that text-format reporting writes one line per diagnostic plus a summary line.
    /// </summary>
    [Fact]
    public void Report_TextFormat_WritesDiagnosticLinesAndSummary()
    {
        // Arrange: capture console output for a text-format context
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create([]);

            // Act: execute the operation being tested
            DiagnosticReporter.Report(context, SampleDiagnostics, 1);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("docs/sample.md:3", output);
            Assert.Contains("[ERROR]", output);
            Assert.Contains("STE100-8.1", output);
            Assert.Contains("Split into two sentences.", output);
            Assert.Contains("[WARN]", output);
            Assert.Contains("Checked 1 file(s): 1 error(s), 1 warning(s).", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that JSON-format reporting writes a single parseable JSON document with the expected
    ///     stable schema fields.
    /// </summary>
    [Fact]
    public void Report_JsonFormat_WritesSingleJsonDocumentWithExpectedSchema()
    {
        // Arrange: capture console output for a JSON-format context
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--format", "json"]);

            // Act: execute the operation being tested
            DiagnosticReporter.Report(context, SampleDiagnostics, 1);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("\"filesChecked\": 1", output);
            Assert.Contains("\"errorCount\": 1", output);
            Assert.Contains("\"warningCount\": 1", output);
            Assert.Contains("\"ruleCode\": \"STE100-8.1\"", output);
            Assert.Contains("\"severity\": \"error\"", output);
            Assert.Contains("\"severity\": \"warn\"", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that reporting an empty diagnostic list still writes a valid zero-count summary.
    /// </summary>
    [Fact]
    public void Report_NoDiagnostics_WritesZeroCountSummary()
    {
        // Arrange: capture console output for a text-format context with no diagnostics
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create([]);

            // Act: execute the operation being tested
            DiagnosticReporter.Report(context, [], 2);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Checked 2 file(s): 0 error(s), 0 warning(s).", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
