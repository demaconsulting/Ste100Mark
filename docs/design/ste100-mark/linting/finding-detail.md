### FindingDetail

![Linting Structure](LintingView.svg)

#### Purpose

`FindingDetail` builds the detail object that the `json-detailed` report attaches to a
dictionary finding. Its single responsibility is to turn the evidence that `DictionaryChecker`
already collected, plus the source lines of the file, into location, sentence, context, match
and tagger evidence, a confidence class, suggestions, and suppression hints. It performs no new
analysis and never changes whether or how a finding is reported.

#### Data Model

**Evidence records** (input, produced by `DictionaryChecker`):

- `DictionaryEvidence` - the prose segment, the span start and length, the flagged text, the
  dictionary term, the form kinds, the split and hyphen flags, whether the spelling is a
  generated word form, every sense, the reported sense label, the number of reported senses,
  the suggestions, the `RoleEvidence` of the tagger, and the outcomes of the participle rule
  and the fragment rule.
- `EvidenceSense` and `EvidenceSuggestion` - one sense (label and alternatives) and one
  suggestion (text, whether it was inflected to the flagged form, and the sense label).

**Detail records** (output, serialized by `DiagnosticReporter`):

- `FindingDetail` - `Span`, `Sentence`, `Context`, `ContextKind`, `Match`, `Tagger`,
  `Confidence`, `Suggestions`, and `Suppression`.
- `FindingSpan` - start and end line, start column and exclusive end column (both `null` when
  not exact), the start and end offsets inside the extracted prose segment, and `ColumnsExact`.
- `FindingSentence` - the sentence text (at most `MaxSentenceLength` = 400 characters) and a
  `Truncated` flag.
- `FindingContext` - the first line number and the source lines (each at most
  `MaxContextLineLength` = 300 characters).
- `FindingMatch` - the flagged text, the dictionary term, the primary `FormKind` (`phrasal-split`,
  `hyphenated`, or the first grammatical form), the grammatical forms (`base`, `third-person`,
  `past`, `participle`, `gerund`, `plural`), the sense label (a part of speech or `ambiguous`), and
  every sense of the entry. The word-form model has no irregular flag, so `irregular` is not a
  form kind.
- `FindingTagger` - the resolved role, the tag, the confidence, whether the threshold was met,
  the threshold (always `PartOfSpeechResolver.MinimumConfidence`), the margin, the runner-up
  tag and probability, the lexicon and glossary flags, the outcome, and the participle and
  fragment rule outcomes. Fields the resolver did not supply are `null`.
- `FindingConfidence` - the class (`certain`, `probable`, `doubtful`) and the reason code.
- `FindingSuggestion` - the suggested text, whether it was inflected to the flagged form, and
  the sense label.
- `FindingSuppression` and `SuppressionOption` - the standing warning and the options. Each
  option has a kind, a configuration snippet and a scope description.

#### Key Methods

**Build**: Builds the detail for one diagnostic.

- *Parameters*: `Diagnostic diagnostic`; `IReadOnlyList<string> sourceLines` - lines of the
  source file (index 0 is line 1); `int contextLines` - lines before and after the finding.
- *Returns*: `FindingDetail?` - `null` when the diagnostic carries no evidence.
- *Preconditions*: `diagnostic` and `sourceLines` are not null.
- *Postconditions*: The result depends only on the arguments.

The span lines come from `ProseSegment.ResolveLine`. The column is found by counting how many
times the flagged text occurs in the segment before the match on the same source line, then
locating that occurrence in the source line; the column is reported only when it is found. A
table row is split into one segment per cell, so occurrences in other cells cannot be counted:
a table column is reported only when the text occurs once in the row. Otherwise
`ColumnsExact` is `false` and only lines are reported. The sentence is the sentence of the
segment that holds the span (using the shared sentence splitter); a longer sentence is cut to a
400-character window around the span. Context lines are clamped to the file and omitted when
`contextLines` is 0. The context kind is `heading`, `table-cell`, `list-item`, `block-quote`
(a paragraph whose source line starts with `>`), or `paragraph`.

**Classify**: Assigns the confidence class and reason by an ordered table; the first matching
row wins.

| Row | Class | Reason | Condition |
| ----- | ------- | -------- | ----------- |
| 1 | doubtful | `role-inconclusive` | No role, and the outcome is neither below-threshold nor unknown-word |
| 2 | doubtful | `multiple-senses` | More than one sense is reported |
| 3 | doubtful | `tagger-below-threshold` | The outcome is below-threshold or unknown-word |
| 4 | probable | `split-phrasal` | A phrasal verb split by other words |
| 5 | probable | `hyphenated-form` | A hyphen-prefixed form |
| 6 | probable | `fragment-with-auxiliary` | A participial form in a heading or cell kept with an auxiliary |
| 7 | probable | `participle-low-margin` | A participial form kept as a confident verb with a margin below 0.50 |
| 8 | certain | `role-confident` | None of the above |

Row 1 is narrowed to a missing role without a below-threshold or unknown-word outcome so that
row 3 stays reachable for the most common weak-tagger cases.

**Suppression hints**: `allow` always (the base term, or the lower-case spelling of a generated
or prefixed form), `allow-in-phrase` when the sentence has a neighbor word (up to two
candidates: previous word with the flagged text, and flagged text with the next word), and
`glossary` only for a generated, non-split, non-hyphenated form with a suggested part of speech
(past and participle give adjective, gerund and plural give noun, third-person gives none),
because the loader rejects a glossary term that is a dictionary entry. A text that is not a
plain YAML scalar is quoted. The standing warning is always present.

#### Error Handling

`Build` and `Classify` throw `ArgumentNullException` for null arguments. Every regular expression
has a one-second timeout. A location that cannot be verified is reported without columns
instead of failing.

#### Dependencies

- **Diagnostic** - carries the `DictionaryEvidence`.
- **MarkdownProseExtractor** - supplies `ProseSegment`, `SegmentRole`, and line resolution.
- **SentenceAnalyzer** - supplies the sentence split pattern.
- **PartOfSpeechResolver** and **IPartOfSpeechResolver** - supply `RoleEvidence` and the
  threshold.
- **.NET BCL** - regular expressions and string handling.

#### Callers

- **Linter** - builds one detail per dictionary finding for the `json-detailed` format.
- **DiagnosticReporter** - serializes the detail objects.
