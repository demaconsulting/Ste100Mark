### StructuralRules

![Linting Structure](LintingView.svg)

#### Purpose

`StructuralRules` evaluates the non-dictionary prose rules enforced by the subsystem. Its
single responsibility is to turn extracted prose segments plus resolved configuration into
`Diagnostic` values for official Rule 4.1, Rule 8.1, and Rule 4.2 enforcement together with
the subsystem's advisory paragraph-length, passive-voice, complex-verb, and `-ing` form
heuristics.

#### Data Model

**ContractionRegex**: compiled regex for common English contractions used by Rule 4.2.

**ApostropheSContractionWords**: `HashSet<string>` used to distinguish likely contractions
such as `it's` from possessives such as `project's` after `ContractionRegex` matches a
trailing `'s`.

**PassiveVoiceRegex**: compiled heuristic regex matching `to be` plus a past-participle-like
word. The `been` alternative excludes a match immediately preceded by `has`/`have`/`had` so
that `has been X` is owned by `ComplexVerbRegex` only.

**ComplexVerbRegex**: compiled heuristic regex matching perfect tense
(`has`/`have`/`had` optionally followed by `been`, plus a past-participle-like word) or
modal-perfect tense (a modal verb plus `have` plus a past-participle-like word).

**IngFormRegex**: compiled heuristic regex matching a word of at least five letters ending in
`ing`.

**IngFormExclusions**: `HashSet<string>` of common words ending in `-ing` that are never a
present-participle verb form (prepositions, pronouns, or plain nouns with no corresponding
base verb, e.g. `during`, `morning`, `something`), excluded from `EvaluateIngForm`
unconditionally rather than relying on the project-supplied allow list to cover every one of
them.

**StativeParticiples**: `HashSet<string>` of common stative/adjectival past participles (for
example `energized`, `closed`, `connected`, `locked`) that, when following `is`/`are`, read as
a predicate adjective describing a current state rather than a genuine passive construction.
`EvaluatePassiveVoice`/`HasGenuinePassiveMatch` does not count such a match as passive unless
an explicit `by <agent>` phrase immediately follows the participle.

**CommonImperativeLeadVerbs**: `HashSet<string>` of common imperative-mood lead verbs (for
example `keep`, `confirm`, `verify`) for Procedure-mode instruction sentences. When one of
these starts a sentence, `HasGenuinePassiveMatch` treats a later `is`/`are` + past-participle
match in that same sentence as a predicate-adjective state the instruction asks the reader to
verify or maintain, not a passive construction, subject to the same explicit-agent-phrase
override as `StativeParticiples`.

**AmbiguousAdjectivalLeadVerbs**: `HashSet<string>` (`open`, `check`) - the subset of
`CommonImperativeLeadVerbs` also routinely used as prenominal adjectives describing
equipment/system types (for example `open systems`, `check valves`). `IsDeclarativeSubjectContinuation`
uses this set to decide whether a coordinated bare-noun object/subject continuation past
`and`/`or` (see `CoordinatingConjunctionsWithinSubject`) is plausible. **Known, accepted
limitation**: when both the sentence's lead word and the word immediately following a
coordinating conjunction are in this set, the heuristic cannot distinguish a genuine
imperative with a coordinated object (for example `Open covers and check seals are
calibrated.`) from a genuine declarative sentence with a coordinated, adjectivally-modified
subject (for example `Open systems and open valves are inspected.`) - both have the identical
`[lead] N1 and [ambiguous-word] N2 are participle` shape. The heuristic conservatively treats
this shape as imperative (remaining exempt from the passive-voice advisory) in both cases,
accepting a false negative on the declarative reading as the lesser cost, consistent with this
advisory's general bias toward avoiding false-positive noise over catching every true positive.

**Rule codes**:

- `STE100-4.1` - sentence word-count limit.
- `STE100-8.1` - semicolon ban.
- `STE100-4.2` - contraction ban.
- `STE100-ADV-PARA` - advisory paragraph sentence-count cap.
- `STE100-ADV-PASSIVE` - advisory passive-voice heuristic.
- `STE100-ADV-COMPLEXVERB` - advisory perfect/modal-perfect tense heuristic.
- `STE100-ADV-INGFORM` - advisory `-ing` form heuristic.

#### Key Methods

**Evaluate**: Applies every structural rule to the prose segments of one file.

- *Parameters*: `string file` - file path for diagnostics; `IReadOnlyList<ProseSegment> segments`
  - extracted prose; `LintMode mode` - resolved writing mode; `RulesConfig rules` - effective
  rule tuning; `IReadOnlyCollection<string>? allowedTerms` - the file's resolved
  `LintConfig.ResolveAllowedTerms` vocabulary, forwarded to `EvaluateIngForm` so a term a
  project has approved via the dictionary allow/ignore lists is also excluded from the
  `-ing`-form advisory, not only from `DictionaryChecker`.
- *Returns*: `IReadOnlyList<Diagnostic>` - diagnostics produced in segment order.
- *Preconditions*: `file`, `segments`, and `rules` are non-null. `allowedTerms` may be
  `null` or empty when no per-file allowance applies.
- *Postconditions*: Word-limit, semicolon, contraction, complex-verb, passive-voice,
  `-ing` form, and paragraph-length findings are appended in deterministic per-segment order.
  `EvaluateComplexVerb` runs before `EvaluatePassiveVoice` for each segment to prevent double
  reporting of `has/have/had been X` patterns.

**EvaluateWordLimit**: Private Rule 4.1 evaluator using `SentenceAnalyzer.Split` and the
mode-dependent word limit.

- *Parameters*: `string file`; `ProseSegment segment`; `IReadOnlyList<Sentence> sentences`;
  `int maxWords`; `LintMode mode`; `List<Diagnostic> diagnostics`.
- *Returns*: `void`.

**EvaluateSemicolons**: Private Rule 8.1 evaluator, disabled when
`rules.AllowSemicolons` is `true`.

- *Parameters*: `string file`; `ProseSegment segment`; `RulesConfig rules`;
  `List<Diagnostic> diagnostics`.
- *Returns*: `void`.
- *Postconditions*: Tests a `MarkdownProseExtractor.MaskInlineCodeSpans`-masked copy of the
  segment text so a semicolon appearing only inside an inline code span is not flagged.

**EvaluateContractions**: Private Rule 4.2 evaluator, disabled when
`rules.AllowContractions` is `true`.

- *Parameters*: `string file`; `ProseSegment segment`; `RulesConfig rules`;
  `List<Diagnostic> diagnostics`.
- *Returns*: `void`.
- *Postconditions*: Skips matches wholly inside inline code spans and suppresses likely
  possessives via `IsLikelyPossessive`.

**IsLikelyPossessive**: Distinguishes ambiguous `'s` matches between contractions and
possessives.

- *Parameters*: `Match match` - `ContractionRegex` match.
- *Returns*: `bool` - `true` when the match is treated as a possessive and should not be
  reported.

**EvaluatePassiveVoice**: Private advisory evaluator that emits at
`rules.PassiveVoice` severity.

- *Parameters*: `string file`; `ProseSegment segment`; `IReadOnlyList<Sentence> sentences`;
  `RulesConfig rules`; `List<Diagnostic> diagnostics`.
- *Returns*: `void`.
- *Postconditions*: Delegates to `HasGenuinePassiveMatch` (tested against a masked copy of
  each sentence) while reporting the verbatim sentence text in the diagnostic message.

**HasGenuinePassiveMatch**: Private helper determining whether a (masked) sentence contains
at least one `PassiveVoiceRegex` match that is a genuine passive construction rather than a
predicate adjective.

- *Parameters*: `string sentenceText` - masked sentence text.
- *Returns*: `bool` - `true` when at least one match is a genuine passive.
- *Postconditions*: A present-tense (`is`/`are`) match is treated as adjectival, not passive,
  when either the participle is in `StativeParticiples` or the sentence is an imperative lead
  sentence (see `IsImperativeLeadSentence`); either exemption is overridden - the match still
  counts as passive - when `HasAgentPhraseAfter` finds an explicit `by <agent>` phrase
  immediately following the participle. `was`/`were`/`be`/`being`/`been` matches are never
  exempted by either rule, since both exemptions apply only to present-tense `is`/`are`.

**IsImperativeLeadSentence**: Private helper determining whether a sentence opens (after
optionally skipping a Markdown list-item marker or leading ordinal) with a word in
`CommonImperativeLeadVerbs`.

- *Parameters*: `string sentenceText`.
- *Returns*: `bool`.

**HasAgentPhraseAfter**: Private helper determining whether the first word following a given
index is `by`, indicating an explicit agent phrase that makes a match unambiguously passive
regardless of either exemption above.

- *Parameters*: `string text`; `int index`.
- *Returns*: `bool`.

**EvaluateComplexVerb**: Private advisory evaluator that emits at `rules.ComplexVerb`
severity.

- *Parameters*: `string file`; `ProseSegment segment`; `IReadOnlyList<Sentence> sentences`;
  `RulesConfig rules`; `List<Diagnostic> diagnostics`.
- *Returns*: `void`.
- *Postconditions*: Tests `ComplexVerbRegex` against a masked copy of each sentence and owns
  perfect-tense passive constructions before passive-voice analysis runs.

**EvaluateIngForm**: Private advisory evaluator that emits at `rules.IngForm` severity,
once per matched `-ing` word.

- *Parameters*: `string file`; `ProseSegment segment`; `RulesConfig rules`;
  `IReadOnlyCollection<string>? allowedTerms` - the file's resolved allow-list vocabulary;
  `List<Diagnostic> diagnostics`.
- *Returns*: `void`.
- *Postconditions*: Skips the whole segment when it is a `Heading` or `TableHeader` - a short
  label, not a prose sentence. For each remaining `IngFormRegex` match, skips it when it
  overlaps an inline code span (`MarkdownProseExtractor.FindInlineCodeSpans`), an admonition
  label (`FindAdmonitionLabelSpans`), or a quoted/emphasis span (`FindQuotedOrEmphasisSpans`
  - a cited title or mention, not a use). Skips any match whose exact word is either in
  `IngFormExclusions` or in `allowedTerms`. For every surviving match, delegates to
  `PartOfSpeechGuesser.GuessIngFormRole` and reports the finding only when it resolves to
  `PartOfSpeech.Verb` - a gerund/participial-adjective use (noun-phrase subject, object of a
  preposition, gerund complement, material noun, or noun modifier) is not flagged.

**EvaluateParagraphLength**: Private advisory evaluator that runs only for paragraph
segments and is disabled when `rules.MaxSentencesParagraph` is `0`.

- *Parameters*: `string file`; `ProseSegment segment`; `IReadOnlyList<Sentence> sentences`;
  `RulesConfig rules`; `List<Diagnostic> diagnostics`.
- *Returns*: `void`.

**Truncate**: Private helper that caps long sentence excerpts at 80 characters for readable
messages.

- *Parameters*: `string text` - sentence excerpt.
- *Returns*: `string` - original text or an ellipsis-truncated version.

#### Error Handling

`Evaluate` propagates `ArgumentNullException` for null inputs. The class performs no I/O and
does not catch regex exceptions; regex matching is bounded by a one-second timeout.
Configuration disables checks by value (`AllowSemicolons`, `AllowContractions`, or advisory
`Severity.Off`) rather than by exceptions or sentinel diagnostics.

#### Dependencies

- **SentenceAnalyzer** - supplies sentence splitting and word counts.
- **MarkdownProseExtractor** - supplies `ProseSegment` text and roles, and the
  `MaskInlineCodeSpans`, `OverlapsInlineCodeSpan`, `FindInlineCodeSpans`,
  `FindAdmonitionLabelSpans`, and `FindQuotedOrEmphasisSpans` helpers used to exclude
  inline-code, admonition-label, and quoted/mention content from grammar-sensitive checks.
- **PartOfSpeechGuesser** - `EvaluateIngForm` delegates to `GuessIngFormRole` to resolve
  whether a matched `-ing` word is a genuine present-participle verb use or a
  gerund/participial-adjective use that should not be flagged.
- **LintConfig** - provides `LintMode` and `RulesConfig` inputs.
- **Diagnostic** and **Severity** - carry rule output.
- **.NET BCL** - regex support and `Match` values.

#### Callers

- **Linter** - evaluates structural rules for each linted file after prose extraction.
