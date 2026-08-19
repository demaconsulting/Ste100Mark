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
///     Identifies how strongly a lint finding should be treated.
/// </summary>
/// <remarks>
///     Three levels (rather than a plain boolean) are required because ASD-STE100 Issue 9 mixes
///     mandatory rules (which must always fail a build) with advisory heuristics that a project may
///     want to see but not enforce. <see cref="Off"/> lets a project silence an advisory check
///     entirely; <see cref="Warn"/> reports a finding without affecting the exit code (unless
///     <c>--strict</c> is supplied); <see cref="Error"/> always affects the exit code.
/// </remarks>
internal enum Severity
{
    /// <summary>
    ///     The check is disabled; no diagnostics are produced for it.
    /// </summary>
    Off,

    /// <summary>
    ///     Findings are reported but do not cause a non-zero exit code unless <c>--strict</c> is
    ///     supplied on the command line.
    /// </summary>
    Warn,

    /// <summary>
    ///     Findings are reported and always cause a non-zero exit code.
    /// </summary>
    Error
}
