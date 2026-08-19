### DictionaryChecker

![Linting Structure](LintingView.svg)

#### Purpose

`DictionaryChecker` enforces the effective disallow list built by `LintDictionary`. Its
single responsibility is to scan extracted prose segments for case-insensitive whole-term and
whole-phrase matches, select the applicable POS-tagged sense(s) of each matched entry using
`PartOfSpeechGuesser`, and emit `STE100-DICT` diagnostics.

#### Data Model

**RuleCode**: constant-by-convention string value `STE100-DICT` emitted for every dictionary
finding.

**Entry ordering**: the effective dictionary entries are sorted by descending term length
before matching so multi-word phrases are preferred over shorter overlapping terms.

**Pattern shape**: each term is converted into a whole-word/whole-phrase regex using
negative lookbehind/lookahead for `\w` and `-`, with spaces widened to `\s+`.

**Sense selection**: a term with exactly one sense is always reported using that sense - no
ambiguity is possible. A term with multiple senses is resolved by calling
`PartOfSpeechGuesser.Guess` with the segment text and match position: a confident `Noun` or
`Verb` guess narrows the candidate senses to those matching that role (plus any `Any`-pos
sense); an inconclusive guess, or a guess that matches no sense in the schema, keeps every
sense as a candidate. Exactly one surviving candidate is reported confidently, with the
sense's `Pos` named in the message (`Any` renders as "general"); more than one surviving
candidate is reported as one ambiguous diagnostic listing every candidate sense.

**Inline code exclusion**: matches falling wholly inside an inline code span (per
`MarkdownProseExtractor.FindInlineCodeSpans`/`OverlapsInlineCodeSpan`) are ignored before a
diagnostic is built, so a disallowed term that appears only inside inline code (for example, a
CLI flag written as `` `utilize-flag` ``) is not flagged; the same term appearing outside a
code span in the same segment is still flagged normally.

#### Key Methods

**Evaluate**: Scans every extracted prose segment against the merged dictionary.

- *Parameters*: `string file` - file path for diagnostics; `IReadOnlyList<ProseSegment> segments`
  - prose segments; `LintDictionary dictionary` - merged dictionary; `LintMode mode` - the
  file's resolved writing mode, forwarded to `PartOfSpeechGuesser.Guess`.
- *Returns*: `IReadOnlyList<Diagnostic>` - one diagnostic per matched occurrence.
- *Preconditions*: `file`, `segments`, and `dictionary` are non-null.
- *Postconditions*: Matches are returned in segment order, excluding any match that falls
  wholly inside an inline code span; every diagnostic uses severity `Error`, rule code
  `STE100-DICT`, and a suggestion string when alternatives are present.

**BuildDiagnostic**: Selects the applicable sense(s) for one match and builds its diagnostic.

**ConfidentDiagnostic**: Builds a diagnostic for a single resolved sense (single-sense term, or
the sole surviving candidate of a multi-sense term), optionally labeling the sense's `Pos` in
the message. When the sense has one or more alternatives, they are folded into the message
itself via `JoinAlternatives` (for example `"use 'effect' instead"` or `"use 'cause', 'give',
'make', or 'supply' instead"`); a sense with no alternatives falls back to the generic
"it is not an approved ASD-STE100-style term" wording. The separate `Suggestion` field is
unaffected and remains a plain, comma-separated list of alternatives.

**AmbiguousDiagnostic**: Builds a diagnostic listing every candidate sense, labeled as
ambiguous, when the heuristic could not confidently resolve one sense. The message has the
shape `"Ambiguous part of speech for '{term}' — possible corrections: {clauses}."`, where each
candidate sense contributes a clause `"as a {pos}, use {alternatives}"` (via
`JoinAlternatives`), or just `"as a {pos}"` when a sense has no alternatives, joined across
senses with `"; "`. The separate `Suggestion` field is unaffected and remains
`"{alternatives} ({pos}); ..."`.

**JoinAlternatives**: Renders a sense's alternatives as natural "or" phrasing for embedding in
a `Message`: a single alternative is quoted alone (`'a'`); two alternatives are joined with
"or" and no Oxford comma (`'a' or 'b'`); three or more are joined with commas plus an Oxford
comma before the final "or" (`'a', 'b', or 'c'`). Not used for the `Suggestion` field, which
stays a plain, unquoted list.

**PosLabel**: Renders a `PartOfSpeech` value for message/suggestion text, rendering `Any` as
"general".

#### Error Handling

`Evaluate` propagates `ArgumentNullException` for null input arguments. Regex matching uses a
one-second timeout and no local exception handling.

#### Dependencies

- **LintDictionary** - supplies the effective dictionary entries.
- **PartOfSpeechGuesser** - selects the applicable sense(s) of a multi-sense entry.
- **MarkdownProseExtractor** - supplies segment text and line numbers, and the
  `FindInlineCodeSpans`/`OverlapsInlineCodeSpan` helpers used to exclude inline-code matches.
- **LintConfig** - supplies the `LintMode` forwarded to `PartOfSpeechGuesser`.
- **Diagnostic** and **Severity** - encode the reported finding.
- **.NET BCL** - regex construction and matching.

#### Callers

- **Linter** - invokes the dictionary check for each linted file after prose extraction.
