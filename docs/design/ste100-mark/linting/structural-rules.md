### StructuralRules

![Linting Structure](LintingView.svg)

#### Purpose

`StructuralRules` evaluates the non-dictionary prose rules enforced by the subsystem. Its
single responsibility is to turn extracted prose segments plus resolved configuration into
`Diagnostic` values for official Rule 4.1, Rule 8.1, Rule 4.2, and the four advisory
heuristics.

#### Data Model

**ContractionRegex**: compiled regex for common English contractions used by Rule 4.2.

**PassiveVoiceRegex**: compiled heuristic regex matching `to be` plus a past-participle-like
word. The `been` alternative excludes a match immediately preceded by
`has`/`have`/`had` via a negative lookbehind, so that `has/have/had been X` is owned by
`ComplexVerbRegex` only (see the precedence note under **EvaluatePassiveVoice** below).

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
- `STE100-ADV-COMPLEXVERB` - advisory perfect/modal-perfect tense heuristic. Evaluated before
  `STE100-ADV-PASSIVE` so that a sentence such as "has been washed" is reported only once, as
  a complex-verb finding, and not also as a passive-voice finding.
- `STE100-ADV-INGFORM` - advisory `-ing` form heuristic.

#### Key Methods

**Evaluate**: Applies every structural rule to the prose segments of one file.

- *Parameters*: `string file` - file path for diagnostics; `IReadOnlyList<ProseSegment> segments`
  - extracted prose; `LintMode mode` - resolved writing mode; `RulesConfig rules` - effective
  rule tuning.
- *Returns*: `IReadOnlyList<Diagnostic>` - diagnostics produced in segment order.
- *Preconditions*: All arguments are non-null.
- *Postconditions*: Word-limit, semicolon, contraction, complex-verb, passive-voice, -ing form,
  and paragraph-length findings are appended in a deterministic per-segment order.
  `EvaluateComplexVerb` runs before `EvaluatePassiveVoice` for each segment, implementing the
  precedence decision described above.

**EvaluateWordLimit**: Private Rule 4.1 evaluator using `SentenceAnalyzer.Split` and the
mode-dependent word limit.

**EvaluateSemicolons**: Private Rule 8.1 evaluator, disabled when
`rules.AllowSemicolons` is `true`. Tests a `MarkdownProseExtractor.MaskInlineCodeSpans`-masked
copy of the segment text, so a semicolon appearing only inside an inline code span is not
flagged.

**EvaluateContractions**: Private Rule 4.2 evaluator, disabled when
`rules.AllowContractions` is `true`. Skips any `ContractionRegex` match that
`MarkdownProseExtractor.OverlapsInlineCodeSpan` reports as wholly inside an inline code span,
so a contraction appearing only inside inline code is not flagged; the diagnostic still quotes
the match's verbatim text.

**EvaluatePassiveVoice**: Private advisory evaluator that emits at
`rules.PassiveVoice` severity. Tests `PassiveVoiceRegex` against a
`MarkdownProseExtractor.MaskInlineCodeSpans`-masked copy of the sentence text, so the heuristic
does not analyze inline-code content as prose grammar; the diagnostic message still shows the
sentence's verbatim text (including any code span) via `Truncate`. Precedence note: because
`PassiveVoiceRegex`'s `been` alternative carries a negative lookbehind for a preceding
`has`/`have`/`had`, a sentence like "The panel has been opened" is not flagged here; it is
flagged only by `EvaluateComplexVerb`, which runs first in `Evaluate`. A sentence like "The
panel was opened" (no perfect-tense auxiliary) is unaffected and is still flagged here.

**EvaluateComplexVerb**: Private advisory evaluator that emits at `rules.ComplexVerb`
severity. Tests `ComplexVerbRegex` against a `MarkdownProseExtractor.MaskInlineCodeSpans`-masked
copy of the sentence text, so the heuristic does not analyze inline-code content as prose
grammar; the diagnostic message still shows the sentence's verbatim text via `Truncate`.

**EvaluateIngForm**: Private advisory evaluator that emits at `rules.IngForm` severity, once
per matched `-ing` word (not once per sentence). Skips any `IngFormRegex` match that
`MarkdownProseExtractor.OverlapsInlineCodeSpan` reports as wholly inside an inline code span.
Also skips any match that touches a sentence-ending period immediately before or after the
matched word (i.e. the adjacent character in the segment text is `.`), on the basis that such a
match is unlikely to be a present-participle verb form embedded mid-sentence. The diagnostic
message interpolates the matched word.

**EvaluateParagraphLength**: Private advisory evaluator that runs only for paragraph segments
and is disabled when `rules.MaxSentencesParagraph` is `0`.

**Truncate**: Private helper that caps long sentence excerpts at 80 characters for readable
messages.

#### Error Handling

`Evaluate` propagates `ArgumentNullException` for null inputs. The class performs no I/O and
does not catch regex exceptions; regex matching is bounded by a one-second timeout.

#### Dependencies

- **SentenceAnalyzer** - supplies sentence splitting and word counts.
- **MarkdownProseExtractor** - supplies `ProseSegment` text and roles, and the
  `MaskInlineCodeSpans`/`OverlapsInlineCodeSpan` helpers used to exclude inline-code content
  from the semicolon, contraction, passive-voice, complex-verb, and -ing form checks.
- **LintConfig** - provides `LintMode` and `RulesConfig` inputs.
- **Diagnostic** and **Severity** - carry rule output.
- **.NET BCL** - regex support.

#### Callers

- **Linter** - evaluates structural rules for each linted file.
