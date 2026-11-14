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
///     Resolves the grammatical role (noun or verb) of one word or phrase occurrence inside a prose
///     segment, so that <see cref="DictionaryChecker"/> and <see cref="StructuralRules"/> can apply a
///     part-of-speech-restricted rule only where it is relevant.
/// </summary>
internal interface IPartOfSpeechResolver
{
    /// <summary>
    ///     Resolves the part of speech of the occurrence at the given position.
    /// </summary>
    /// <param name="segmentText">The full prose segment text containing the occurrence.</param>
    /// <param name="matchIndex">Zero-based index in <paramref name="segmentText"/> where the occurrence starts.</param>
    /// <param name="matchLength">Length of the occurrence in characters.</param>
    /// <returns>
    ///     A confident dictionary role (<see cref="PartOfSpeech.Noun"/>, <see cref="PartOfSpeech.Verb"/>,
    ///     <see cref="PartOfSpeech.Adjective"/>, or <see cref="PartOfSpeech.Adverb"/>), the resolver-only
    ///     <see cref="PartOfSpeech.Determiner"/> result for a confident determiner, or
    ///     <see langword="null"/> (inconclusive) for other outcomes.
    /// </returns>
    PartOfSpeech? Resolve(string segmentText, int matchIndex, int matchLength);

    /// <summary>
    ///     Resolves the part of speech of the occurrence at the given position and reports the
    ///     tagger evidence behind the answer.
    /// </summary>
    /// <remarks>
    ///     The default implementation wraps <see cref="Resolve"/> and has no tagger evidence, so
    ///     a resolver that only implements <see cref="Resolve"/> (for example a test double) keeps
    ///     working; only <see cref="RoleEvidence.Role"/> and <see cref="RoleEvidence.Outcome"/> are set.
    /// </remarks>
    /// <param name="segmentText">The full prose segment text containing the occurrence.</param>
    /// <param name="matchIndex">Zero-based index in <paramref name="segmentText"/> where the occurrence starts.</param>
    /// <param name="matchLength">Length of the occurrence in characters.</param>
    /// <returns>The resolved role together with the evidence.</returns>
    RoleEvidence ResolveWithEvidence(string segmentText, int matchIndex, int matchLength)
    {
        var role = Resolve(segmentText, matchIndex, matchLength);
        return RoleEvidence.FromRole(role);
    }
}

/// <summary>
///     The outcome of a role resolution together with the tagger evidence behind it.
/// </summary>
/// <param name="Role">The resolved role, or <see langword="null"/> when inconclusive.</param>
/// <param name="Outcome">
///     Why the role is what it is: <c>resolved</c>, <c>below-threshold</c>, <c>unknown-word</c>,
///     <c>unsupported-tag</c>, <c>multi-token</c>, or <c>unavailable</c> (no tagger evidence).
/// </param>
/// <param name="Tag">Lower-case universal part-of-speech tag chosen by the tagger, or <see langword="null"/>.</param>
/// <param name="Confidence">Confidence (0 to 1) of the chosen tag, or <see langword="null"/>.</param>
/// <param name="Margin">Confidence minus the runner-up probability, or <see langword="null"/>.</param>
/// <param name="IsKnownWord">Whether the tagger's lexicon knows the word, or <see langword="null"/>.</param>
/// <param name="IsConstrained">Whether a project glossary decided the tag, or <see langword="null"/>.</param>
/// <param name="RunnerUpTag">Tag of the second most likely reading, or <see langword="null"/>.</param>
/// <param name="RunnerUpProbability">Probability of the second most likely reading, or <see langword="null"/>.</param>
/// <param name="ThresholdMet">Whether the confidence reached the acceptance threshold, or <see langword="null"/>.</param>
internal sealed record RoleEvidence(
    PartOfSpeech? Role,
    string Outcome,
    string? Tag = null,
    double? Confidence = null,
    double? Margin = null,
    bool? IsKnownWord = null,
    bool? IsConstrained = null,
    string? RunnerUpTag = null,
    double? RunnerUpProbability = null,
    bool? ThresholdMet = null)
{
    /// <summary>Outcome text for a resolver that gives no tagger evidence.</summary>
    internal const string UnavailableOutcome = "unavailable";

    /// <summary>
    ///     Creates evidence that carries only a role (no tagger evidence).
    /// </summary>
    /// <param name="role">The resolved role, or <see langword="null"/>.</param>
    /// <returns>The evidence.</returns>
    public static RoleEvidence FromRole(PartOfSpeech? role)
    {
        return new RoleEvidence(role, role is null ? UnavailableOutcome : "resolved");
    }
}
