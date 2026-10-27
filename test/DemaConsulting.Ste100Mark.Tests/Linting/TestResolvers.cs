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

using DemaConsulting.Ste100Mark.Linting;
using DemaConsulting.WordSense;

namespace DemaConsulting.Ste100Mark.Tests.Linting;

/// <summary>
///     Shared offline part-of-speech resolvers for tests.
/// </summary>
internal static class TestResolvers
{
    /// <summary>
    ///     A process-wide resolver over the offline basic model (no download, thread-safe); never disposed.
    /// </summary>
    public static readonly PartOfSpeechResolver Basic = new(new BasicWordSenseModel());
}

/// <summary>
///     Test-side wrapper over <c>Linter.Run</c> that supplies the offline basic model for every engine
///     unless a factory is given, so linter unit tests never download a model.
/// </summary>
internal static class TestLinter
{
    /// <summary>Runs the linter with the offline basic model for every engine.</summary>
    /// <param name="context">The context.</param>
    public static void Run(Ste100Mark.Cli.Context context) =>
        Ste100Mark.Linting.Linter.Run(context, _ => new BasicWordSenseModel());

    /// <summary>Runs the linter with the given model factory.</summary>
    /// <param name="context">The context.</param>
    /// <param name="modelFactory">The model factory.</param>
    public static void Run(
        Ste100Mark.Cli.Context context,
        Func<Ste100Mark.Cli.PartOfSpeechEngine, IWordSenseModel> modelFactory) =>
        Ste100Mark.Linting.Linter.Run(context, modelFactory);
}

/// <summary>
///     Test-side wrapper over <see cref="DictionaryChecker.Evaluate"/> that defaults to the offline
///     basic resolver, so unit tests never select (or download) an engine implicitly.
/// </summary>
/// <remarks>
///     Production code must always pass a resolver explicitly; this wrapper exists only so that the
///     many checker tests do not repeat the offline default at every call site.
/// </remarks>
internal static class TestDictionaryChecker
{
    /// <summary>
    ///     Evaluates the dictionary check using <paramref name="resolver"/>, or the offline basic
    ///     resolver when it is <see langword="null"/>.
    /// </summary>
    public static IReadOnlyList<Diagnostic> Evaluate(
        string file,
        IReadOnlyList<ProseSegment> segments,
        LintDictionary dictionary,
        IReadOnlyCollection<string>? extraAllowedTerms = null,
        IReadOnlyCollection<string>? allowedPhrases = null,
        IPartOfSpeechResolver? resolver = null) =>
        DictionaryChecker.Evaluate(
            file, segments, dictionary, resolver ?? TestResolvers.Basic, extraAllowedTerms, allowedPhrases);
}
