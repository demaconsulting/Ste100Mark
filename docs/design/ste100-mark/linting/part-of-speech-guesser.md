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

**ObjectPronouns**: `HashSet<string>` of personal/object pronouns (`it`, `them`, `him`, etc.)
that are never the head noun of a noun-noun compound, used by `LooksLikeCompoundNoun` and by
`GuessIngFormRole`'s transitive-object signal.

**OtherAuxiliaryVerbs**: `HashSet<string>` of "do"/"have"-family auxiliary verb forms (`do`,
`does`, `did`, `has`, `have`, `had`). Combined with `ModalAuxiliaries` and `BeAuxiliaries`,
excludes the matched word itself from the `FollowedByNegation` noun signal, so an auxiliary
verb immediately followed by "not"/"never" (for example "shall not", "does not") remains
verbal rather than being misread as a noun.

**CompoundNounHeadAllowList**: `HashSet<string>` of common, unambiguous technical-writing
plural nouns (for example `modules`, `units`, `sensors`, `valves`) that are safe to recognize
as the head of a noun-noun compound (for example "backup modules") even though their spelling
also ends in a bare "-s" - otherwise deliberately excluded by `LooksLikeCompoundNoun` as too
ambiguous with 3rd-person-singular verb forms. A closed, curated allow-list rather than a
blanket relaxation; it also deliberately omits any word that, like `cycles`, is also an
ordinary finite verb (see `FiniteVerbForms` below), to avoid overriding a genuine imperative
verb use.

**FiniteVerbForms**: `HashSet<string>` of closed-class, third-person-singular-present finite
verb forms (for example "moves", "has", "operates") signaling the preceding match is the
subject of a clause. Deliberately omits any such word that is also a common technical-writing
plural noun (for example "results", "checks", "controls"), so a following plural-noun object
cannot make the guesser misread a preceding subject match as a noun.

**ClauseBreakingWords**: `HashSet<string>` of words that end a determiner's reach through
modifiers (conjunctions, clause markers), used by `GoverningDeterminer`/`LooksLikeModifier`.

**NonCompoundHeadWords**: `HashSet<string>` of negation/adverbial words (`not`, `never`,
`also`, `already`, `still`, `just`, `often`) that cannot be the head noun of a compound, used
by `LooksLikeCompoundNoun`/`LooksLikeUnitWord`.

**CatenativeVerbs**: `HashSet<string>` of catenative verbs that commonly take a gerund as
their complement (for example `continue`, `keep`, `start`, `finish`). Used only by
`GuessIngFormRole`: an `-ing` word immediately following one of these is a gerund complement,
not a finite verb in its own right.

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
  category hit resolves confidently; zero or two categories hit is ambiguous. Treats the
  strong and weak tiers of each part of speech identically (a plain OR of both).

**GuessEffective**: An additive refinement of `Guess` that only ever narrows an otherwise
ambiguous (`null`) result.

- *Parameters*: same as `Guess`.
- *Returns*: `PartOfSpeech?` - when `Guess` itself resolves a role, that result is returned
  unchanged. Otherwise, promotes to a role when exactly one of the two *strong* signal
  categories (`StrongVerb`, `StrongNoun`) fired, ignoring the two weaker categories
  (`WeakVerb`/`ImperativeSentenceStart` and `WeakNoun`/`PluralNounSuffix`+`VerblessSegment`)
  that would otherwise have produced the conflict.
- *Postconditions*: Use this overload (rather than `Guess`) wherever a confident role is
  actionable from a single strong signal category alone - `DictionaryChecker`'s per-occurrence
  sense selection uses `GuessEffective`, not `Guess`.

The exact rule set, evaluated against `matchText` (the matched surface form), `precedingWord`
(the last whitespace-delimited token strictly before the match, lower-cased and stripped of
punctuation), `governingWord` (see `GoverningDeterminer` below), `followingWord` (the first
token strictly after the match, same normalization as `precedingWord`), and `isSentenceStart`
(the match begins the segment, or immediately follows the same `[.!?:]` + whitespace boundary
condition as `SentenceAnalyzer`):

| Category | Signal | Condition (abbreviated - see notes below) |
| --- | --- | --- |
| Verb (strong) | InfinitiveMarker | `precedingWord == "to"` |
| Verb (strong) | ModalAuxiliary | `precedingWord` is in `ModalAuxiliaries` |
| Verb (strong) | ProgressiveAuxiliary | `precedingWord` is a `BeAuxiliaries` word, `matchText` ends with `ing` |
| Verb (strong) | VerbInflectionSuffix | `matchText` ends with `ed` or `ing` |
| Verb (strong) | FollowedByArticle | `followingWord` is in `Articles` |
| Verb (strong) | FollowedByQualifiedNumber | `followingWord` is a "qualified" number |
| Verb (weak) | ImperativeSentenceStart | `isSentenceStart` and `mode == Procedure` |
| Noun (strong) | Article | `precedingWord` is in `Articles` |
| Noun (strong) | Possessive | `precedingWord` is in `PossessivePronouns`, or ends with `'s` |
| Noun (strong) | QuantifierOrDemonstrative | `precedingWord` is in `QuantifiersOrDemonstratives` |
| Noun (strong) | Preposition | `precedingWord` is in `Prepositions` |
| Noun (strong) | DeterminerGovernsThroughModifiers | `governingWord` is a determiner/possessive/etc. |
| Noun (strong) | NounPhraseContinuation | `followingWord == "of"` |
| Noun (strong) | FollowedByFiniteVerb | `followingWord` is an auxiliary or `FiniteVerbForms` word |
| Noun (strong) | NounCompoundModifier | `followingWord` looks like a compound-noun head |
| Noun (strong) | FollowedByNegation | `followingWord` is `"not"`/`"never"`, match is not an auxiliary |
| Noun (strong) | FollowedByBareIdentifier | `followingWord` looks like an unqualified bare integer |
| Noun (weak) | PluralNounSuffix | `matchText` ends with `s` and not `ss`, not an auxiliary |
| Noun (weak) | VerblessSegment | no strong Verb-category signal fired anywhere in segment |

See **Data Model** above for the exact word lists behind `ModalAuxiliaries`, `BeAuxiliaries`,
`Articles`, `PossessivePronouns`, `QuantifiersOrDemonstratives`, `Prepositions`,
`FiniteVerbForms`, `ObjectPronouns`, `OtherAuxiliaryVerbs`, and `CompoundNounHeadAllowList`.

A few conditions above are abbreviated and need further detail: `FollowedByArticle` requires a
direct-object noun phrase (e.g. "utilize the tool"); `FollowedByQualifiedNumber` requires either
a decimal point (e.g. "use 0.12") or a following unit abbreviation (e.g. "set 5 volts");
`DeterminerGovernsThroughModifiers` requires `governingWord` to be a determiner, possessive,
quantifier, or preposition; `FollowedByFiniteVerb` requires `followingWord` to be a modal/
`"to be"` auxiliary or a `FiniteVerbForms` entry (subject position); `NounCompoundModifier`
requires `followingWord` to look like a compound-noun head (see `LooksLikeCompoundNoun`) with no
strong Verb signal fired (e.g. "test fixture", "backup modules"); `FollowedByNegation` requires
`matchText` to not itself be a modal/`"to be"`/`OtherAuxiliaryVerbs` auxiliary (e.g. "Trigger not
issued" but not "shall not"); `FollowedByBareIdentifier` requires no strong Verb signal fired
(e.g. "Block 0").

Every signal is evaluated into one of four tiers (`GuessSignals`: `StrongVerb`, `StrongNoun`,
`WeakVerb`, `WeakNoun`), computed once per match by `ComputeSignals` and shared by both
`Guess` and `GuessEffective`. The Verb category is split so the whole-segment `VerblessSegment`
noun signal cannot silently out-vote a strong, match-local verb signal: `HasOtherVerbSignal`
evaluates every strong-tier Verb signal above (all anchored to the match itself); the
mode-dependent `ImperativeSentenceStart` signal is the sole weak-tier Verb signal. `HasNounSignal`
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

**LooksLikeCompoundNoun**: Determines whether a following word looks like the head noun of a
noun-noun compound (for example "fixture" in "test fixture", "cycle" in "duty cycle") rather
than a verb, function word, or number, backing the `NounCompoundModifier` signal.

- *Parameters*: `string followingWord`.
- *Returns*: `bool`.
- *Postconditions*: An entry in `CompoundNounHeadAllowList` returns `true` immediately,
  overriding the general "ends in 's'" exclusion below. Otherwise, articles, possessives,
  quantifiers, prepositions, clause-breaking words, modal/`"to be"` auxiliaries,
  `NonCompoundHeadWords`, and `ObjectPronouns` all return `false`; a finite-verb form, or a
  word ending in `-ing`/`-ed`/`-ly`/`s`, also returns `false` (deliberately conservative - any
  word ending in `s` is excluded, not just recognized finite-verb forms, since it is
  ambiguously either a 3rd-person-singular verb or a plural noun).

**LooksLikeModifier**: Determines whether a token looks like a plausible noun-phrase modifier a
determiner could still govern through, rather than an ordinary word that would break the
chain. Returns `true` for: a participle (`-ing`/`-ed` suffix, for example "metering"/"coated"),
a closed-class adjective in `CommonAdjectiveModifiers` (for example "custom", "manual"), or a
word capitalized mid-sentence (a proper-noun modifier, for example a product name in "the
Vantage probe"). Returns `false` for clause-boundary words (`ClauseBreakingWords`), auxiliary/
modal verbs, and the infinitive marker "to", as well as any other ordinary lower-case word - this
is deliberately conservative, so a sentence like "The system shall report..." does not let "the"
reach past the ordinary noun "system" to govern "shall".

`hasVerb` is true when any Verb-category signal (strong or weak) fired; `hasNoun` is true when
any Noun-category signal (strong or weak) fired. `Guess` returns `Verb` when
`hasVerb && !hasNoun`, `Noun` when `hasNoun && !hasVerb`, and `null` otherwise (both or
neither fired). `GuessEffective` additionally resolves a role from strong-only evidence when
`Guess` itself would have returned `null` (see `GuessEffective` above).

Multi-word terms (for example "in order to") are expected to be single-sense `pos: any`
entries, since no POS distinction is detectable or needed for connector phrases - this
heuristic is only meaningfully exercised for single-word multi-sense terms.

**GuessIngFormRole**: Resolves whether a word matching `STE100-ADV-INGFORM`'s `-ing` pattern
is being used as a present-participle verb (should be flagged) or as a noun/adjective (gerund,
material noun, or participial adjective - should not be flagged).

- *Parameters*: `string segmentText`, `int matchIndex`, `int matchLength` - same meaning as in
  `Guess`, with no `mode` parameter (this method is mode-independent).
- *Returns*: `PartOfSpeech?` - `Verb` when genuine progressive/catenative/transitive-object
  evidence is found with no conflicting noun evidence; `Noun` when noun evidence is found (or
  no finite verb exists anywhere in the segment, or the match is sentence/list-item/heading
  initial); `null` (treated as not-flagged, the same as `Noun`) when inconclusive.
- *Preconditions*: `segmentText` is not null.
- *Postconditions*: This is deliberately a separate resolution path from `Guess`/
  `GuessEffective`: those general-purpose methods treat a bare `-ing` suffix as a strong verb
  signal in its own right, which is circular when the very question being asked is "is this
  `-ing` word a verb" - every candidate ends in `-ing` by construction. A confident `Verb`
  result requires: preceded by a `BeAuxiliaries`/`ModalAuxiliaries` word or "to" (progressive/
  infinitive), or followed by an `Articles`/`ObjectPronouns` word or a number (transitive
  object, e.g. "closing the valve"). Otherwise, `Noun` is returned when: preceded by an
  article/possessive/quantifier/preposition/`CatenativeVerbs` entry (gerund complement, e.g.
  "continue monitoring"), `GoverningDeterminer` finds a governing determiner, followed by
  `"of"`, followed by a word that looks like a noun it modifies (a participial-adjective use,
  e.g. "moving structure"), the match is sentence/list-item/heading-initial, or no finite verb
  exists anywhere in the segment. Everything else defaults to `null` (not flagged), matching
  this feature's intent of reducing `-ing`-form false positives.

#### Error Handling

`Guess` propagates `ArgumentNullException` for a null `segmentText`; `GuessEffective` and
`GuessIngFormRole` do likewise. Regex timeouts are bounded to one second per pattern match. No
exceptions are caught locally.

#### Dependencies

- **LintConfig** - supplies the `LintMode` enum consumed by the imperative-sentence-start
  signal.
- **.NET BCL** - `Regex` support only.

#### Callers

- **DictionaryChecker** - calls `GuessEffective` once per match of a dictionary entry to select
  the applicable sense(s).
- **StructuralRules** - `EvaluateIngForm` calls `GuessIngFormRole` to resolve whether a matched
  `-ing` word is a genuine present-participle verb use.
