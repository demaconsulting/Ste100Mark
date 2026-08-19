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
**QuantifiersOrDemonstratives**, **Prepositions**, **FiniteVerbForms**,
**ClauseBreakingWords**, **CommonAdjectiveModifiers**: closed keyword sets used by the
noun/verb signal rules below.

**MaxModifierScanDistance**: `const int` (3) - maximum number of tokens `GoverningDeterminer`
scans backward past modifiers before giving up on finding a governing determiner.

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
punctuation), `governingWord` (see `GoverningDeterminer` below), `followingWord` (the first
token strictly after the match, same normalization as `precedingWord`), and `isSentenceStart`
(the match begins the segment, or immediately follows the same `[.!?:]` + whitespace boundary
condition as `SentenceAnalyzer`):

| Category | Signal | Condition |
| --- | --- | --- |
| Verb | ImperativeSentenceStart | `isSentenceStart` and `mode == Procedure` |
| Verb | InfinitiveMarker | `precedingWord == "to"` |
| Verb | ModalAuxiliary | `precedingWord` is in `ModalAuxiliaries` |
| Verb | ProgressiveAuxiliary | `precedingWord` is in `BeAuxiliaries` and `matchText` ends with `ing` |
| Verb | VerbInflectionSuffix | `matchText` ends with `ed` or `ing` |
| Verb | FollowedByArticle | `followingWord` is in `Articles` (direct-object noun phrase, e.g. "utilize the tool") |
| Noun | Article | `precedingWord` is in `Articles` |
| Noun | Possessive | `precedingWord` is in `PossessivePronouns`, or ends with `'s` |
| Noun | QuantifierOrDemonstrative | `precedingWord` is in `QuantifiersOrDemonstratives` |
| Noun | Preposition | `precedingWord` is in `Prepositions` |
| Noun | DeterminerGovernsThroughModifiers | `governingWord` is a determiner/possessive/quantifier/preposition |
| Noun | PluralNounSuffix | `matchText` ends with `s` and not `ss` |
| Noun | NounPhraseContinuation | `followingWord == "of"` |
| Noun | FollowedByFiniteVerb | `followingWord` is a modal/`"to be"` auxiliary or in `FiniteVerbForms` |
| Noun | VerblessSegment | no other Verb-category signal fired anywhere in `segmentText` (see `HasAnyFiniteVerb`) |

See **Data Model** above for the exact word lists behind `ModalAuxiliaries`, `BeAuxiliaries`,
`Articles`, `PossessivePronouns`, `QuantifiersOrDemonstratives`, `Prepositions`, and
`FiniteVerbForms`.

The Verb category is itself split internally into two tiers so the whole-segment
`VerblessSegment` noun signal cannot silently out-vote a strong, match-local verb signal:
`HasOtherVerbSignal` evaluates `InfinitiveMarker`, `ModalAuxiliary`, `ProgressiveAuxiliary`,
`VerbInflectionSuffix`, and `FollowedByArticle` (all anchored to the match itself); the
mode-dependent `ImperativeSentenceStart` signal is evaluated separately in `Guess`. `HasNounSignal`
only evaluates `VerblessSegment` when `HasOtherVerbSignal` did *not* fire, so a confidently-verb
match (for example "to wash" or "utilize the tool") is never contradicted by the absence of a
recognized finite verb elsewhere in the segment. The weaker `ImperativeSentenceStart` signal, by
contrast, may still conflict with `VerblessSegment` (both fire, `Guess` returns `null`) - for
example an imperative list item with no other finite verb resolves as ambiguous rather than
silently becoming `Noun`, since `Guess`'s conflict rule (`null` when both categories fire) is the
safe default.

**HasAnyFiniteVerb**: Determines whether any whitespace-delimited token anywhere in
`segmentText` looks like a finite verb form (`IsFiniteVerbForm`: a modal/`"to be"` auxiliary, or
a closed-class third-person-singular `FiniteVerbForms` entry). Used only by the `VerblessSegment`
noun signal: a segment - typically a table cell, list item, or heading fragment - containing no
finite verb anywhere cannot be using any of its words as a finite verb, so a verb-only dictionary
entry matched within it must be a noun usage (for example "wash" in "Wash pump", or "probe" in
"Probe geometry and diameter."). An entry with a noun or adjective sense still matches normally
in the same segment (for example "arrangement" in "Drive arrangement for the wash pump" is still
reported), since the discriminator is the *entry's* available senses, not the segment kind.

**GoverningDeterminer**: Scans backward from a match, past a bounded run of
adjective/participle/proper-noun modifier tokens, to find a determiner/possessive/quantifier/
preposition that still governs the match as the head noun of a compound noun phrase (for
example "the *metering* probe", "the *Vantage* probe", or "a *custom* probe" - the italicized
word is the modifier between the determiner and the match).

- *Parameters*: `string segmentText`, `int matchIndex` - same meaning as in `Guess`.
- *Returns*: `string?` - the normalized governing word, or `null` when no determiner is found
  within `MaxModifierScanDistance` tokens, or an ordinary (non-modifier) word is reached first.
- *Postconditions*: Scans up to `MaxModifierScanDistance` tokens backward. At each token: if it
  is a determiner/possessive/quantifier/preposition, that word is returned immediately. Otherwise,
  if it does not look like a modifier (see `LooksLikeModifier`), the scan stops and returns `null`
  - an ordinary word (for example "system" in "The system shall...") breaks the determiner's
  reach, so it is never mistaken for governing a later word.

**LooksLikeModifier**: Determines whether a token looks like a plausible noun-phrase modifier a
determiner could still govern through, rather than an ordinary word that would break the
chain. Returns `true` for: a participle (`-ing`/`-ed` suffix, for example "metering"/"coated"),
a closed-class adjective in `CommonAdjectiveModifiers` (for example "custom", "manual"), or a
word capitalized mid-sentence (a proper-noun modifier, for example a product name in "the
Vantage probe"). Returns `false` for clause-boundary words (`ClauseBreakingWords`), auxiliary/
modal verbs, and the infinitive marker "to", as well as any other ordinary lower-case word - this
is deliberately conservative, so a sentence like "The system shall report..." does not let "the"
reach past the ordinary noun "system" to govern "shall".

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

- **DictionaryChecker** - calls `Guess` once per match of a dictionary entry to select the
  applicable sense(s).
