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

using System.Text.RegularExpressions;

namespace DemaConsulting.Ste100Mark.Linting;

/// <summary>
///     One sense of the matched dictionary entry: its part of speech label and base-form alternatives.
/// </summary>
/// <param name="Pos">Part of speech label (<c>noun</c>, <c>verb</c>, <c>adjective</c>, <c>adverb</c> or <c>general</c>).</param>
/// <param name="Alternatives">The dictionary's base-form alternatives for the sense.</param>
internal sealed record EvidenceSense(string Pos, IReadOnlyList<string> Alternatives);

/// <summary>
///     One suggested replacement shown for a finding.
/// </summary>
/// <param name="Text">The suggested text (inflected to the flagged form when that was safe).</param>
/// <param name="InflectionSafe">
///     Whether <paramref name="Text"/> was inflected to match the flagged form; <see langword="false"/>
///     means the base form is shown and the writer must adjust it.
/// </param>
/// <param name="Pos">Part of speech label of the sense the suggestion belongs to.</param>
internal sealed record EvidenceSuggestion(string Text, bool InflectionSafe, string Pos);

/// <summary>
///     The raw evidence <see cref="DictionaryChecker"/> collects for one dictionary finding when
///     detailed output is requested. <see cref="FindingDetail.Build"/> turns it into the reported
///     <see cref="FindingDetail"/>.
/// </summary>
/// <param name="Segment">The prose segment that holds the match.</param>
/// <param name="SpanStart">Start of the flagged text within <see cref="ProseSegment.Text"/>.</param>
/// <param name="SpanLength">Length of the flagged text.</param>
/// <param name="Surface">The flagged text exactly as written.</param>
/// <param name="BaseTerm">The dictionary term the match belongs to.</param>
/// <param name="FormKinds">The grammatical forms the spelling represents (<c>base</c> for the base term).</param>
/// <param name="IsSplit">Whether the match is a phrasal verb split by other words.</param>
/// <param name="IsHyphenated">Whether the match carries a hyphen prefix.</param>
/// <param name="IsGeneratedForm">Whether the spelling is a generated word form rather than the base term.</param>
/// <param name="Senses">Every sense of the matched entry.</param>
/// <param name="SensePos">The part of speech label of the reported sense, or <c>ambiguous</c>.</param>
/// <param name="CandidateCount">How many senses the finding reports.</param>
/// <param name="Suggestions">The suggested replacements.</param>
/// <param name="Tagger">The role resolution evidence for the flagged word.</param>
/// <param name="ParticipleRule">Outcome of the participle rule (<c>not-applicable</c> or <c>kept-confident-verb</c>).</param>
/// <param name="FragmentRule">Outcome of the fragment rule (<c>not-applicable</c> or <c>kept-auxiliary-present</c>).</param>
internal sealed record DictionaryEvidence(
    ProseSegment Segment,
    int SpanStart,
    int SpanLength,
    string Surface,
    string BaseTerm,
    IReadOnlyList<string> FormKinds,
    bool IsSplit,
    bool IsHyphenated,
    bool IsGeneratedForm,
    IReadOnlyList<EvidenceSense> Senses,
    string SensePos,
    int CandidateCount,
    IReadOnlyList<EvidenceSuggestion> Suggestions,
    RoleEvidence Tagger,
    string ParticipleRule,
    string FragmentRule);

/// <summary>Source location of a finding.</summary>
/// <param name="StartLine">1-based first line of the flagged text.</param>
/// <param name="StartColumn">1-based first column, or <see langword="null"/> when not exact.</param>
/// <param name="EndLine">1-based last line of the flagged text.</param>
/// <param name="EndColumn">1-based column after the last character, or <see langword="null"/> when not exact.</param>
/// <param name="SegmentStart">Start offset of the flagged text within the extracted prose segment.</param>
/// <param name="SegmentEnd">End offset (exclusive) of the flagged text within the extracted prose segment.</param>
/// <param name="ColumnsExact">Whether the columns were located and verified in the source line.</param>
internal sealed record FindingSpan(
    int StartLine,
    int? StartColumn,
    int EndLine,
    int? EndColumn,
    int SegmentStart,
    int SegmentEnd,
    bool ColumnsExact);

/// <summary>The sentence that holds a finding.</summary>
/// <param name="Text">The sentence text (a window of at most 400 characters).</param>
/// <param name="Truncated">Whether the sentence was shortened to fit the cap.</param>
internal sealed record FindingSentence(string Text, bool Truncated);

/// <summary>Source lines around a finding.</summary>
/// <param name="StartLine">1-based line number of the first entry in <paramref name="Lines"/>.</param>
/// <param name="Lines">The source lines (each capped at 300 characters).</param>
internal sealed record FindingContext(int StartLine, IReadOnlyList<string> Lines);

/// <summary>The dictionary match evidence of a finding.</summary>
/// <param name="Surface">The flagged text exactly as written.</param>
/// <param name="Base">The dictionary term.</param>
/// <param name="FormKind">
///     Primary kind: <c>phrasal-split</c>, <c>hyphenated</c>, or the first of <paramref name="FormKinds"/>.
/// </param>
/// <param name="FormKinds">
///     Grammatical forms: <c>base</c>, <c>third-person</c>, <c>past</c>, <c>participle</c>,
///     <c>gerund</c> or <c>plural</c>.
/// </param>
/// <param name="SensePos">Part of speech label of the reported sense, or <c>ambiguous</c>.</param>
/// <param name="Senses">Every sense of the dictionary entry.</param>
internal sealed record FindingMatch(
    string Surface,
    string Base,
    string FormKind,
    IReadOnlyList<string> FormKinds,
    string SensePos,
    IReadOnlyList<EvidenceSense> Senses);

/// <summary>The second most likely tag reading.</summary>
/// <param name="Tag">The tag.</param>
/// <param name="Probability">The probability of the tag.</param>
internal sealed record FindingRunnerUp(string? Tag, double? Probability);

/// <summary>The tagger evidence behind a finding.</summary>
/// <param name="Role">Resolved role label, or <see langword="null"/> when inconclusive.</param>
/// <param name="Tag">Universal part-of-speech tag chosen by the tagger.</param>
/// <param name="Confidence">Confidence of the tag.</param>
/// <param name="ThresholdMet">Whether the confidence reached <paramref name="Threshold"/>.</param>
/// <param name="Threshold">The acceptance threshold.</param>
/// <param name="Margin">Confidence minus the runner-up probability.</param>
/// <param name="RunnerUp">The second most likely reading.</param>
/// <param name="IsKnownWord">Whether the lexicon knows the word.</param>
/// <param name="IsConstrained">Whether a glossary decided the tag.</param>
/// <param name="Outcome">Why the role is what it is.</param>
/// <param name="ParticipleRule">Participle rule outcome.</param>
/// <param name="FragmentRule">Fragment rule outcome.</param>
internal sealed record FindingTagger(
    string? Role,
    string? Tag,
    double? Confidence,
    bool? ThresholdMet,
    double Threshold,
    double? Margin,
    FindingRunnerUp? RunnerUp,
    bool? IsKnownWord,
    bool? IsConstrained,
    string Outcome,
    string ParticipleRule,
    string FragmentRule);

/// <summary>Deterministic confidence class of a finding.</summary>
/// <param name="Class"><c>certain</c>, <c>probable</c> or <c>doubtful</c>.</param>
/// <param name="Reason">Reason code from the classification table.</param>
internal sealed record FindingConfidence(string Class, string Reason);

/// <summary>One suggested replacement.</summary>
/// <param name="Text">Suggested text.</param>
/// <param name="InflectionSafe">Whether the text was inflected to the flagged form.</param>
/// <param name="Pos">Part of speech label of the sense.</param>
internal sealed record FindingSuggestion(string Text, bool InflectionSafe, string Pos);

/// <summary>One suppression option.</summary>
/// <param name="Kind"><c>allow</c>, <c>allow-in-phrase</c> or <c>glossary</c>.</param>
/// <param name="Snippet">Configuration snippet to add to the configuration file.</param>
/// <param name="Scope">What the option suppresses.</param>
internal sealed record SuppressionOption(string Kind, string Snippet, string Scope);

/// <summary>The suppression hints of a finding.</summary>
/// <param name="Warning">The standing warning that suppression hides true findings too.</param>
/// <param name="Options">The suppression options.</param>
internal sealed record FindingSuppression(string Warning, IReadOnlyList<SuppressionOption> Options);

/// <summary>
///     The detailed evidence reported for one dictionary finding in the <c>json-detailed</c> format:
///     location, sentence and context lines, match and tagger evidence, a deterministic
///     confidence class, suggestions, and suppression hints.
/// </summary>
/// <remarks>
///     Everything here is computed deterministically from the <see cref="DictionaryEvidence"/>
///     the checker collected and the source lines of the file; no new analysis is performed. The
///     unit is separate from <see cref="DictionaryChecker"/> so that the checker stays focused on
///     matching. Columns are best effort because <see cref="ProseSegment"/> does not record source
///     columns: the n-th occurrence of the flagged text on its line is located in the source line
///     and accepted only when the text there equals the flagged text; otherwise
///     <see cref="FindingSpan.ColumnsExact"/> is <see langword="false"/> and only lines are given.
/// </remarks>
/// <param name="Span">Source location.</param>
/// <param name="Sentence">The sentence that holds the finding.</param>
/// <param name="Context">Source lines around the finding, or <see langword="null"/> when none were requested.</param>
/// <param name="ContextKind"><c>heading</c>, <c>table-cell</c>, <c>list-item</c>, <c>paragraph</c> or <c>block-quote</c>.</param>
/// <param name="Match">Dictionary match evidence.</param>
/// <param name="Tagger">Tagger evidence.</param>
/// <param name="Confidence">Confidence class.</param>
/// <param name="Suggestions">Suggested replacements.</param>
/// <param name="Suppression">Suppression hints.</param>
internal sealed record FindingDetail(
    FindingSpan Span,
    FindingSentence Sentence,
    FindingContext? Context,
    string ContextKind,
    FindingMatch Match,
    FindingTagger Tagger,
    FindingConfidence Confidence,
    IReadOnlyList<FindingSuggestion> Suggestions,
    FindingSuppression Suppression)
{
    /// <summary>Form kind of a base-term match.</summary>
    internal const string FormBase = "base";

    /// <summary>Form kind of a split phrasal verb match.</summary>
    internal const string FormPhrasalSplit = "phrasal-split";

    /// <summary>Form kind of a hyphen-prefixed match.</summary>
    internal const string FormHyphenated = "hyphenated";

    /// <summary>Rule outcome text when the rule did not apply.</summary>
    internal const string RuleNotApplicable = "not-applicable";

    /// <summary>Participle rule outcome: a participial form kept because the role is a confident verb.</summary>
    internal const string RuleKeptConfidentVerb = "kept-confident-verb";

    /// <summary>Fragment rule outcome: a participial form in a heading or cell kept because an auxiliary is present.</summary>
    internal const string RuleKeptAuxiliaryPresent = "kept-auxiliary-present";

    /// <summary>Sense label used when more than one sense is reported.</summary>
    internal const string SensePosAmbiguous = "ambiguous";

    /// <summary>Confidence class: the finding is a reliable match.</summary>
    internal const string ClassCertain = "certain";

    /// <summary>Confidence class: the finding is a match with a known weaker signal.</summary>
    internal const string ClassProbable = "probable";

    /// <summary>Confidence class: the grammatical role is not established.</summary>
    internal const string ClassDoubtful = "doubtful";

    /// <summary>Maximum length of the reported sentence.</summary>
    internal const int MaxSentenceLength = 400;

    /// <summary>Maximum length of one reported context line.</summary>
    internal const int MaxContextLineLength = 300;

    /// <summary>The tagger margin below which a participial finding is only probable.</summary>
    internal const double LowMarginThreshold = 0.50;

    /// <summary>The standing warning attached to every suppression hint list.</summary>
    internal const string SuppressionWarning =
        "Use only for a true false positive; suppression hides this finding for every matching occurrence in the scope shown";

    /// <summary>Time limit for every regular expression in this unit.</summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>A word directly before a position (whitespace in between).</summary>
    private static readonly Regex PreviousWordRegex =
        new(@"([\p{L}\p{N}][\p{L}\p{N}'’-]*)\s+$", RegexOptions.CultureInvariant, RegexTimeout);

    /// <summary>A word directly after a position (whitespace in between).</summary>
    private static readonly Regex NextWordRegex =
        new(@"^\s+([\p{L}\p{N}][\p{L}\p{N}'’-]*)", RegexOptions.CultureInvariant, RegexTimeout);

    /// <summary>A text that is safe as a plain YAML scalar.</summary>
    private static readonly Regex PlainScalarRegex =
        new(@"^[\p{L}\p{N}][\p{L}\p{N} '’.-]*$", RegexOptions.CultureInvariant, RegexTimeout);

    /// <summary>
    ///     Builds the detail for a diagnostic.
    /// </summary>
    /// <param name="diagnostic">The diagnostic; it must carry <see cref="Diagnostic.Evidence"/>.</param>
    /// <param name="sourceLines">The lines of the source file (index 0 is line 1).</param>
    /// <param name="contextLines">Number of context lines before and after the flagged lines (0 for none).</param>
    /// <returns>The detail, or <see langword="null"/> when the diagnostic has no evidence.</returns>
    public static FindingDetail? Build(Diagnostic diagnostic, IReadOnlyList<string> sourceLines, int contextLines)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        ArgumentNullException.ThrowIfNull(sourceLines);
        if (diagnostic.Evidence is not { } evidence)
        {
            return null;
        }

        var span = ComputeSpan(evidence, sourceLines);
        var sentence = ExtractSentence(evidence, out var sentenceStart, out var sentenceEnd);
        var tagger = BuildTagger(evidence);
        var formKind = evidence.FormKinds.FirstOrDefault() ?? FormBase;
        if (evidence.IsSplit)
        {
            formKind = FormPhrasalSplit;
        }
        else if (evidence.IsHyphenated)
        {
            formKind = FormHyphenated;
        }

        return new FindingDetail(
            span,
            sentence,
            ComputeContext(span, sourceLines, contextLines),
            ComputeContextKind(evidence, span, sourceLines),
            new FindingMatch(
                evidence.Surface,
                evidence.BaseTerm,
                formKind,
                evidence.FormKinds,
                evidence.SensePos,
                evidence.Senses),
            tagger,
            Classify(evidence),
            evidence.Suggestions.Select(s => new FindingSuggestion(s.Text, s.InflectionSafe, s.Pos)).ToList(),
            BuildSuppression(evidence, sentenceStart, sentenceEnd));
    }

    /// <summary>
    ///     Classifies a finding with the documented confidence table; the first matching row wins.
    /// </summary>
    /// <remarks>
    ///     Rows, in order: <c>role-inconclusive</c> (no role and no threshold or unknown-word
    ///     explanation), <c>multiple-senses</c>, <c>tagger-below-threshold</c> (below the threshold or
    ///     an unknown word), <c>split-phrasal</c>, <c>hyphenated-form</c>,
    ///     <c>fragment-with-auxiliary</c>, <c>participle-low-margin</c> (a kept participial form with a
    ///     tagger margin below <see cref="LowMarginThreshold"/>), then <c>role-confident</c>.
    /// </remarks>
    /// <param name="evidence">The evidence collected for the finding.</param>
    /// <returns>The confidence class and reason code.</returns>
    public static FindingConfidence Classify(DictionaryEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var tagger = evidence.Tagger;
        var weakTagger = tagger.Outcome is "below-threshold" or "unknown-word";
        if (tagger.Role is null && !weakTagger)
        {
            return new FindingConfidence(ClassDoubtful, "role-inconclusive");
        }

        if (evidence.CandidateCount > 1)
        {
            return new FindingConfidence(ClassDoubtful, "multiple-senses");
        }

        if (weakTagger)
        {
            return new FindingConfidence(ClassDoubtful, "tagger-below-threshold");
        }

        if (evidence.IsSplit)
        {
            return new FindingConfidence(ClassProbable, "split-phrasal");
        }

        if (evidence.IsHyphenated)
        {
            return new FindingConfidence(ClassProbable, "hyphenated-form");
        }

        if (evidence.FragmentRule == RuleKeptAuxiliaryPresent)
        {
            return new FindingConfidence(ClassProbable, "fragment-with-auxiliary");
        }

        if (evidence.ParticipleRule == RuleKeptConfidentVerb && tagger.Margin is < LowMarginThreshold)
        {
            return new FindingConfidence(ClassProbable, "participle-low-margin");
        }

        return new FindingConfidence(ClassCertain, "role-confident");
    }

    /// <summary>
    ///     Builds the reported tagger evidence.
    /// </summary>
    private static FindingTagger BuildTagger(DictionaryEvidence evidence)
    {
        var tagger = evidence.Tagger;
        var runnerUp = tagger.RunnerUpTag is null && tagger.RunnerUpProbability is null
            ? null
            : new FindingRunnerUp(tagger.RunnerUpTag, tagger.RunnerUpProbability);
        return new FindingTagger(
            tagger.Role is { } role ? RoleLabel(role) : null,
            tagger.Tag,
            tagger.Confidence,
            tagger.ThresholdMet,
            PartOfSpeechResolver.MinimumConfidence,
            tagger.Margin,
            runnerUp,
            tagger.IsKnownWord,
            tagger.IsConstrained,
            tagger.Outcome,
            evidence.ParticipleRule,
            evidence.FragmentRule);
    }

    /// <summary>
    ///     Renders a resolved role for the report.
    /// </summary>
    private static string RoleLabel(PartOfSpeech role)
    {
        return role.ToString().ToLowerInvariant();
    }

    /// <summary>
    ///     Computes the source lines and (when verifiable) columns of the flagged text.
    /// </summary>
    private static FindingSpan ComputeSpan(DictionaryEvidence evidence, IReadOnlyList<string> sourceLines)
    {
        var segment = evidence.Segment;
        var start = evidence.SpanStart;
        var end = start + evidence.SpanLength;
        var startLine = segment.ResolveLine(start);
        var endLine = segment.ResolveLine(Math.Max(start, end - 1));

        int? startColumn = null;
        int? endColumn = null;
        if (startLine == endLine && startLine >= 1 && startLine <= sourceLines.Count && evidence.Surface.Length > 0)
        {
            var column = LocateColumn(evidence, startLine, sourceLines[startLine - 1]);
            if (column is { } found)
            {
                startColumn = found;
                endColumn = found + evidence.Surface.Length;
            }
        }

        return new FindingSpan(startLine, startColumn, endLine, endColumn, start, end, startColumn is not null);
    }

    /// <summary>
    ///     Locates the 1-based column of the flagged text in its source line by occurrence counting.
    /// </summary>
    private static int? LocateColumn(DictionaryEvidence evidence, int line, string sourceLine)
    {
        var segment = evidence.Segment;
        var surface = evidence.Surface;
        var text = segment.Text;

        // Occurrences of the flagged text that start before it on the same source line.
        var lineStart = 0;
        foreach (var (offset, lineNumber) in segment.LineOffsets)
        {
            if (lineNumber == line && offset <= evidence.SpanStart)
            {
                lineStart = offset;
            }
        }

        var before = 0;
        var from = lineStart;
        while (from < evidence.SpanStart)
        {
            var index = text.IndexOf(surface, from, StringComparison.OrdinalIgnoreCase);
            if (index < 0 || index >= evidence.SpanStart)
            {
                break;
            }

            before++;
            from = index + 1;
        }

        // Find the same occurrence in the source line.
        var position = -1;
        var count = 0;
        var occurrences = 0;
        var search = 0;
        while (true)
        {
            var index = sourceLine.IndexOf(surface, search, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                break;
            }

            occurrences++;
            if (count == before)
            {
                position = index;
            }

            count++;
            search = index + 1;
        }

        if (position < 0)
        {
            return null;
        }

        // A table row is split into one segment per cell, so occurrences in other cells cannot be
        // counted; only an unambiguous single occurrence is accepted there.
        if (segment.Role is SegmentRole.TableRow or SegmentRole.TableHeader && occurrences != 1)
        {
            return null;
        }

        return position + 1;
    }

    /// <summary>
    ///     Extracts the sentence that holds the flagged text, capped to <see cref="MaxSentenceLength"/>.
    /// </summary>
    private static FindingSentence ExtractSentence(DictionaryEvidence evidence, out int sentenceStart, out int sentenceEnd)
    {
        var text = evidence.Segment.Text;
        sentenceStart = 0;
        sentenceEnd = text.Length;
        foreach (Match boundary in SentenceAnalyzer.SentenceSplitRegex.Matches(text))
        {
            if (boundary.Index + boundary.Length <= evidence.SpanStart)
            {
                sentenceStart = boundary.Index + boundary.Length;
            }
            else
            {
                sentenceEnd = boundary.Index;
                break;
            }
        }

        sentenceEnd = Math.Max(sentenceEnd, Math.Min(text.Length, evidence.SpanStart + evidence.SpanLength));
        var length = sentenceEnd - sentenceStart;
        if (length <= MaxSentenceLength)
        {
            return new FindingSentence(text.Substring(sentenceStart, length).Trim(), false);
        }

        var windowStart = Math.Clamp(
            evidence.SpanStart - (MaxSentenceLength / 2),
            sentenceStart,
            sentenceEnd - MaxSentenceLength);
        return new FindingSentence(text.Substring(windowStart, MaxSentenceLength).Trim(), true);
    }

    /// <summary>
    ///     Reads the source lines around the flagged lines, or <see langword="null"/> for zero context lines.
    /// </summary>
    private static FindingContext? ComputeContext(FindingSpan span, IReadOnlyList<string> sourceLines, int contextLines)
    {
        if (contextLines <= 0 || sourceLines.Count == 0)
        {
            return null;
        }

        var first = Math.Max(1, span.StartLine - contextLines);
        var last = Math.Min(sourceLines.Count, span.EndLine + contextLines);
        var lines = new List<string>();
        for (var line = first; line <= last; line++)
        {
            var content = sourceLines[line - 1];
            lines.Add(content.Length > MaxContextLineLength ? content[..MaxContextLineLength] : content);
        }

        return new FindingContext(first, lines);
    }

    /// <summary>
    ///     Names the kind of Markdown element that holds the finding.
    /// </summary>
    private static string ComputeContextKind(DictionaryEvidence evidence, FindingSpan span, IReadOnlyList<string> sourceLines)
    {
        switch (evidence.Segment.Role)
        {
            case SegmentRole.Heading:
                return "heading";
            case SegmentRole.TableRow:
            case SegmentRole.TableHeader:
                return "table-cell";
            case SegmentRole.ListItem:
                return "list-item";
            default:
                return span.StartLine >= 1
                    && span.StartLine <= sourceLines.Count
                    && sourceLines[span.StartLine - 1].TrimStart().StartsWith('>')
                        ? "block-quote"
                        : "paragraph";
        }
    }

    /// <summary>
    ///     Builds the suppression hints for a finding.
    /// </summary>
    private static FindingSuppression BuildSuppression(DictionaryEvidence evidence, int sentenceStart, int sentenceEnd)
    {
        var options = new List<SuppressionOption>();
        var surfaceKey = string.Join(' ', evidence.Surface.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();

        // allow: the spelling for a generated or prefixed form, the base term otherwise.
        var allowed = evidence.IsSplit || !(evidence.IsGeneratedForm || evidence.IsHyphenated)
            ? evidence.BaseTerm
            : surfaceKey;
        options.Add(new SuppressionOption(
            "allow",
            $"dictionary:\n  allow:\n    - {YamlScalar(allowed)}",
            "project-wide (or per profile); broader than this finding"));

        var phrases = PhraseCandidates(evidence, sentenceStart, sentenceEnd);
        if (phrases.Count > 0)
        {
            options.Add(new SuppressionOption(
                "allow-in-phrase",
                "dictionary:\n  allow-in-phrase:\n" + string.Join("\n", phrases.Select(p => $"    - {YamlScalar(p)}")),
                "occurrences inside the phrase only; the phrases are literal text, check them before use"));
        }

        // glossary: a generated form only (the loader rejects a glossary term that is a dictionary entry).
        if (evidence.IsGeneratedForm && !evidence.IsSplit && !evidence.IsHyphenated
            && GlossaryPos(evidence.FormKinds) is { } pos)
        {
            options.Add(new SuppressionOption(
                "glossary",
                $"dictionary:\n  glossary:\n    {YamlScalar(surfaceKey)}:\n      definition: <describe the term>\n      pos: {pos}",
                "the term as written, everywhere in the project; the pos is a suggestion to review"));
        }

        return new FindingSuppression(SuppressionWarning, options);
    }

    /// <summary>
    ///     Builds up to two phrase candidates from the flagged text and its neighbor words in the sentence.
    /// </summary>
    private static List<string> PhraseCandidates(DictionaryEvidence evidence, int sentenceStart, int sentenceEnd)
    {
        var text = evidence.Segment.Text;
        var spanEnd = evidence.SpanStart + evidence.SpanLength;
        var phrases = new List<string>();
        var before = text[sentenceStart..evidence.SpanStart];
        var after = text[spanEnd..Math.Max(spanEnd, sentenceEnd)];

        var previous = PreviousWordRegex.Match(before);
        if (previous.Success)
        {
            phrases.Add($"{previous.Groups[1].Value} {evidence.Surface}");
        }

        var next = NextWordRegex.Match(after);
        if (next.Success)
        {
            phrases.Add($"{evidence.Surface} {next.Groups[1].Value}");
        }

        return phrases;
    }

    /// <summary>
    ///     Suggests the glossary part of speech for the form kinds, or <see langword="null"/> when none fits.
    /// </summary>
    private static string? GlossaryPos(IReadOnlyList<string> formKinds)
    {
        if (formKinds.Contains("past") || formKinds.Contains("participle"))
        {
            return "adjective";
        }

        if (formKinds.Contains("gerund") || formKinds.Contains("plural"))
        {
            return "noun";
        }

        return null;
    }

    /// <summary>
    ///     Renders a text as a YAML scalar, quoting it when it is not a plain word or phrase.
    /// </summary>
    private static string YamlScalar(string text)
    {
        return PlainScalarRegex.IsMatch(text)
            ? text
            : "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}
