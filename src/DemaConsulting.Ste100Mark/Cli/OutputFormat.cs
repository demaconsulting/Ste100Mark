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

namespace DemaConsulting.Ste100Mark.Cli;

/// <summary>
///     Identifies the diagnostic output format selected via the <c>--format</c> command-line flag.
/// </summary>
/// <remarks>
///     Kept as a small, closed set so that the diagnostic reporter can switch on it exhaustively;
///     adding a new format requires an explicit update to both the parser and the reporter rather
///     than silently falling back to text.
/// </remarks>
internal enum OutputFormat
{
    /// <summary>
    ///     Human-readable, line-oriented diagnostic output (the default).
    /// </summary>
    Text,

    /// <summary>
    ///     Machine-readable, structured JSON diagnostic output for CI consumption.
    /// </summary>
    Json,

    /// <summary>
    ///     Machine-readable JSON diagnostic output that carries every field of <see cref="Json"/>
    ///     plus per-finding evidence (span, sentence, context lines, match and tagger evidence,
    ///     confidence class, and suppression hints) for agents and other tooling.
    /// </summary>
    JsonDetailed
}

/// <summary>
///     Helpers for <see cref="OutputFormat"/>.
/// </summary>
internal static class OutputFormatExtensions
{
    /// <summary>
    ///     Determines whether a format writes a JSON document to stdout (and so must keep stdout
    ///     free of any other text).
    /// </summary>
    /// <param name="format">The output format.</param>
    /// <returns>
    ///     <see langword="true"/> for <see cref="OutputFormat.Json"/> and
    ///     <see cref="OutputFormat.JsonDetailed"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsJson(this OutputFormat format)
    {
        return format is OutputFormat.Json or OutputFormat.JsonDetailed;
    }
}
