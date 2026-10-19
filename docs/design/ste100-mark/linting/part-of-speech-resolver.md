### PartOfSpeechResolver

![Linting Structure](LintingView.svg)

#### Purpose

`PartOfSpeechResolver` decides whether a matched dictionary term is used as a noun or a verb at
a specific point in prose, so that `DictionaryChecker` can select the correct
part-of-speech-tagged sense of a multi-sense `DictionaryEntry`. It implements the internal
`IPartOfSpeechResolver` interface and delegates the tagging to the `DemaConsulting.WordSense`
OTS library (the `BasicWordSenseModel`). It replaces an earlier regex-based heuristic.

The resolver only ever returns `Noun`, `Verb`, or `null` (inconclusive). Entries with only
adjective or adverb senses therefore resolve as ambiguous, which is the conservative behavior
for those cases.

#### Data Model

**IPartOfSpeechResolver**: internal interface with one method, `PartOfSpeech? Resolve(string
segmentText, int matchIndex, int matchLength)`. `DictionaryChecker` depends on the interface so
tests can inject a scripted or fixed resolver.

**MinimumConfidence**: `internal const double` (0.45). The minimum tagger confidence for a noun
or verb answer to be accepted. See *Threshold selection* below.

**SentenceBoundaryRegex**: shared with `SentenceAnalyzer` (`SentenceSplitRegex`, a single source
of truth): whitespace after `.`, `!`, `?`, or `:` that is followed by an uppercase letter, digit,
quote, opening parenthesis, backtick, asterisk, underscore, or the end of text. A lowercase
continuation (for example `e.g. test the valve`) therefore stays in one sentence. A segment is
split into sentences so a match is classified within its own sentence only.

**TokenRegex**: `[\w'-]+|[^\w\s]` - tokenizes a sentence into words (letters, digits,
apostrophes, hyphens) and single punctuation characters.

**Model**: the `IWordSenseModel` used for tagging. The public constructor creates a
`BasicWordSenseModel`; an internal constructor accepts any model for tests. The model is
in-memory and thread-safe and needs no download.

**Shared**: lazily created process-wide instance, so the model is built once per process.

**Cache**: a `ConcurrentDictionary` of per-sentence `Lazy` token spans and roles (thread-safe
`ExecutionAndPublication`), keyed by sentence text. Hits are served without a lock. Miss management (re-check,
capacity check, eviction and insertion) is serialized by a private lock. In-flight entries are never evicted, so
under concurrent misses the total can temporarily exceed the capacity by at most the number of in-flight entries;
as each classification completes, completed entries other than the one just used are trimmed until the cache is
back within capacity. When a
new sentence would be added at 4096 entries, a single completed entry
(the first one found, never an in-flight one) is evicted to make room and the rest stay cached for reuse. The
classification itself (`Lazy.Value`) runs outside the lock, so different sentences classify in parallel and
single-flight is preserved.
One sentence is therefore classified only once however many dictionary terms it contains and
however many threads ask at the same time. A computation that throws is removed from the cache
so a later call retries.

#### Key Methods

**Resolve**: Resolves the grammatical role of one dictionary-term match.

- *Parameters*: `string segmentText` - full prose text of the segment; `int matchIndex` -
  0-based character offset of the match; `int matchLength` - length of the matched span.
- *Returns*: `PartOfSpeech?` - `Noun` or `Verb` when the tagger confidently chose that part of
  speech for the token that covers the match; `null` otherwise.
- *Preconditions*: `segmentText` is not null; the match lies within the segment text.
- *Postconditions*: The sentence that holds the match is tokenized and classified with a single
  `ClassifyAsync` call. A `WordSense` result with `Details` equal to null (an unknown word) is
  inconclusive. A result whose confidence is below `MinimumConfidence` is inconclusive. A
  `Noun` or `Verb` part of speech maps to the repository's `PartOfSpeech`; every other part of
  speech (adjective, adverb, determiner, and so on) is inconclusive. A match that spans more
  than one token (a multi-word term) is inconclusive. A model that returns a wrong number of
  results is inconclusive.

**GetTags**: Tokenizes one sentence, classifies it (synchronously waiting on the completed
task of the Basic model), maps each result to a role, and caches the outcome.

#### Threshold selection

`MinimumConfidence` was chosen empirically. A temporary harness dumped the tagger's confidence
for the target word of every sentence of the 100-sentence false-positive corpus
(`PartOfSpeechFalsePositiveCorpusTests`) and of 26 additional hard cases
(`PartOfSpeechHardCasesTests`: imperatives, noun-noun compounds, and modals). All 126 sentences
were classified correctly. The lowest confidence of a correct noun or verb answer was 0.4926
(`process` as a verb), followed by 0.4990 (`brace` as a noun). No incorrect noun or verb answer
was observed in that set at any confidence, so within the set the threshold does not change
accuracy. The value 0.45 is set just below the lowest correct confidence, so that no correct
answer in the set is lost while near-random answers are still rejected.
`MinimumConfidence_IsBelowLowestObservedCorrectConfidence` guards that relationship.

The 26 hard cases were authored for this change, because the case list from the original
spike was not recoverable.

#### Known limitations

The tagger is a statistical model and is wrong for some inputs. Wrong answers observed outside
the 126-sentence set are not removable by any threshold that keeps all 126 correct:

- A sentence-initial imperative that looks like a noun phrase ("Test results daily") is read as
  a noun (confidence 0.98).
- A verb followed by a number ("Blocks 1 to 5 use 0.12 ohms", "set 0.12 ohms") is read as a
  noun (0.73 and 0.4987). "set 5 V" is a verb at 0.40 and is dropped by the threshold.
- Some noun-modifier uses are read as adjectives (for example "signal" in "signal cable") and
  are therefore inconclusive.

When the answer is wrong in the noun direction, a verb use of a verb-only entry is not
reported; when it is inconclusive, every sense is reported as ambiguous. The mode (Procedure or
Descriptive) no longer influences the part of speech.

#### Error Handling

`Resolve` throws `ArgumentNullException` for a null `segmentText` and
`ArgumentOutOfRangeException` for a match outside the text. The internal constructor throws
`ArgumentNullException` for a null model. Regex timeouts are bounded to one second per pattern
match. Failures of the model propagate to the caller.

#### Dependencies

- **WordSense** (OTS) - `BasicWordSenseModel` and `ClassifyAsync`.
- **.NET BCL** - `Regex` and concurrent collections.

#### Callers

- **DictionaryChecker** - calls `Resolve` once per match of a dictionary entry to select the
  applicable sense(s). `Linter` passes the shared resolver.
