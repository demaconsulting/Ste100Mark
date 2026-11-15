### PhraseIndex

![Linting Structure](LintingView.svg)

#### Purpose

`PhraseIndex` answers one question quickly for a large set of approved phrases (single words
included): does a span of text lie entirely inside an occurrence of any of them? `Linter` builds
one index per run from the multi-word glossary terms and variants (`LintGlossary.GetAllowedPhrases`)
and one index per distinct set of configured `dictionary.allow-in-phrase` sources (see
`LintConfig.ResolveAllowedPhraseKey`), and `DictionaryChecker` asks them whether a dictionary hit
is inside an approved phrase. Its cost depends on the number of dictionary hits and the phrases
that share a word with the hit, not on the number of phrases, so no regex is compiled or run per
phrase and no per-phrase regex timeout applies.

#### Data Model

**Class**: `internal sealed class PhraseIndex`.

- A lookup from each word/hyphen token of every phrase (case-insensitive; a token is a run of
  regex word characters and hyphens, so a word such as `(safe)` is indexed as `safe`) to the
  phrases that contain it. Each phrase is stored as its literal text with the count of its
  non-space characters.

Phrase model (space-only whitespace): an occurrence of a phrase is case-insensitive (ordinal,
culture-independent; exotic Unicode case equivalences such as the Kelvin sign are not folded),
and the characters before and after the occurrence are neither word characters nor hyphens (or
the text edge). Only U+0020 in a phrase is whitespace-tolerant: each one matches one or more
whitespace characters of the text (the regex `\s` set), so consecutive spaces need at least that
many whitespace characters and a leading or trailing space requires whitespace at that edge. Every
other character, including a literal tab or non-breaking space, matches only itself. Glossary
phrases are normalized by `LintGlossary.GetAllowedPhrases` (whitespace runs become one space, ends
trimmed), so glossary matching stays any-whitespace tolerant.

Every occurrence of a phrase is honored, including an occurrence that overlaps another occurrence
of the same phrase (for example the second `a b a` in `a b a b a`). The former per-phrase regex
scan skipped such an occurrence; this is an intentional, documented change that makes explicit and
glossary phrases behave the same.

#### Key Methods

**Constructor**: indexes the distinct phrases by token. A phrase with no word or hyphen
character cannot be indexed and is ignored.

**Contains(text, start, length)**: picks the key token: the first token inside the span (so a
span that starts with punctuation, such as `(safe`, is resolved), or, when the span has no token
character (for example `&`), the nearest token on each side (so `&` inside `R&D` is found). For
every phrase that contains the key token, it scans backward from `start` over at most the
phrase's non-space length (counting only non-whitespace text characters) to find candidate
occurrence starts. A candidate is accepted when it follows a boundary, `MatchEnd` matches the
whole phrase from it and finds a closing boundary, and the occurrence covers the whole span.

**MatchEnd**: matches the phrase at a position with a case-insensitive ordinal comparison per
character. For each run of spaces it tries the longest whitespace run first and backtracks down to
the minimum, exactly like a greedy `\s+` per space, and returns the end of the occurrence or `-1`.

#### Error Handling

`PhraseIndex` does no I/O and throws no domain exceptions. A span whose key tokens no phrase
contains is simply not contained; a span made only of punctuation that has no token on either
side, and a phrase made only of punctuation, cannot be resolved and are not matched.

#### Dependencies

- **.NET BCL** - collections, LINQ, and string comparison only.

#### Callers

- **Linter** - builds the glossary index once per run and one explicit-phrase index per distinct
  source set (at most 64 cached; later distinct sets are built per file).
- **DictionaryChecker** - calls `Contains` on both indexes for each dictionary hit that is not
  already excluded.
