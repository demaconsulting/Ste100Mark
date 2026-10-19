### PartOfSpeechGuesser

![Linting Structure](LintingView.svg)

#### Purpose

`PartOfSpeechGuesser` is a lightweight, deterministic, regex/rule-based heuristic used only by
the `STE100-ADV-INGFORM` advisory in `StructuralRules`. It decides whether a word ending in
`-ing` is a present-participle verb (the advisory flags it) or a gerund, material noun, or
participial adjective (the advisory does not flag it).

Dictionary-term part-of-speech selection is no longer performed here; `PartOfSpeechResolver`
does that using the WordSense tagger. This unit is retained for one reason: a word-level
tagger tags a gerund that has a direct object (for example "before testing the gauge") as a
verb, but the advisory must treat that gerund as not flagged. The rule-based heuristic makes
that distinction, so replacing it with the tagger caused six `-ing` form regression tests to
fail. This is not a grammatical guarantee; it is a signal-counting heuristic that returns only
`Noun`, `Verb`, or `null` (inconclusive).

#### Data Model

**RegexTimeout**: one-second timeout applied to every compiled pattern used by this class.

**SentenceBoundaryRegex**: narrow subset of `SentenceAnalyzer`'s sentence split, answering
only "does a new sentence start immediately after this point" (`[.!?:]` followed by
whitespace).

**LeadingPunctuationRegex**, **TrailingPunctuationRegex**: strip leading/trailing punctuation
from a token before keyword comparison.

**ModalAuxiliaries**, **BeAuxiliaries**, **Articles**, **PossessivePronouns**,
**QuantifiersOrDemonstratives**, **Prepositions**, **FiniteVerbForms**,
**ClauseBreakingWords**, **CommonAdjectiveModifiers**: closed keyword sets used by the
signal rules below.

**ObjectPronouns**: personal/object pronouns (`it`, `them`, `him`, and so on) that signal a
transitive object after an `-ing` word.

**CatenativeVerbs**: catenative verbs that commonly take a gerund complement (for example
`continue`, `keep`, `start`, `finish`). An `-ing` word immediately after one of these is a
gerund complement, not a finite verb.

**MaxModifierScanDistance**: `const int` (3) - maximum number of tokens `GoverningDeterminer`
scans backward past modifiers before giving up on finding a governing determiner.

#### Key Methods

**GuessIngFormRole**: Resolves whether a word matching `STE100-ADV-INGFORM`'s `-ing` pattern
is being used as a present-participle verb (should be flagged) or as a noun/adjective (gerund,
material noun, or participial adjective - should not be flagged).

- *Parameters*: `string segmentText` - full prose text of the segment containing the match;
  `int matchIndex` - 0-based character offset of the match; `int matchLength` - length of the
  matched span.
- *Returns*: `PartOfSpeech?` - `Verb` when genuine progressive/catenative/transitive-object
  evidence is found with no conflicting noun evidence; `Noun` when noun evidence is found (or
  no finite verb exists anywhere in the segment, or the match is sentence/list-item/heading
  initial); `null` (treated as not-flagged, the same as `Noun`) when inconclusive.
- *Preconditions*: `segmentText` is not null.
- *Postconditions*: A confident `Verb` result requires: preceded by a
  `BeAuxiliaries`/`ModalAuxiliaries` word (progressive/modal), or followed by an
  `Articles`/`ObjectPronouns` word or a number (transitive object, for example "closing the
  valve"). Otherwise, `Noun` is returned when: preceded by an article/possessive/quantifier/
  preposition (including "to" - there is no true "to testing" infinitive, so "to" + `-ing`
  always governs a preposition-object gerund)/`CatenativeVerbs` entry,
  `GoverningDeterminer` finds a governing determiner, followed by `"of"`, followed by a word
  that looks like a noun it modifies (a participial-adjective use, for example "moving
  structure"), the match is sentence/list-item/heading-initial, or no finite verb exists
  anywhere in the segment. Everything else defaults to `null` (not flagged), matching this
  feature's intent of reducing `-ing`-form false positives.

**HasAnyFiniteVerb**: Determines whether any token in `segmentText` looks like a finite verb
form (a modal/`"to be"` auxiliary, or a closed-class third-person-singular `FiniteVerbForms`
entry). A segment with no finite verb (a table cell, list item, or heading fragment) cannot
use its `-ing` word as a finite verb.

**GoverningDeterminer**: Scans backward from a match, past a bounded run of
adjective/participle/proper-noun modifier tokens, to find a determiner/possessive/quantifier/
preposition that still governs the match as the head of a noun phrase (for example "the
*metering* probe"). It scans up to `MaxModifierScanDistance` tokens and stops at the first
ordinary non-modifier word, returning `null`.

**LooksLikeModifier**: Determines whether a token is a plausible noun-phrase modifier a
determiner could still govern through: a participle (`-ing`/`-ed` suffix), a closed-class
adjective in `CommonAdjectiveModifiers`, or a word capitalized mid-sentence. Clause-boundary
words, auxiliaries, the infinitive marker "to", and any other ordinary lower-case word are not
modifiers.

#### Error Handling

`GuessIngFormRole` propagates `ArgumentNullException` for a null `segmentText`. Regex timeouts
are bounded to one second per pattern match. No exceptions are caught locally.

#### Dependencies

- **.NET BCL** - `Regex` support only.

#### Callers

- **StructuralRules** - `EvaluateIngForm` calls `GuessIngFormRole` to resolve whether a matched
  `-ing` word is a genuine present-participle verb use.
