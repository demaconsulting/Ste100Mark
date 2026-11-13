## PartOfSpeech Verification

This document provides the verification evidence for the PartOfSpeech OTS software item.
Requirements for this OTS item are defined in the PartOfSpeech OTS Software Requirements document.

### Required Functionality

PartOfSpeech is the part-of-speech tagging library used by the Linting subsystem. Through
`Tagger.Default.Tag` it tags the tokens of a sentence, so the project can tell noun, verb,
adjective, and adverb usage of a dictionary term apart and preserve confident determiner evidence.
Correct noun, verb, adjective, adverb, and determiner answers for representative technical
sentences, and inconclusive results for low-confidence answers, confirm the library is
functioning correctly for the project's usage.

### Verification Approach

PartOfSpeech has no self-validation CLI of its own, so it is verified by transitive evidence from
the Linting subsystem's passing tests. Each scenario below names a test that runs the real
`Tagger.Default` through `PartOfSpeechResolver`. Scripted-tagger tests of the resolver's own
mapping logic (for example `Resolve_NoTokensCoverMatch_ReturnsNull`, which scripts a tagger that
returns no tokens) verify the resolver rather than the library, so they are not evidence for this
OTS item. A passing test run for all scenarios constitutes evidence that the tagger returns the
expected part of speech at or above the 0.90 minimum confidence for the legal-usage corpus (all
68 rows of `LegalNounUsageCases` and all 30 rows of `LegalVerbUsageCases`) and for all 26
hard cases, and that low-confidence or
other-tag answers are reported as inconclusive. Two further legal noun usages, "rank" and
"cast", are tested separately as confident noun cases: the resolver follows the confident-role
path and does not flag their verb-only entries.

### Test Scenarios

#### Resolve_NounUsage_ReturnsNoun

**Scenario**: A dictionary term is used as a noun in a technical sentence and is tagged by the
real tagger.

**Expected**: The tagger returns a noun tag at or above the minimum confidence.

**Requirement coverage**: `Ste100Mark-OTS-PartOfSpeech`.

#### Resolve_AdjectiveOrAdverbUsage_ReturnsMatchingRole

**Scenario**: Representative adjective and adverb usages are tagged by the real tagger and
resolved through the adjective/adverb evidence test.

**Expected**: The resolver returns the matching adjective or adverb role at or above the
minimum confidence.

**Requirement coverage**: `Ste100Mark-OTS-PartOfSpeech`.

#### Evaluate_DefaultResolver_AdjectiveAndAdverbEvidence_SelectsOrSuppresses

**Scenario**: The full checker evaluates the adjective/adverb evidence corpus with the real
tagger and shipped checker path.

**Expected**: Approved adjective/adverb senses are suppressed and unapproved senses report only
the matching role-specific suggestion.

**Requirement coverage**: `Ste100Mark-OTS-PartOfSpeech`.

#### Resolve_VerbUsage_ReturnsVerb

**Scenario**: A dictionary term is used as a verb in a technical sentence and is tagged by the
real tagger.

**Expected**: The tagger returns a verb tag at or above the minimum confidence.

**Requirement coverage**: `Ste100Mark-OTS-PartOfSpeech`.

#### Resolve_HardCase_ReturnsExpectedRole

**Scenario**: Each of 26 hard cases (imperatives, noun-noun compounds, and modals) is tagged by
the real tagger.

**Expected**: Every case resolves to the expected noun, verb, adjective, or adverb role, or to the
resolver-only determiner result for a confident determiner.

**Requirement coverage**: `Ste100Mark-OTS-PartOfSpeech`.

#### Resolve_BelowThresholdOrOtherTag_ReturnsNull

**Scenario**: The real tagger tags short or ambiguous sentences (for example "Heading:",
"Check the coolant mix ratio before startup.", or "Block 0 failed the self-test.") with a
confidence below 0.90 or with another part of speech.

**Expected**: The result is inconclusive (null) and no exception is thrown.

**Requirement coverage**: `Ste100Mark-OTS-PartOfSpeech`.

#### Resolve_UnknownWordWithRealTagger_ReturnsNullAndKnownWordResolves

**Scenario**: The real tagger tags "Inspect the turbopump for damage.", in which "turbopump" is
not in its lexicon and "damage" is.

**Expected**: The tagger reports `IsKnownWord` false for "turbopump" with a noun tag at or above
the minimum confidence, and true for "damage"; the resolver treats "turbopump" as inconclusive
and resolves "damage" as a noun.

**Requirement coverage**: `Ste100Mark-OTS-PartOfSpeech`.

#### Tagger_Default_ConfidenceSeparatesResolvedFromInconclusiveScenarios

**Scenario**: The real tagger tags the noun, verb, adjective-and-adverb, and inconclusive
scenario sets directly, without the resolver.

**Expected**: Noun, verb, adjective, and adverb scenarios are tagged as a noun, verb,
adjective, or adverb at or above the minimum confidence; inconclusive scenarios are not a
confident noun, verb, adjective, or adverb. Determiner evidence is covered separately by
`Resolve_DeterminerUsage_ReturnsDeterminerRole`.

**Requirement coverage**: `Ste100Mark-OTS-PartOfSpeech`.

#### Evaluate_LegalNounUsage_VerbOnlyDisallowedEntry_NotFlagged

**Scenario**: The corpus of legal noun usages of verb-only disallowed entries is linted using the
real tagger.

**Expected**: The tagger classifies each usage as a noun, so no usage is flagged.

**Requirement coverage**: `Ste100Mark-OTS-PartOfSpeech`.

#### Evaluate_ConfidentNounUsage_VerbOnlyDisallowedEntry_NotFlagged

**Scenario**: Two legal noun usages that the real tagger answers as confident nouns ("rank" in
"Note the technician's rank on the form." and "cast" in "Examine the cast for cracks.") are
linted against a verb-only disallowed entry.

**Expected**: The resolver identifies the noun role through the confident-role path and the usage
is not flagged.

**Requirement coverage**: `Ste100Mark-OTS-PartOfSpeech`.

#### Evaluate_LegalVerbUsage_NounOnlyDisallowedEntry_NotFlagged

**Scenario**: The corpus of legal verb usages of noun-only disallowed entries is linted using
the real tagger.

**Expected**: The tagger classifies each usage as a verb, so no usage is flagged.

**Requirement coverage**: `Ste100Mark-OTS-PartOfSpeech`.
