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
///     The single, shared registry of every rule code the linter can emit. <see cref="StructuralRules"/>
///     and <see cref="DictionaryChecker"/> reference these constants when constructing their
///     <see cref="Diagnostic"/> instances, and <see cref="RuleCatalog"/> references the same constants
///     when building its published entries, so a rule code cannot be added to an emitter without a
///     compile-time-visible corresponding constant here.
/// </summary>
/// <remarks>
///     This registry only centralizes the code strings themselves; it is not a substitute for
///     <see cref="RuleCatalog"/>, which additionally holds each code's title, classification
///     (official/mechanical/advisory), suggestion kind, and applicable modes. Adding a constant here does not by
///     itself add a <see cref="RuleCatalog"/> entry - that step remains a separate, deliberate edit -
///     but every code an emitter can produce is now guaranteed to be textually identical to the
///     code <see cref="RuleCatalog"/> uses, eliminating typo/copy drift between the two.
/// </remarks>
internal static class RuleCodes
{
    /// <summary>Sentence word-count limit (Rules 4.1, 8.4-8.7).</summary>
    public const string SentenceWordLimit = "STE100-4.1";

    /// <summary>No semicolons (Rule 8.1).</summary>
    public const string NoSemicolons = "STE100-8.1";

    /// <summary>No contractions (Rule 4.2).</summary>
    public const string NoContractions = "STE100-4.2";

    /// <summary>Dictionary/vocabulary enforcement (tool-defined mechanical check).</summary>
    public const string Dictionary = "STE100-DICT";

    /// <summary>Paragraph sentence-count cap (advisory heuristic).</summary>
    public const string AdvisoryParagraphLength = "STE100-ADV-PARA";

    /// <summary>Passive-voice detection (advisory heuristic).</summary>
    public const string AdvisoryPassiveVoice = "STE100-ADV-PASSIVE";

    /// <summary>Perfect/modal-perfect tense detection (advisory heuristic).</summary>
    public const string AdvisoryComplexVerb = "STE100-ADV-COMPLEXVERB";

    /// <summary>-ing form detection (advisory heuristic).</summary>
    public const string AdvisoryIngForm = "STE100-ADV-INGFORM";
}
