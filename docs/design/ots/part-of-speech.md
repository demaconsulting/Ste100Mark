## PartOfSpeech

This document describes the integration and usage design for the `DemaConsulting.PartOfSpeech`
OTS software item (package `DemaConsulting.PartOfSpeech`).

### Purpose

PartOfSpeech is chosen as the part-of-speech tagger for the project. It tags each token of a
sentence with a Universal POS tag and a confidence, so that the `Linting` subsystem can decide
whether a matched dictionary term is used as a noun or a verb. It replaces an earlier tagger
library and, before that, a regex-based heuristic.

### Features Used

- `Tagger.Default` - the shared, thread-safe tagger whose lexicon and weights are embedded in the
  assembly; it needs no model download and no network access, and it is created lazily on first
  use.
- `Tagger.Tag(string)` - splits a text into sentences, tokenizes them, and returns the tagged
  sentences (`TaggedSentence`) with their `TaggedToken` items.
- `TaggedToken.Start`, `TaggedToken.Length`, `TaggedToken.Tag` (a `Upos` value), and
  `TaggedToken.Confidence`.

### Integration Pattern

The package is consumed as a NuGet package reference in the main application project, so it is a
NuGet dependency of the Ste100Mark tool package (the tool package does not contain the assembly
itself). The `DemaConsulting.PartOfSpeech` assembly embeds its model: the lexicon and tagger
weights. `PartOfSpeechResolver`
splits the prose segment into sentences with the repository's own sentence rule, then calls
`Tagger.Default.Tag` once for the sentence that contains the match. The token offsets index the
string that was passed in, so they are relative to the sentence text; the resolver flattens the
tokens of every returned sentence. Only a `Noun` or `Verb` tag at or above the resolver's minimum
confidence (0.95, the strict threshold documented by the package) is mapped to the repository's own
`PartOfSpeech`; every other answer is inconclusive. The package does not report unknown words: a
word that is not in its lexicon is tagged as a noun, so such a word cannot be detected. The
library is beta software, so its package version is pinned exactly in the project file and
upgrades are reviewed deliberately.

### Licensing

The assembly embeds a lexicon derived from Open English WordNet (CC BY 4.0), which also uses the
part-of-speech data of the Moby Part-of-Speech List (Moby Part-of-Speech II, by Grady Ward,
dedicated to the public domain by its author), and weights trained in
part on the Manually Annotated Sub-Corpus (CC BY 3.0 US) and on public-domain and CC0 corpora.
Moby and the other public-domain sources need no attribution; their provenance is recorded in the
courtesy notices of the package's `THIRD-PARTY-NOTICES.md`, which the repository's
`THIRD-PARTY-NOTICES.md` reproduces in condensed form. The Ste100Mark tool package therefore
states `MIT AND CC-BY-4.0 AND CC-BY-3.0-US` in `PACKAGE-LICENSE.md` and ships that file and
`THIRD-PARTY-NOTICES.md` at the package root so the attribution travels with every copy.
