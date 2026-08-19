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

**Sense selection**: every match (including single-sense terms) is resolved by calling
`PartOfSpeechGuesser.Guess` with the segment text and match position. A confident `Noun` or
`Verb` guess narrows the candidate senses to those matching that role (plus any `Any`-pos
sense); when the guess is confident but matches no sense in the schema, the term is not being
used in a role ASD-STE100 restricts here, and no diagnostic is reported at all. An
inconclusive guess keeps every sense as a candidate. Exactly one surviving candidate is
reported confidently (labeled with the sense's `Pos` in the message only when the entry has
more than one sense, so a single-sense entry's message stays unqualified; `Any` renders as
"general"); more than one surviving candidate is reported as one ambiguous diagnostic listing
every candidate sense.

**Per-file dictionary allowance**: an optional `extraAllowedTerms` collection (typically
`LintConfig.ResolveAllowedTerms` for the file being checked) removes matching entries from
consideration before matching runs, letting a `Profile`'s `dictionary.allow`/`dictionary.ignore`
delta permit a term (for example "shall" for a requirements-documents profile) without
altering the merged `LintDictionary` used for every other file.

**Phrase-scoped allowance**: an optional `allowedPhrases` collection (typically
`LintConfig.ResolveAllowedPhrases`, populated from `dictionary.allow-in-phrase`) is converted
into whole-phrase regex spans (`FindAllowedPhraseSpans`, using the same pattern shape as a
multi-word `Disallow` term) per segment; a term match falling wholly inside one of these spans
is excluded before a diagnostic is built, using the same "falls entirely inside" containment
test (`MarkdownProseExtractor.OverlapsInlineCodeSpan`) as the inline-code-span exclusion below.
Unlike `extraAllowedTerms`, this does not remove the term from consideration project-wide: the
same disallowed word elsewhere in the same segment, outside any listed phrase, is still
flagged. This lets a project declare a specific approved phrase (for example "swish mix" as the
approved name of a thing) without silently permitting the disallowed word ("mix") on its own.

**Inline code exclusion**: matches falling wholly inside an inline code span (per
`MarkdownProseExtractor.FindInlineCodeSpans`/`OverlapsInlineCodeSpan`) are ignored before a
diagnostic is built, so a disallowed term that appears only inside inline code (for example, a
CLI flag written as `` `utilize-flag` ``) is not flagged; the same term appearing outside a
code span in the same segment is still flagged normally.

#### Key Methods

**Evaluate**: Scans every extracted prose segment against the merged dictionary.

- *Parameters*: `string file` - file path for diagnostics; `IReadOnlyList<ProseSegment> segments`
  - prose segments; `LintDictionary dictionary` - merged dictionary; `LintMode mode` - the
  file's resolved writing mode, forwarded to `PartOfSpeechGuesser.Guess`;
  `IReadOnlyCollection<string>? extraAllowedTerms` - optional additional per-file allowed
  terms (typically `LintConfig.ResolveAllowedTerms`), defaulting to `null` (no per-file
  allowance); `IReadOnlyCollection<string>? allowedPhrases` - optional phrase-scoped
  allowances (typically `LintConfig.ResolveAllowedPhrases`), defaulting to `null` (no
  phrase-scoped allowance).
- *Returns*: `IReadOnlyList<Diagnostic>` - one diagnostic per matched occurrence, excluding
  matches suppressed by a confident-but-non-matching POS guess, by `extraAllowedTerms`, or by
  falling wholly inside an `allowedPhrases` occurrence.
- *Preconditions*: `file`, `segments`, and `dictionary` are non-null.
- *Postconditions*: Matches are returned in segment order, excluding any match that falls
  wholly inside an inline code span or an allowed-phrase occurrence; every diagnostic uses
  severity `Error`, rule code `STE100-DICT`, and a suggestion string when alternatives are
  present.

**FindAllowedPhraseSpans**: Locates every occurrence of every configured `allowedPhrases` entry
within a segment's text, using the same case-insensitive, whitespace-tolerant pattern shape as
a multi-word `Disallow` term, returning the spans `Evaluate` tests each dictionary-term match
against.

**BuildDiagnostic**: Selects the applicable sense(s) for one match and builds its diagnostic,
or returns `null` when a confident POS guess rules out every sense (the term is not
disallowed in the grammatical role it is being used in here).

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
- **LintConfig** - supplies the `LintMode` forwarded to `PartOfSpeechGuesser`, and the
  per-file `extraAllowedTerms`/`allowedPhrases` via `ResolveAllowedTerms`/`ResolveAllowedPhrases`.
- **Diagnostic** and **Severity** - encode the reported finding.
- **.NET BCL** - regex construction and matching.

#### Callers

- **Linter** - invokes the dictionary check for each linted file after prose extraction.
