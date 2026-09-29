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
    /// <param name="noFilesMatched">
    ///     <see langword="true"/> when the file selection matched zero files and the run is being
    ///     treated as a failure (that is, <c>--allow-empty</c> was not specified); <see langword="false"/>
    ///     otherwise, including when the selection matched zero files but was accepted via
    ///     <c>--allow-empty</c>.
    /// </param>
    public static void Report(Context context, IReadOnlyList<Diagnostic> diagnostics, int filesChecked, bool noFilesMatched)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (context.Format == OutputFormat.Json)
        {
            WriteJson(context, diagnostics, filesChecked, noFilesMatched);
        }
        else
        {
            WriteText(context, diagnostics, filesChecked, noFilesMatched);
        }
    }

    /// <summary>
    ///     Writes one human-readable line per diagnostic, followed by a summary line.
    /// </summary>
    /// <param name="context">Output target.</param>
    /// <param name="diagnostics">Diagnostics to report.</param>
    /// <param name="filesChecked">Number of files that were linted.</param>
    /// <param name="noFilesMatched">
    ///     <see langword="true"/> when the file selection matched zero files and the run is being
    ///     treated as a failure; when <see langword="true"/>, the summary line is replaced with an
    ///     unambiguous failure statement instead of the ordinary zero-count summary.
    /// </param>
    private static void WriteText(Context context, IReadOnlyList<Diagnostic> diagnostics, int filesChecked, bool noFilesMatched)
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

        if (noFilesMatched)
        {
            // Deliberately distinct wording from the ordinary summary line below: a bare
            // "Checked 0 file(s): 0 error(s), 0 warning(s)." reads as a clean pass to anyone
            // scanning the output, which is exactly the ambiguity this feature exists to remove.
            context.WriteLine(
                "No files matched the configured file selection; 0 file(s) checked. This run is " +
                "treated as a failure to avoid silently passing on a misconfigured glob or " +
                "include/exclude pattern. Use --allow-empty to accept an intentionally empty file " +
                "selection.");
            return;
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
    /// <param name="noFilesMatched">
    ///     <see langword="true"/> when the file selection matched zero files and the run is being
    ///     treated as a failure; threaded through to the <see cref="JsonReport.NoFilesMatched"/>
    ///     field so a JSON-consuming caller does not need to separately know whether
    ///     <c>--allow-empty</c> was specified to disambiguate <c>filesChecked: 0</c>.
    /// </param>
    private static void WriteJson(Context context, IReadOnlyList<Diagnostic> diagnostics, int filesChecked, bool noFilesMatched)
    {
        var document = new JsonReport(
            filesChecked,
            noFilesMatched,
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
    /// <param name="NoFilesMatched">
    ///     <see langword="true"/> only when the file selection matched zero files and the run was
    ///     treated as a failure (that is, <c>--allow-empty</c> was not specified). This
    ///     disambiguates <paramref name="FilesChecked"/><c> == 0</c>, which by itself cannot
    ///     distinguish a misconfigured glob/include pattern from a genuine clean pass over zero
    ///     files.
    /// </param>
    /// <param name="ErrorCount">Number of <see cref="Severity.Error"/>-severity diagnostics.</param>
    /// <param name="WarningCount">Number of <see cref="Severity.Warn"/>-severity diagnostics.</param>
    /// <param name="Diagnostics">All diagnostics, in the order they were produced.</param>
    private sealed record JsonReport(
        int FilesChecked,
        bool NoFilesMatched,
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
