## PartOfSpeech Verification

This document provides the verification evidence for the PartOfSpeech OTS software item.
Requirements for this OTS item are defined in the PartOfSpeech OTS Software Requirements document.

### Required Functionality

PartOfSpeech is the part-of-speech tagging library used by the Linting subsystem. Through
`Tagger.Default.Tag` it tags the tokens of a sentence, so the project can tell a noun usage of a
dictionary term from a verb usage. Correct noun and verb answers for representative technical
sentences, and inconclusive results for low-confidence answers, confirm the library is
functioning correctly for the project's usage.

### Verification Approach

PartOfSpeech has no self-validation CLI of its own, so it is verified by transitive evidence from
the Linting subsystem's passing tests. Each scenario below names a test that runs the real
`Tagger.Default` through `PartOfSpeechResolver`. Scripted-tagger tests of the resolver's own
mapping logic (for example `Resolve_NoTokensCoverMatch_ReturnsNull`, which scripts a tagger that
returns no tokens) verify the resolver rather than the library, so they are not evidence for this
OTS item. A passing test run for all scenarios constitutes evidence that the tagger returns the
expected part of speech at or above the 0.95 minimum confidence for the legal-usage corpus (68 of
the 70 noun rows and all 30 verb rows; the other two noun rows are inconclusive but still not
flagged) and the 26 hard cases, and that low-confidence or other-tag answers are reported as
inconclusive.

### Test Scenarios

#### Resolve_NounUsage_ReturnsNoun

**Scenario**: A dictionary term is used as a noun in a technical sentence and is tagged by the
real tagger.

**Expected**: The tagger returns a noun tag at or above the minimum confidence.

**Requirement coverage**: `Ste100Mark-OTS-PartOfSpeech`.

#### Resolve_VerbUsage_ReturnsVerb

**Scenario**: A dictionary term is used as a verb in a technical sentence and is tagged by the
real tagger.

**Expected**: The tagger returns a verb tag at or above the minimum confidence.

**Requirement coverage**: `Ste100Mark-OTS-PartOfSpeech`.

#### Resolve_HardCase_ReturnsExpectedRole

**Scenario**: Each of 26 hard cases (imperatives, noun-noun compounds, and modals) is tagged by
the real tagger.

**Expected**: Every case resolves to the expected noun or verb role, except one imperative
("Mount the bracket on the frame.") that the tagger answers below the minimum confidence and
that is therefore inconclusive.

**Requirement coverage**: `Ste100Mark-OTS-PartOfSpeech`.

#### Resolve_BelowThresholdOrOtherTag_ReturnsNull

**Scenario**: The real tagger tags short or ambiguous sentences (for example "Wash pump" or
"Block 0 failed the self-test.") with a confidence below 0.95 or with another part of speech.

**Expected**: The result is inconclusive (null) and no exception is thrown.

**Requirement coverage**: `Ste100Mark-OTS-PartOfSpeech`.

#### Evaluate_LegalNounUsage_VerbOnlyDisallowedEntry_NotFlagged

**Scenario**: The corpus of legal noun usages of verb-only disallowed entries is linted using the
real tagger.

**Expected**: The tagger classifies each usage as a noun, so no usage is flagged.

**Requirement coverage**: `Ste100Mark-OTS-PartOfSpeech`.

#### Evaluate_LegalVerbUsage_NounOnlyDisallowedEntry_NotFlagged

**Scenario**: The corpus of legal verb usages of noun-only disallowed entries is linted using
the real tagger.

**Expected**: The tagger classifies each usage as a verb, so no usage is flagged.

**Requirement coverage**: `Ste100Mark-OTS-PartOfSpeech`.
