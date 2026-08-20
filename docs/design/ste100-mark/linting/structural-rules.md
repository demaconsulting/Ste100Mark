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
  rule tuning.
- *Returns*: `IReadOnlyList<Diagnostic>` - diagnostics produced in segment order.
- *Preconditions*: All arguments are non-null.
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
- *Postconditions*: Tests `PassiveVoiceRegex` against a masked copy of each sentence while
  reporting the verbatim sentence text in the diagnostic message.

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
  `List<Diagnostic> diagnostics`.
- *Returns*: `void`.
- *Postconditions*: Skips matches inside inline code spans and skips matches touching a
  sentence-ending period immediately before or after the word.

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
  `MaskInlineCodeSpans` and `OverlapsInlineCodeSpan` helpers used to exclude inline-code
  content from grammar-sensitive checks.
- **LintConfig** - provides `LintMode` and `RulesConfig` inputs.
- **Diagnostic** and **Severity** - carry rule output.
- **.NET BCL** - regex support and `Match` values.

#### Callers

- **Linter** - evaluates structural rules for each linted file after prose extraction.
