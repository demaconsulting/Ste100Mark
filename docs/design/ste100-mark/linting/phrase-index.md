### PhraseIndex

![Linting Structure](LintingView.svg)

#### Purpose

`PhraseIndex` answers one question quickly for a large set of approved multi-word phrases: does a
span of text lie entirely inside an occurrence of any of them? `Linter` builds one index per run
from the multi-word glossary terms and variants (`LintGlossary.GetAllowedPhrases`), and
`DictionaryChecker` asks it whether a dictionary hit is inside an approved glossary term. Its
cost depends on the number of dictionary hits and the phrases that share a word with the hit, not
on the glossary size, so no regex is compiled or run for the glossary.

#### Data Model

**Class**: `internal sealed class PhraseIndex`.

- A lookup from each word/hyphen token of every phrase (case-insensitive; a token is a run of
  regex word characters and hyphens, so a word such as `(safe)` is indexed as `safe`) to the
  phrases that contain it. Each phrase is stored as its whitespace-separated words.

An occurrence of a phrase has the same meaning the allow-in-phrase pattern has: case-insensitive,
a space in the phrase matches one or more whitespace characters, and the characters before and
after the occurrence are neither word characters nor hyphens (or the text edge).

#### Key Methods

**Constructor**: indexes the phrases by token. Blank phrases are ignored.

**Contains(text, start, length)**: takes the first token of the span, then, for every phrase that
contains that token, scans backward from `start` over at most the phrase's non-whitespace
length to find candidate occurrence starts. A candidate is accepted when it follows a boundary,
`MatchEnd` matches the whole phrase from it and finds a closing boundary, and the occurrence
covers the whole span.

**MatchEnd**: matches the phrase words at a position with a hand-written case-insensitive ordinal
comparison, requiring one or more whitespace characters between words, and returns the end of the
occurrence or `-1`.

#### Error Handling

`PhraseIndex` does no I/O and throws no domain exceptions. A span that does not start with a
word/hyphen character, or whose first token no phrase contains, is simply not contained.

#### Dependencies

- **.NET BCL** - collections, LINQ, and string comparison only.

#### Callers

- **Linter** - builds the index once per run from the glossary phrases.
- **DictionaryChecker** - calls `Contains` for each dictionary hit that is not already excluded.
