### SentenceAnalyzer

![Linting Structure](LintingView.svg)

#### Purpose

`SentenceAnalyzer` supplies the sentence-level interpretation required by Rule 4.1 and the
Rule 8.4-8.7 counting adjustments. Its single responsibility is to split extracted prose into
`Sentence` values and compute each sentence's rule-adjusted word count.

#### Data Model

**Sentence**: immutable record representing one identified sentence.

- `Text`: `string` - sentence text after splitting.
- `WordCount`: `int` - word count computed by `CountWords`.

**SentenceSplitRegex**: compiled regex that splits after `.`, `!`, `?`, or `:` when the
following text looks like a new sentence.

**QuotedTextRegex**, **ParentheticalRegex**, **NumberWithUnitRegex**, and
**TitleCaseSequenceRegex**: compiled regex helpers implementing Rule 8.5-8.7 counting
adjustments with one-second timeouts.

#### Key Methods

**Split**: Splits segment text into ordered `Sentence` values and counts each one.

- *Parameters*: `string text` - prose text of one segment.
- *Returns*: `IReadOnlyList<Sentence>` - zero or more sentences.
- *Preconditions*: `text` is not null.
- *Postconditions*: Empty or whitespace-only input returns an empty list. Parentheticals that
  look like complete sentences are emitted both inside the containing sentence count and as a
  separate `Sentence` value. `Sentence.Text` keeps any inline code span's literal text
  (backticks included) verbatim, since `MarkdownProseExtractor` no longer strips it.

**CountWords**: Applies Rule 8.5-8.7 counting semantics to one sentence.

- *Parameters*: `string sentence` - one sentence's text.
- *Returns*: `int` - rule-adjusted word count.
- *Preconditions*: `sentence` is not null.
- *Postconditions*: An inline code span (`MarkdownProseExtractor.InlineCodeSpanRegex`) is
  collapsed to a single placeholder token first (Rule 8.6 extension: a code span counts as
  exactly one word regardless of how many tokens it contains), followed by parentheticals,
  quoted text, number-plus-unit spans, and title-case sequences, each also collapsed to single
  placeholder tokens before the final whitespace split. Running the inline-code substitution
  first prevents a code span's internal punctuation, quotes, parentheses, or digits from being
  independently re-matched by the later patterns.

**LooksLikeCompleteSentence**: Private heuristic for deciding whether a parenthetical should
be emitted as its own sentence.

#### Error Handling

`Split` and `CountWords` propagate `ArgumentNullException` for null input. Regex timeouts are
bounded to one second per pattern match. No exceptions are caught locally.

Known limitation: because inline code spans are retained verbatim rather than stripped before
sentence splitting, a code span that itself contains sentence-terminating punctuation followed
by whitespace and an uppercase letter/digit (for example, `` `Foo. Bar()` ``) can cause
`SentenceSplitRegex` to split what should be one opaque token into multiple sentences. This is
consistent with this class's documented scope as a heuristic, regex-based implementation, not
a full natural-language parser.

#### Dependencies

- **MarkdownProseExtractor** - provides segment text for analysis, and the shared
  `InlineCodeSpanRegex` used by `CountWords` to count an inline code span as one word.
- **.NET BCL** - `Regex` support only.

#### Callers

- **StructuralRules** - obtains sentence lists and word counts for rule evaluation.
