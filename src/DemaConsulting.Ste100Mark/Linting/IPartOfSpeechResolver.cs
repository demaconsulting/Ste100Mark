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
    ///     <see cref="PartOfSpeech.Noun"/> or <see cref="PartOfSpeech.Verb"/> when the role is resolved
    ///     with sufficient confidence; otherwise <see langword="null"/> (inconclusive).
    /// </returns>
    PartOfSpeech? Resolve(string segmentText, int matchIndex, int matchLength);
}
