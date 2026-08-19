### MarkdownProseExtractor

![Linting Structure](LintingView.svg)

#### Purpose

`MarkdownProseExtractor` identifies the Markdown text that should be checked as prose and
strips content that should not affect linguistic rules. Fenced code blocks and link
destination URLs are removed entirely; inline code spans are retained verbatim (Rule 8.6
treats a technical literal as one word), so downstream consumers can display them in
diagnostics while still excluding their content from grammar-sensitive checks. Table rows are
split into one segment per cell rather than merged into a paragraph, since concatenating
several rows would corrupt sentence/word counting with pipe characters and unrelated cell
text; the header separator row is skipped entirely because it carries no prose. Its single
responsibility is to turn a raw Markdown document into ordered `ProseSegment` values.

#### Data Model

**SegmentRole**: enum describing the source structure of a segment.

- `Heading` - ATX heading line.
- `ListItem` - one bulleted or numbered list item line.
- `TableRow` - one cell of a Markdown table row (the header separator row is skipped entirely).
- `Paragraph` - one or more consecutive non-heading, non-list-item, non-table-row lines.

**ProseSegment**: immutable record describing one extracted prose run.

- `Text`: `string` - cleaned prose text with fenced code blocks and link destinations
  removed; inline code spans are retained verbatim (backticks included).
- `LineNumber`: `int` - 1-based source line where the segment begins.
- `Role`: `SegmentRole` - heading, list item, table row, or paragraph.

**Regex set**: compiled regexes for fences, headings, list items, inline code (`InlineCodeSpanRegex`,
`internal`, shared with `SentenceAnalyzer`/`StructuralRules`/`DictionaryChecker`), inline
links, and blank lines. Each regex uses a one-second timeout.

#### Key Methods

**Extract**: Converts raw Markdown into ordered prose segments.

- *Parameters*: `string markdown` - full Markdown document text.
- *Returns*: `IReadOnlyList<ProseSegment>` - extracted segments in document order.
- *Preconditions*: `markdown` is not null.
- *Postconditions*: Fenced code blocks are skipped, inline code spans are retained verbatim,
  paragraphs are merged across adjacent lines, list items remain separate, each table row cell
  becomes its own segment (the separator row is skipped), and segment line numbers point to the
  start of each emitted segment.

**CleanLine**: Rewrites inline links to keep visible text, leaving inline code spans
untouched.

- *Parameters*: `string line` - raw source line.
- *Returns*: `string` - line with link destinations removed; inline code spans retained
  verbatim.

**SplitTableRowCells**: Splits a table row into its cell texts, trimming the leading/trailing
`|` delimiters. A `|` inside an inline code span is not treated as a cell separator.

- *Parameters*: `string row` - a single table row line (already known to start with `|`).
- *Returns*: `IEnumerable<string>` - the non-empty, trimmed text of each cell, in column order.

**FindInlineCodeSpans**: Locates every inline code span's character offsets in a verbatim
segment or sentence text, for callers (`StructuralRules`, `DictionaryChecker`) that must test
whether some other match falls inside one without disturbing offsets.

- *Parameters*: `string text` - verbatim segment or sentence text.
- *Returns*: `IReadOnlyList<(int Start, int Length)>` - each span's start index and length, in
  document order.

**OverlapsInlineCodeSpan**: Determines whether a tested span is wholly contained in one of the
supplied inline code spans.

- *Parameters*: `int start`, `int length` - the span to test; `IReadOnlyList<(int, int)> spans`
  - spans from `FindInlineCodeSpans`.
- *Returns*: `bool` - `true` if the tested span lies wholly inside an inline code span.

**MaskInlineCodeSpans**: Replaces every inline code span with a single neutral placeholder
character, for grammar-sensitive checks that only need a yes/no signal and do not need to
preserve character offsets afterward (for example, "does this segment contain a semicolon?").

- *Parameters*: `string text` - verbatim segment or sentence text.
- *Returns*: `string` - text with each inline code span replaced by a single character.

**FlushParagraph**: Emits the accumulated paragraph buffer, if non-empty, as a paragraph
segment and resets the buffer state.

#### Error Handling

`Extract`, `FindInlineCodeSpans`, `OverlapsInlineCodeSpan`, and `MaskInlineCodeSpans`
propagate `ArgumentNullException` for null input. The extractor performs no file-system I/O
and does not catch regex exceptions; the compiled regex timeout bounds worst-case matching
time.

#### Dependencies

- **.NET BCL** - `StringBuilder` and `Regex`.
- **SentenceAnalyzer** - downstream consumer of `ProseSegment` text and roles.
- **StructuralRules** and **DictionaryChecker** - evaluate the extracted segments.

#### Callers

- **Linter** - extracts segments before structural and dictionary evaluation.
