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
///     Identifies the part-of-speech engine selected via the <c>--pos-engine</c> command-line flag or
///     the <c>dictionary.pos-engine</c> configuration key.
/// </summary>
/// <remarks>
///     Kept as a small, closed set so that engine creation can switch on it exhaustively.
/// </remarks>
internal enum PartOfSpeechEngine
{
    /// <summary>
    ///     The offline, in-memory <c>basic</c> engine; nothing is downloaded.
    /// </summary>
    Basic,

    /// <summary>
    ///     The <c>minilm</c> encoder engine (the default); the model is downloaded on first use.
    /// </summary>
    MiniLm,

    /// <summary>
    ///     The <c>bert</c> (BERT base cased) encoder engine; the model is downloaded on first use.
    /// </summary>
    Bert
}

/// <summary>
///     Parsing helpers for <see cref="PartOfSpeechEngine"/>.
/// </summary>
internal static class PartOfSpeechEngineNames
{
    /// <summary>The engine used when neither the command line nor the configuration selects one.</summary>
    internal const PartOfSpeechEngine Default = PartOfSpeechEngine.MiniLm;

    /// <summary>The accepted values, for use in error messages.</summary>
    internal const string AcceptedValues = "basic, minilm or bert";

    /// <summary>
    ///     Parses an engine name (case-insensitive: <c>basic</c>, <c>minilm</c>, <c>bert</c>).
    /// </summary>
    /// <param name="value">The text to parse.</param>
    /// <param name="engine">The parsed engine when parsing succeeds.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> names an engine.</returns>
    internal static bool TryParse(string? value, out PartOfSpeechEngine engine)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "basic":
                engine = PartOfSpeechEngine.Basic;
                return true;
            case "minilm":
                engine = PartOfSpeechEngine.MiniLm;
                return true;
            case "bert":
                engine = PartOfSpeechEngine.Bert;
                return true;
            default:
                engine = Default;
                return false;
        }
    }
}
