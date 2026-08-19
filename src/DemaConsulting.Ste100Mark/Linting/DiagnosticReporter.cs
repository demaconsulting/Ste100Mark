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
using System.Text.Json.Serialization;
using DemaConsulting.Ste100Mark.Cli;

namespace DemaConsulting.Ste100Mark.Linting;

/// <summary>
///     Formats aggregated <see cref="Diagnostic"/> results as either human-readable text or a
///     stable JSON document, and writes the result through the shared <see cref="Context"/> output
///     channel.
/// </summary>
/// <remarks>
///     The JSON writer buffers the entire document and writes it in a single
///     <see cref="Context.WriteLine"/> call so that stdout remains one parseable JSON value even
///     when <see cref="Context.Silent"/> is not set; callers that also need to fail the build must
///     use <see cref="Context.MarkFailure"/> rather than <see cref="Context.WriteError"/> in JSON
///     mode, to avoid interleaving a stderr line with the JSON document when both streams are
///     captured together (for example, by CI log collectors).
/// </remarks>
internal static partial class DiagnosticReporter
{
    /// <summary>
    ///     Writes the diagnostic report for a lint run in the format selected by
    ///     <see cref="Context.Format"/>.
    /// </summary>
    /// <param name="context">Output target and format selector.</param>
    /// <param name="diagnostics">Diagnostics collected across all linted files.</param>
    /// <param name="filesChecked">Number of files that were linted.</param>
    public static void Report(Context context, IReadOnlyList<Diagnostic> diagnostics, int filesChecked)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (context.Format == OutputFormat.Json)
        {
            WriteJson(context, diagnostics, filesChecked);
        }
        else
        {
            WriteText(context, diagnostics, filesChecked);
        }
    }

    /// <summary>
    ///     Writes one human-readable line per diagnostic, followed by a summary line.
    /// </summary>
    /// <param name="context">Output target.</param>
    /// <param name="diagnostics">Diagnostics to report.</param>
    /// <param name="filesChecked">Number of files that were linted.</param>
    private static void WriteText(Context context, IReadOnlyList<Diagnostic> diagnostics, int filesChecked)
    {
        foreach (var diagnostic in diagnostics)
        {
            var location = diagnostic.Column is { } column
                ? $"{diagnostic.File}:{diagnostic.Line}:{column}"
                : $"{diagnostic.File}:{diagnostic.Line}";

            var severityText = diagnostic.Severity.ToString().ToUpperInvariant();
            var suggestion = diagnostic.Suggestion is null ? string.Empty : $" (Suggestion: {diagnostic.Suggestion})";

            context.WriteLine($"{location}: [{severityText}] {diagnostic.RuleCode} \u2014 {diagnostic.Message}{suggestion}");
        }

        var errorCount = diagnostics.Count(d => d.Severity == Severity.Error);
        var warningCount = diagnostics.Count(d => d.Severity == Severity.Warn);
        context.WriteLine($"Checked {filesChecked} file(s): {errorCount} error(s), {warningCount} warning(s).");
    }

    /// <summary>
    ///     Writes the diagnostics as a single, stable-schema JSON document.
    /// </summary>
    /// <param name="context">Output target.</param>
    /// <param name="diagnostics">Diagnostics to report.</param>
    /// <param name="filesChecked">Number of files that were linted.</param>
    private static void WriteJson(Context context, IReadOnlyList<Diagnostic> diagnostics, int filesChecked)
    {
        var document = new JsonReport(
            filesChecked,
            diagnostics.Count(d => d.Severity == Severity.Error),
            diagnostics.Count(d => d.Severity == Severity.Warn),
            diagnostics
                .Select(d => new JsonDiagnostic(
                    d.File,
                    d.Line,
                    d.Column,
                    d.RuleCode,
                    d.Severity.ToString().ToLowerInvariant(),
                    d.Message,
                    d.Suggestion))
                .ToList());

        var json = JsonSerializer.Serialize(document, JsonReportContext.Default.JsonReport);
        context.WriteLine(json);
    }

    /// <summary>
    ///     Stable JSON schema root: overall summary counts plus the full diagnostic list.
    /// </summary>
    /// <param name="FilesChecked">Number of files that were linted.</param>
    /// <param name="ErrorCount">Number of <see cref="Severity.Error"/>-severity diagnostics.</param>
    /// <param name="WarningCount">Number of <see cref="Severity.Warn"/>-severity diagnostics.</param>
    /// <param name="Diagnostics">All diagnostics, in the order they were produced.</param>
    private sealed record JsonReport(
        int FilesChecked,
        int ErrorCount,
        int WarningCount,
        IReadOnlyList<JsonDiagnostic> Diagnostics);

    /// <summary>
    ///     Stable JSON schema for a single diagnostic entry.
    /// </summary>
    /// <param name="File">Path to the Markdown file the finding relates to.</param>
    /// <param name="Line">1-based source line number.</param>
    /// <param name="Column">1-based source column number, or <see langword="null"/>.</param>
    /// <param name="RuleCode">Stable rule identifier, for example <c>STE100-4.1</c>.</param>
    /// <param name="Severity">Lowercase severity string: <c>"error"</c>, <c>"warn"</c>, or <c>"off"</c>.</param>
    /// <param name="Message">Human-readable description of the violation.</param>
    /// <param name="Suggestion">Suggested fix, or <see langword="null"/>.</param>
    private sealed record JsonDiagnostic(
        string File,
        int Line,
        int? Column,
        string RuleCode,
        string Severity,
        string Message,
        string? Suggestion);

    /// <summary>
    ///     Source-generated JSON serialization context, required for reflection-free, trimming- and
    ///     AOT-safe <see cref="JsonSerializer"/> usage.
    /// </summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
    [JsonSerializable(typeof(JsonReport))]
    private sealed partial class JsonReportContext : JsonSerializerContext;
}
