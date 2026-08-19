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

namespace DemaConsulting.Ste100Mark.Linting;

/// <summary>
///     Represents a single lint finding produced by a structural rule or the dictionary check.
/// </summary>
/// <remarks>
///     A plain, immutable record is used because diagnostics flow one-way from the rule engines
///     (<see cref="StructuralRules"/>, <see cref="DictionaryChecker"/>) through aggregation in
///     <see cref="Linter"/> to <see cref="DiagnosticReporter"/>; nothing downstream ever mutates a
///     diagnostic once produced, and value-based equality simplifies unit test assertions.
/// </remarks>
/// <param name="File">
///     Path to the Markdown file the finding relates to, exactly as it was resolved by the
///     <see cref="Linter"/> (relative when the input glob was relative, absolute otherwise).
/// </param>
/// <param name="Line">1-based source line number where the finding begins.</param>
/// <param name="Column">
///     1-based source column number where the finding begins, or <see langword="null"/> when the
///     rule that produced the finding does not track column-level position.
/// </param>
/// <param name="RuleCode">
///     Stable identifier for the rule that produced the finding (for example <c>STE100-4.1</c> for
///     the sentence word-count rule, or <c>STE100-DICT</c> for the dictionary check). See
///     <see cref="StructuralRules"/> and <see cref="DictionaryChecker"/> for the full list of codes.
/// </param>
/// <param name="Severity">Effective severity of the finding, as resolved from configuration.</param>
/// <param name="Message">Human-readable description of the violation.</param>
/// <param name="Suggestion">
///     Optional suggested fix (for example, an alternative word from the dictionary), or
///     <see langword="null"/> when the rule has no specific suggestion to offer.
/// </param>
internal sealed record Diagnostic(
    string File,
    int Line,
    int? Column,
    string RuleCode,
    Severity Severity,
    string Message,
    string? Suggestion);
