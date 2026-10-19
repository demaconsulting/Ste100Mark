## WordSense

This document describes the integration and usage design for the `DemaConsulting.WordSense`
OTS software item (packages `DemaConsulting.WordSense` and `DemaConsulting.WordSense.Basic`).

### Purpose

WordSense is chosen as the part-of-speech tagger for the project. It classifies each word of a
sentence into a sense with a part of speech and a confidence, so that the `Linting` subsystem
can decide whether a matched dictionary term is used as a noun or a verb. It replaces an earlier
regex-based heuristic.

### Features Used

- `BasicWordSenseModel` - the in-memory model bundled in the `DemaConsulting.WordSense.Basic`
  package; it needs no model download and no network access.
- `IWordSenseModel.ClassifyAsync` - classifies a sequence of tokens that form one sentence and
  returns one `WordSense` result per token.
- `WordSense.Details` (null for an unknown word), `WordDetails.PartOfSpeech`, and
  `WordSense.Confidence`.

### Integration Pattern

Both packages are consumed as NuGet package references in the main application project, so the
assemblies ship inside the Ste100Mark tool package. `PartOfSpeechResolver` holds one shared
`BasicWordSenseModel` instance for the process lifetime. For each lookup it splits the prose
segment into sentences, tokenizes the sentence containing the match with the regular
expression `[\w'-]+|[^\w\s]`, and calls `ClassifyAsync` once per sentence. It waits for the
result synchronously, which is safe because the Basic model completes synchronously. A result
with `Details` equal to null is treated as an unknown word. Only a `Noun` or `Verb` answer at
or above the resolver's minimum confidence is mapped to the repository's own `PartOfSpeech`;
every other answer is inconclusive. The library is beta software, so its package version is
pinned exactly in the project file and upgrades are reviewed deliberately.
