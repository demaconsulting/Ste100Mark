### PartOfSpeechResolver

![Linting Structure](LintingView.svg)

#### Purpose

`PartOfSpeechResolver` decides whether a matched dictionary term is used as a noun or a verb at
a specific point in prose, so that `DictionaryChecker` can select the correct
part-of-speech-tagged sense of a multi-sense `DictionaryEntry`. It implements the internal
`IPartOfSpeechResolver` interface and delegates the tagging to the `DemaConsulting.WordSense`
OTS library. The engine is selectable: `basic` (`BasicWordSenseModel`, in-memory, offline),
`minilm` (`MiniLMWordSenseModel`, the default), or `bert` (`BertBaseCasedWordSenseModel`). The two
encoder models implement `IDownloadableWordSenseModel` and download their model files on first use.
It replaces an earlier regex-based heuristic.

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

**Model**: the `IWordSenseModel` used for tagging. `Create(engine, factory)` builds a resolver
for a `PartOfSpeechEngine`; the optional factory lets tests substitute any model, and the
default factory creates the real `BasicWordSenseModel`, `MiniLMWordSenseModel`, or
`BertBaseCasedWordSenseModel`. Creating a model performs no I/O. Internal constructors accept any
model for tests.

**Lazy preparation**: when the model is an `IDownloadableWordSenseModel`, the first
classification calls `EnsureModelAsync` once (download if needed, then load). Resolutions that
never need classification never touch the network. If preparation throws, the resolver throws an
`InvalidOperationException` ("Part-of-speech engine '<name>' could not be prepared: ... Use
--pos-engine basic (or dictionary.pos-engine: basic) to work offline."), caches that failure so
later calls fail the same way without retrying, and never falls back to another engine. The wait is
bounded (five minutes by default): a cancellation token is passed to `EnsureModelAsync`, and the
resolver also stops waiting itself, so a hung download fails with the same error ("... did not
complete within 300 seconds ... Use --pos-engine basic ...").

**Disposal**: the resolver is `IDisposable` and disposes its model. `Linter` owns it per run.
There is no shared process-wide instance.

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

The calibration used the original basic model. It has not been re-tuned for the `minilm` and
`bert` encoder engines or for the 0.1.0-beta.6 basic model, and the threshold stays at 0.45.
Re-tuning per engine is a recommended follow-up.

#### Known limitations

**Runt fragments (accepted limitation)**: the beta.6 basic tagger reads a two-word fragment
without a verb, such as "Wash pump", as an imperative and tags `pump` as a verb. Real prose
sentences ("The wash pump is clean.") are tagged as nouns. The affected unit tests use a full
sentence or a scripted resolver instead of the fragment, and the verb-less table-cell case is
documented in `DictionaryCheckerTests`. This limitation is accepted; no resolver heuristic
compensates for it.

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
match. Failures of the model propagate to the caller; a failure to download or load a
downloadable model is wrapped in a clear `InvalidOperationException` (see *Lazy preparation*).

#### Dependencies

- **WordSense** (OTS) - `BasicWordSenseModel`, `MiniLMWordSenseModel`,
  `BertBaseCasedWordSenseModel`, `IDownloadableWordSenseModel.EnsureModelAsync`, and
  `ClassifyAsync`.
- **PartOfSpeechEngine** - the engine selection enum.
- **.NET BCL** - `Regex` and concurrent collections.

#### Callers

- **DictionaryChecker** - calls `Resolve` once per match of a dictionary entry to select the
  applicable sense(s). `Linter` creates one resolver per run and passes it in.
