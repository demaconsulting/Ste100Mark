## WordSense

This document describes the integration and usage design for the `DemaConsulting.WordSense`
OTS software item (packages `DemaConsulting.WordSense`, `DemaConsulting.WordSense.MiniLM`, and
`DemaConsulting.WordSense.BertBaseCased`, version 0.1.0-beta.6).

### Purpose

WordSense is chosen as the part-of-speech tagger for the project. It classifies each word of a
sentence into a sense with a part of speech and a confidence, so that the `Linting` subsystem
can decide whether a matched dictionary term is used as a noun or a verb. It replaces an earlier
regex-based heuristic.

### Features Used

- `BasicWordSenseModel` (core package) - the in-memory model; it needs no model download and no
  network access. Selected with `--pos-engine basic`.
- `MiniLMWordSenseModel` (`DemaConsulting.WordSense.MiniLM`) - the default encoder model.
  `BertBaseCasedWordSenseModel` (`DemaConsulting.WordSense.BertBaseCased`) - the larger encoder
  model. Both run on ONNX Runtime and implement `IDownloadableWordSenseModel`.
- `IDownloadableWordSenseModel.EnsureModelAsync` - downloads the model files on first use and
  loads them. The `WORDSENSE_MIRROR_*` environment variables redirect the download. Failures
  surface as `ModelUnavailableException`, `ModelIntegrityException`, or
  `DownloadConfigurationException`.
- `IWordSenseModel.ClassifyAsync` - classifies a sequence of tokens that form one sentence and
  returns one `ClassifyResult` per token.
- `ClassifyResult.Details` (null for an unknown word), `WordDetails.PartOfSpeech`, and
  `ClassifyResult.Confidence`.

### Integration Pattern

The packages are consumed as NuGet package references in the main application project, so the
assemblies ship inside the Ste100Mark tool package. The model files of the encoder engines are
not in the package; they download on first use. `PartOfSpeechResolver` owns one model per lint
run, chosen by the selected engine, and disposes it at the end of the run. A downloadable model
is prepared once, lazily, before the first classification; a failure is reported as an error and
there is no fallback to another engine. For each lookup it splits the prose
segment into sentences, tokenizes the sentence containing the match with the regular
expression `[\w'-]+|[^\w\s]`, and calls `ClassifyAsync` once per sentence. It waits for the
result synchronously. A result
with `Details` equal to null is treated as an unknown word. Only a `Noun` or `Verb` answer at
or above the resolver's minimum confidence is mapped to the repository's own `PartOfSpeech`;
every other answer is inconclusive. The library is beta software, so its package version is
pinned exactly in the project file and upgrades are reviewed deliberately. The size of the ONNX
Runtime native assets that ship in the tool package has not been measured.
