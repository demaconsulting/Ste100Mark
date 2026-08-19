### PartOfSpeechGuesser

![Linting Structure](LintingView.svg)

#### Purpose

`PartOfSpeechGuesser` is a lightweight, deterministic, regex/rule-based heuristic that guesses
whether a matched dictionary term is being used as a noun or a verb at a specific point in
prose, so that `DictionaryChecker` can select the correct part-of-speech-tagged sense's
alternative(s) from a multi-sense `DictionaryEntry`.

This is not a grammatical guarantee. It is a lightweight signal-counting heuristic, consistent
with this codebase's dependency-light approach (compare `SentenceAnalyzer`'s own regex-based,
non-NLP sentence splitting). It only ever returns `Noun`, `Verb`, or `null` (inconclusive) - it
does not attempt to positively detect `Adjective` or `Adverb`, because no reliable lightweight
signal for those roles exists. Entries with only adjective/adverb senses always resolve as
ambiguous via the "no signals fired" path, which is the correct conservative behavior for those
cases.

#### Data Model

**RegexTimeout**: one-second timeout applied to every compiled pattern used by this class.

**SentenceBoundaryRegex**: narrowly-scoped subset of `SentenceAnalyzer`'s own
`SentenceSplitRegex`, answering only "does a new sentence start immediately after this point in
the text" (`[.!?:]` followed by whitespace, at the end of the text preceding the match).

**LeadingPunctuationRegex**, **TrailingPunctuationRegex**: strip leading/trailing punctuation
from a token before keyword comparison.

**ModalAuxiliaries**, **BeAuxiliaries**, **Articles**, **PossessivePronouns**,
**QuantifiersOrDemonstratives**, **Prepositions**: closed keyword sets used by the noun/verb
signal rules below.

#### Key Methods

**Guess**: Guesses the grammatical role of one dictionary-term match within its surrounding
segment text.

- *Parameters*: `string segmentText` - full prose text of the segment containing the match;
  `int matchIndex` - 0-based character offset of the match; `int matchLength` - length of the
  matched span; `LintMode mode` - the file's resolved writing mode, used only for the
  imperative-sentence-start signal, which applies exclusively in `Procedure` mode.
- *Returns*: `PartOfSpeech?` - `Noun` or `Verb` when exactly one category of signal fired;
  `null` when signals conflict (both fired) or none fired.
- *Preconditions*: `segmentText` is not null.
- *Postconditions*: Signal categories are evaluated as sets, not first-match priority - any
  single fired signal in a category is sufficient to mark that category "hit". Exactly one
  category hit resolves confidently; zero or two categories hit is ambiguous.

The exact rule set, evaluated against `matchText` (the matched surface form), `precedingWord`
(the last whitespace-delimited token strictly before the match, lower-cased and stripped of
punctuation), `followingWord` (the first token strictly after the match, same normalization),
and `isSentenceStart` (the match begins the segment, or immediately follows the same
`[.!?:]` + whitespace boundary condition as `SentenceAnalyzer`):

| Category | Signal | Condition |
| --- | --- | --- |
| Verb | ImperativeSentenceStart | `isSentenceStart` and `mode == Procedure` |
| Verb | InfinitiveMarker | `precedingWord == "to"` |
| Verb | ModalAuxiliary | `precedingWord` is in `ModalAuxiliaries` |
| Verb | ProgressiveAuxiliary | `precedingWord` is in `BeAuxiliaries` and `matchText` ends with `ing` |
| Verb | VerbInflectionSuffix | `matchText` ends with `ed` or `ing` |
| Noun | Article | `precedingWord` is in `Articles` |
| Noun | Possessive | `precedingWord` is in `PossessivePronouns`, or ends with `'s` |
| Noun | QuantifierOrDemonstrative | `precedingWord` is in `QuantifiersOrDemonstratives` |
| Noun | Preposition | `precedingWord` is in `Prepositions` |
| Noun | PluralNounSuffix | `matchText` ends with `s` and not `ss` |
| Noun | NounPhraseContinuation | `followingWord == "of"` |

See **Data Model** above for the exact word lists behind `ModalAuxiliaries`, `BeAuxiliaries`,
`Articles`, `PossessivePronouns`, `QuantifiersOrDemonstratives`, and `Prepositions`.

`hasVerb` is true when any Verb-category signal fired; `hasNoun` is true when any Noun-category
signal fired. `Guess` returns `Verb` when `hasVerb && !hasNoun`, `Noun` when
`hasNoun && !hasVerb`, and `null` otherwise (both or neither fired).

Multi-word terms (for example "in order to") are expected to be single-sense `pos: any`
entries, since no POS distinction is detectable or needed for connector phrases - this
heuristic is only meaningfully exercised for single-word multi-sense terms.

#### Error Handling

`Guess` propagates `ArgumentNullException` for a null `segmentText`. Regex timeouts are bounded
to one second per pattern match. No exceptions are caught locally.

#### Dependencies

- **LintConfig** - supplies the `LintMode` enum consumed by the imperative-sentence-start
  signal.
- **.NET BCL** - `Regex` support only.

#### Callers

- **DictionaryChecker** - calls `Guess` once per match of a multi-sense dictionary entry to
  select the applicable sense(s).
