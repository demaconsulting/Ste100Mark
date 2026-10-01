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

**Citations**: alongside the human-readable `Suggestion` string, every `STE100-DICT` diagnostic
with at least one real alternative term also carries a `Citations` list of
`DictionaryCitation(Term, Pos)` records - one entry per alternative, each paired with the
grammatical role (`Pos`, via `PosLabel`) it is approved in. This is additive: `Suggestion` is
unchanged and still the plain-text form used by text-mode CLI output; `Citations` gives callers
(for example an integrating tool that must not mistake a dictionary citation for replacement
prose) the same information as structured data instead of a formatted string. A diagnostic with
no real alternative to cite (a purely self-referential sense, or a sense with no alternatives at
all) leaves `Citations` `null`, matching the cases where `Suggestion` is likewise empty or
role-restriction prose rather than a word list.

**Pattern shape**: each term is converted into a whole-word/whole-phrase regex using
negative lookbehind/lookahead for `\w` and `-`, with spaces widened to `\s+`.

**Sense selection**: every match (including single-sense terms) is resolved by calling
`PartOfSpeechGuesser.GuessEffective` with the segment text and match position. A confident
`Noun` or `Verb` guess narrows the candidate senses to those matching that role (plus any
`Any`-pos sense); when the guess is confident but matches no sense in the schema, the term is
not being used in a role ASD-STE100 restricts here, and no diagnostic is reported at all. An
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
flagged. This lets a project declare a specific approved phrase (for example "trail mix" as the
approved name of a thing) without silently permitting the disallowed word ("mix") on its own.

**Inline code exclusion**: matches falling wholly inside an inline code span (per
`MarkdownProseExtractor.FindInlineCodeSpans`/`OverlapsInlineCodeSpan`) are ignored before a
diagnostic is built, so a disallowed term that appears only inside inline code (for example, a
CLI flag written as `` `utilize-flag` ``) is not flagged; the same term appearing outside a
code span in the same segment is still flagged normally.

**Table-header exclusion**: a segment whose `Role` is `SegmentRole.TableHeader` is skipped in
its entirety, before any term matching runs. Table column header cells (for example "Hazard" or
"Use") are short labels, not prose sentences, and are routinely a bare noun/verb-looking word
out of any sentence context - the same reasoning `StructuralRules.EvaluateIngForm` applies to
headings and table headers for the `-ing`-form advisory.

**Admonition-label and quoted/mention exclusion**: matches falling wholly inside a bold
admonition-label span at the start of a block (`MarkdownProseExtractor.FindAdmonitionLabelSpans`,
for example `**Caution.**`) or inside a double-quoted or single-emphasis span
(`MarkdownProseExtractor.FindQuotedOrEmphasisSpans`, for example a quoted or cited document
title) are ignored before a diagnostic is built. A label is not a prose sentence, and a word
inside a quotation or cited title is a *mention* of that exact text, not a *use* of the word in
this document's own prose, so neither is flagged.

**Self-referential alternative filtering**: `FilterSelfAlternatives` removes the exact matched
surface form (case-insensitively) from a sense's alternatives before any suggestion or citation
is built, so a suggestion never tells the user to replace a word with itself (for example a
sense whose alternatives happen to include an inflected form matching the match text). This is
distinct from, and does not replace, `IsSelfReferentialSense`/`IsPureSelfReferentialSense` below,
which compare against the dictionary entry's own headword (`DictionaryEntry.Term`) for
role-restriction detection - the matched surface form (for example an inflected "running") can
differ from the entry's headword (for example "run"), so both comparisons are needed.

#### Key Methods

**Evaluate**: Scans every extracted prose segment against the merged dictionary.

- *Parameters*: `string file` - file path for diagnostics; `IReadOnlyList<ProseSegment> segments`
  - prose segments; `LintDictionary dictionary` - merged dictionary; `LintMode mode` - the
  file's resolved writing mode, forwarded to `PartOfSpeechGuesser.GuessEffective`;
  `IReadOnlyCollection<string>? extraAllowedTerms` - optional additional per-file allowed
  terms (typically `LintConfig.ResolveAllowedTerms`), defaulting to `null` (no per-file
  allowance); `IReadOnlyCollection<string>? allowedPhrases` - optional phrase-scoped
  allowances (typically `LintConfig.ResolveAllowedPhrases`), defaulting to `null` (no
  phrase-scoped allowance).
- *Returns*: `IReadOnlyList<Diagnostic>` - one diagnostic per matched occurrence, excluding
  matches suppressed by a confident-but-non-matching POS guess, by `extraAllowedTerms`, or by
  falling wholly inside an `allowedPhrases`, admonition-label, or quoted/emphasis span
  occurrence.
- *Preconditions*: `file`, `segments`, and `dictionary` are non-null.
- *Postconditions*: A `TableHeader`-role segment is skipped entirely before matching. Matches
  are returned in segment order, excluding any match that falls wholly inside an inline code
  span, an allowed-phrase occurrence, an admonition-label span, or a quoted/emphasis span;
  every diagnostic uses severity `Error`, rule code `STE100-DICT`, and a suggestion string
  when alternatives (after self-referential filtering) are present.

**FindAllowedPhraseSpans**: Locates every occurrence of every configured `allowedPhrases` entry
within a segment's text, using the same case-insensitive, whitespace-tolerant pattern shape as
a multi-word `Disallow` term, returning the spans `Evaluate` tests each dictionary-term match
against.

**BuildDiagnostic**: Selects the applicable sense(s) for one match and builds its diagnostic,
or returns `null` when a confident POS guess rules out every sense (the term is not
disallowed in the grammatical role it is being used in here).

**FilterSelfAlternatives**: Removes the exact matched surface form (case-insensitive) from a
sense's alternatives list.

- *Parameters*: `IReadOnlyList<string> alternatives` - the sense's configured alternatives;
  `string matchedWord` - the exact surface form matched in the segment text.
- *Returns*: `IReadOnlyList<string>` - `alternatives` with the matched word removed.
- *Postconditions*: Used by `ConfidentDiagnostic`, `AmbiguousDiagnostic`, and
  `CorrectionClause` before any suggestion/citation is built, so a diagnostic never suggests
  replacing the flagged word with itself.

**IsPureSelfReferentialSense**: Determines whether a sense's *only* alternative is the entry's
own headword (for example `test (v) -> TEST`), as distinct from a sense whose alternatives
include the headword *alongside* other genuine replacement words (for example
`check (v) -> MAKE SURE, MEASURE, EXAMINE, CHECK`). Used by `ConfidentDiagnostic` and
`CorrectionClause` to avoid a nonsensical "avoid 'test'; use 'test' instead" word-swap message
when there is no actual word to swap - the real correction is a change of grammatical role.

**ConfidentDiagnostic**: Builds a diagnostic for a single resolved sense (single-sense term, or
the sole surviving candidate of a multi-sense term), optionally labeling the sense's `Pos` in
the message. When the sense is purely self-referential (see `IsPureSelfReferentialSense`), the
message is phrased as a role restriction instead - `"Avoid using '{term}' as a {pos}; ASD-STE100
approves '{term}' only in a different grammatical role."` - with a matching `Suggestion` telling
the user to rewrite the sentence rather than swap words, and no `Citations` (there is no
alternative term to cite). Otherwise, when the sense has one or more alternatives, they are
folded into the message itself via `JoinAlternatives` (for example `"use 'effect' instead"` or
`"use 'cause', 'give', 'make', or 'supply' instead"`), and `Citations` is populated with one
entry per alternative, each paired with the sense's `Pos` (via `PosLabel`); a sense with no
alternatives falls back to the generic "it is not an approved ASD-STE100-style term" wording and
leaves `Citations` `null`. The separate `Suggestion` field for the non-self-referential path is
unaffected and remains a plain, comma-separated list of alternatives.

**AmbiguousDiagnostic**: Builds a diagnostic listing every candidate sense, labeled as
ambiguous, when the heuristic could not confidently resolve one sense. The message has the
shape `"Ambiguous part of speech for '{term}' — possible corrections: {clauses}."`, where each
candidate sense's clause is produced by `CorrectionClause` (see below), joined across senses
with `"; "`. Both the `Suggestion` string and `Citations` list are built from the same filtered
set of candidates - those that are both non-self-referential and have at least one alternative;
a purely self-referential candidate has no real alternative word to suggest, and a candidate
with no alternatives at all (disallowed in that role with no suggested replacement) is likewise
excluded from both, rather than contributing a stray leading-space/bare-part-of-speech fragment
(for example `" (adjective)"`) to `Suggestion` - its role restriction is still surfaced via
`CorrectionClause` in the message text. `Suggestion` renders the filtered candidates as
`"{alternatives} ({pos}); ..."`; `Citations` contains one entry per alternative term across the
same candidates, each paired with its sense's `Pos`. When no candidate survives the filter,
`Citations` is `null` (matching an empty `Suggestion`).

**CorrectionClause**: Renders one candidate sense's clause for `AmbiguousDiagnostic`'s message.
A purely self-referential sense (see `IsPureSelfReferentialSense`) renders as
`"as a {pos}, only a different grammatical role is approved"`; otherwise it renders as
`"as a {pos}, use {alternatives}"` (via `JoinAlternatives`), or just `"as a {pos}"` when the
sense has no alternatives.

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
- **PartOfSpeechGuesser** - `GuessEffective` selects the applicable sense(s) of a multi-sense
  entry.
- **MarkdownProseExtractor** - supplies segment text, line numbers, and the `TableHeader`
  segment role, plus the `FindInlineCodeSpans`/`OverlapsInlineCodeSpan`,
  `FindAdmonitionLabelSpans`, and `FindQuotedOrEmphasisSpans` helpers used to exclude matches.
- **LintConfig** - supplies the `LintMode` forwarded to `PartOfSpeechGuesser`, and the
  per-file `extraAllowedTerms`/`allowedPhrases` via `ResolveAllowedTerms`/`ResolveAllowedPhrases`.
- **Diagnostic** and **Severity** - encode the reported finding.
- **.NET BCL** - regex construction and matching.

#### Callers

- **Linter** - invokes the dictionary check for each linted file after prose extraction.
