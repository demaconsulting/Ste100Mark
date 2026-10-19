## WordSense Verification

This document provides the verification evidence for the WordSense OTS software item.
Requirements for this OTS item are defined in the WordSense OTS Software Requirements document.

### Required Functionality

WordSense is the part-of-speech tagging library used by the Linting subsystem. Through
`BasicWordSenseModel.ClassifyAsync` it classifies the words of a sentence, so the project can
tell a noun usage of a dictionary term from a verb usage. Correct noun and verb answers for
representative technical sentences, and a null result for unknown words, confirm the library is
functioning correctly for the project's usage.

### Verification Approach

WordSense has no self-validation CLI of its own, so it is verified by transitive evidence from
the Linting subsystem's passing tests. Each scenario below names a test that runs the real
`BasicWordSenseModel` through `PartOfSpeechResolver`. Scripted-model tests of the resolver's own
mapping logic (for example `Resolve_UnknownWord_ReturnsNull`, which scripts a tagger that
returns no details) verify the resolver rather than the library, so they are not evidence for
this OTS item. A passing test run for all scenarios constitutes evidence that the tagger
returns the expected part of speech for the 100-sentence legal-usage corpus and the 26 hard
cases used to choose the confidence threshold, and no details for a word it does not know.

### Test Scenarios

#### Resolve_NounUsage_ReturnsNoun

**Scenario**: A dictionary term is used as a noun in a technical sentence and is classified by
the real tagger.

**Expected**: The tagger returns a noun sense at or above the minimum confidence.

**Requirement coverage**: `Ste100Mark-OTS-WordSense`.

#### Resolve_VerbUsage_ReturnsVerb

**Scenario**: A dictionary term is used as a verb in a technical sentence and is classified by
the real tagger.

**Expected**: The tagger returns a verb sense at or above the minimum confidence.

**Requirement coverage**: `Ste100Mark-OTS-WordSense`.

#### Resolve_HardCase_ReturnsExpectedRole

**Scenario**: Each of 26 hard cases (imperatives, noun-noun compounds, and modals) is
classified by the real tagger.

**Expected**: Every case resolves to the expected noun or verb role.

**Requirement coverage**: `Ste100Mark-OTS-WordSense`.

#### Resolve_UnknownWordWithRealModel_ReturnsNull

**Scenario**: The real tagger classifies a made-up word it cannot know.

**Expected**: The tagger returns no details, so the result is inconclusive (null) and no
exception is thrown.

**Requirement coverage**: `Ste100Mark-OTS-WordSense`.

#### Evaluate_LegalNounUsage_VerbOnlyDisallowedEntry_NotFlagged

**Scenario**: The 100-sentence corpus of legal noun usages of verb-only disallowed entries is
linted using the real tagger.

**Expected**: The tagger classifies each usage as a noun, so no usage is flagged.

**Requirement coverage**: `Ste100Mark-OTS-WordSense`.

#### Evaluate_LegalVerbUsage_NounOnlyDisallowedEntry_NotFlagged

**Scenario**: The corpus of legal verb usages of noun-only disallowed entries is linted using
the real tagger.

**Expected**: The tagger classifies each usage as a verb, so no usage is flagged.

**Requirement coverage**: `Ste100Mark-OTS-WordSense`.
