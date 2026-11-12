### PartOfSpeechResolver

![Linting Structure](LintingView.svg)

#### Purpose

`PartOfSpeechResolver` decides whether a matched dictionary term is used as a noun, verb, adjective, or adverb at
a specific point in prose, so that `DictionaryChecker` can select the correct
part-of-speech-tagged sense of a multi-sense `DictionaryEntry`. It implements the internal
`IPartOfSpeechResolver` interface and delegates the tagging to the `DemaConsulting.PartOfSpeech`
OTS library (`Tagger.Default`). It replaces an earlier tagger library and, before that, a
regex-based heuristic.

The resolver returns `Noun`, `Verb`, `Adjective`, or `Adverb` when the tagger is at least 0.90
confident. It preserves a confident `Determiner` result as resolver evidence distinct from
dictionary sense roles; that result rules out all role-specific senses without being a valid
dictionary configuration value. Other unsupported tags remain `null` (inconclusive).

#### Data Model

**IPartOfSpeechResolver**: internal interface with one method, `PartOfSpeech? Resolve(string
segmentText, int matchIndex, int matchLength)`. `DictionaryChecker` depends on the interface so
tests can inject a scripted or fixed resolver.

**MinimumConfidence**: `internal const double` (0.90). The minimum tagger confidence for a
supported answer to be accepted. See *Threshold selection* below.

**SentenceBoundaryRegex**: shared with `SentenceAnalyzer` (`SentenceSplitRegex`, a single source
of truth): whitespace after `.`, `!`, `?`, or `:` that is followed by an uppercase letter, digit,
quote, opening parenthesis, backtick, asterisk, underscore, or the end of text. A lowercase
continuation (for example `e.g. test the valve`) therefore stays in one sentence. A segment is
split into sentences so a match is classified within its own sentence only.

**TaggedSpan**: `internal readonly record struct` of a token's `Start`, `Length`, `Upos` tag, and
`Confidence`. Offsets are relative to the sentence text that was tagged.

**Tagging function**: a `Func<string, IReadOnlyList<TaggedSpan>>` that tags one sentence. The
public constructor uses `Tagger.Default` (the package tokenizes the sentence itself, splits
contractions and clitics, keeps hyphenated words whole, and the resolver flattens the tokens of
every sentence the package returns); an internal constructor accepts any function for tests. The
tagger is in-memory and thread-safe and needs no download.

**Shared**: lazily created process-wide instance, so the tagger is used through one resolver and
one cache per process.

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
- *Returns*: `PartOfSpeech?` - `Noun`, `Verb`, `Adjective`, or `Adverb` when the tagger confidently
  chose that part of speech for the token that covers the match; resolver-only `Determiner` for a
  confident determiner; `null` otherwise.
- *Preconditions*: `segmentText` is not null; the match lies within the segment text.
- *Postconditions*: The sentence that holds the match is tagged with a single `Tagger.Tag` call.
  A token whose confidence is below `MinimumConfidence` is inconclusive. A supported tag maps to
  the repository's `PartOfSpeech`; a confident determiner maps to the resolver-only
  `Determiner` role, while every other unsupported tag is inconclusive. A match that spans more than one token (a multi-word
  term) is inconclusive, as is a match that no token covers. The package does not report unknown
  words: a word that is not in its lexicon is tagged as a noun, so it cannot be detected.

**GetTags**: Tags one sentence, maps each token to a role, and caches the outcome.

#### Threshold selection

`MinimumConfidence` is the strict threshold documented by the `DemaConsulting.PartOfSpeech`
package: on the package's open test split it answers 89.4% of decisions with 3.8 harmful errors
per 1000 answered decisions. In the project's own hand check of 110 samples, no error was found
at a confidence of 0.90 or higher, while low-confidence VERB answers on noun-noun compounds were
wrong about half the time. A strict value is preferred because a wrong answer either hides a real
violation or reports a false one, whereas an inconclusive answer only makes the checker report
the term as ambiguous. `MinimumConfidence_IsPackageStrictThreshold` guards the value, and
`Resolve_ConfidenceThreshold_BoundaryIsInclusive` verifies the boundary behavior.

At this threshold the corpus (`PartOfSpeechFalsePositiveCorpusTests`) resolves all 68 rows of
`LegalNounUsageCases` as nouns and all 30 rows of `LegalVerbUsageCases` as verbs, and 25 of the
26 hard cases (`PartOfSpeechHardCasesTests`; the imperative "Mount the bracket on the frame."
is the exception: it is answered below the threshold and is inconclusive). Two further legal
noun usages, "rank" and "cast", are tested separately as confident noun cases
(`Evaluate_ConfidentNounUsage_VerbOnlyDisallowedEntry_NotFlagged`): the resolver follows the
confident-role path and suppresses their verb-only, self-referential entries.

#### Known limitations

The tagger is a statistical model and is wrong or unsure for some inputs:

- Short verbless fragments (a heading such as "Heading:" or a bare label such as "Function"), sentences that
  contain a low-confidence noun compound (such as "Check the coolant mix ratio before startup."), and some
  imperatives are often answered below the threshold,
  so the term is reported as ambiguous or, for a single-sense entry, reported.
- A noun-noun compound can be tagged as a verb at low confidence; such answers are rejected by
  the threshold.
- Words that are not in the lexicon are tagged as nouns, so a noun-only restriction on an
  unknown word cannot be told from a known noun.
- A sentence-initial imperative that looks like a noun phrase ("Test results daily") can still be
  read as a noun with high confidence.

When the answer is wrong in the noun direction, a verb use of a verb-only entry is not
reported; when it is inconclusive, every sense is reported as ambiguous. The mode (Procedure or
Descriptive) no longer influences the part of speech.

#### Error Handling

`Resolve` throws `ArgumentNullException` for a null `segmentText` and
`ArgumentOutOfRangeException` for a match outside the text. The internal constructor throws
`ArgumentNullException` for a null tagging function. Failures of the tagger propagate to the
caller. The only regular expression is the shared sentence-boundary pattern, which has its own
timeout.

#### Dependencies

- **PartOfSpeech** (OTS) - `Tagger.Default`, `Tagger.Tag`, and `TaggedToken`.
- **.NET BCL** - `Regex` and concurrent collections.

#### Callers

- **DictionaryChecker** - calls `Resolve` once per match of a dictionary entry to select the
  applicable sense(s). `Linter` passes the shared resolver.
