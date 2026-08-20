### MarkdownProseExtractor

![Linting Structure](LintingView.svg)

#### Purpose

`MarkdownProseExtractor` identifies the Markdown text that should be checked as prose and
strips content that should not affect linguistic rules. Fenced code blocks and link
destination URLs are removed entirely; inline code spans are retained verbatim so downstream
consumers can display them in diagnostics while still excluding their content from
grammar-sensitive checks. Table rows are split into one segment per cell rather than merged
into paragraphs, since concatenating several rows would corrupt sentence and word counting.
Its single responsibility is to turn a raw Markdown document into ordered `ProseSegment`
values.

#### Data Model

**SegmentRole**: enum describing the source structure of a segment.

- `Heading` - ATX heading line.
- `ListItem` - one bulleted or numbered list item line.
- `TableRow` - one emitted Markdown table cell segment.
- `Paragraph` - one or more consecutive non-heading, non-list-item, non-table-row lines.

**ProseSegment**: immutable record describing one extracted prose run.

- `Text`: `string` - cleaned prose text with fenced code blocks and link destinations
  removed; inline code spans are retained verbatim.
- `LineNumber`: `int` - 1-based source line where the segment begins.
- `Role`: `SegmentRole` - heading, list item, table row, or paragraph.
- `LineOffsets`: `IReadOnlyList<(int Offset, int Line)>` - for a single-line segment, always
  `[(0, LineNumber)]` via the record's secondary constructor; for a multi-line paragraph, one
  entry per folded source line giving that line's starting character offset within `Text`.
  Consumed by `ResolveLine` so diagnostics can report the actual violating line rather than
  always the paragraph's first line.

**ResolveLine**: Resolves the true 1-based source line for a character offset within `Text`,
walking `LineOffsets` to find the last entry whose offset does not exceed the given offset.

- *Parameters*: `int charOffset` - character offset within `Text`.
- *Returns*: `int` - the 1-based source line containing `charOffset`.

**Regex set**: compiled regexes for fences, headings, list items, table rows, table
separator rows, inline code spans, inline links, and blank lines. Each regex uses a
one-second timeout.

#### Key Methods

**Extract**: Converts raw Markdown into ordered prose segments.

- *Parameters*: `string markdown` - full Markdown document text.
- *Returns*: `IReadOnlyList<ProseSegment>` - extracted segments in document order.
- *Preconditions*: `markdown` is not null.
- *Postconditions*: Fenced code blocks are skipped, inline code spans are retained verbatim,
  paragraphs are merged across adjacent lines, list items remain separate, each table row
  cell becomes its own segment, segment line numbers point to the start of each emitted
  segment, and each multi-line paragraph's `LineOffsets` records every folded line's true
  source line for later `ResolveLine` lookups.

**CleanLine**: Rewrites inline links to keep visible text, leaving inline code spans
untouched.

- *Parameters*: `string line` - raw source line.
- *Returns*: `string` - line with link destinations removed.

**SplitTableRowCells**: Splits a table row into its cell texts, trimming the leading and
trailing `|` delimiters. A `|` inside an inline code span is not treated as a cell
separator.

- *Parameters*: `string row` - a single table row line already known to start with `|`.
- *Returns*: `IEnumerable<string>` - non-empty trimmed cell texts, in column order.

**FindInlineCodeSpans**: Locates every inline code span's character offsets in a verbatim
segment or sentence text.

- *Parameters*: `string text` - verbatim segment or sentence text.
- *Returns*: `IReadOnlyList<(int Start, int Length)>` - each span's start index and length.

**OverlapsInlineCodeSpan**: Determines whether a tested span is wholly contained in one of
the supplied inline code spans.

- *Parameters*: `int start`, `int length` - span to test;
  `IReadOnlyList<(int Start, int Length)> spans` - spans returned by
  `FindInlineCodeSpans`.
- *Returns*: `bool` - `true` when the tested span lies wholly inside an inline code span.

**MaskInlineCodeSpans**: Replaces every inline code span with a single neutral placeholder
character for checks that only need a yes or no signal.

- *Parameters*: `string text` - verbatim segment or sentence text.
- *Returns*: `string` - text with each inline code span replaced by a single character.

**FlushParagraph**: Emits the accumulated paragraph buffer, if non-empty, as a paragraph
segment and resets the buffer state.

- *Parameters*: `List<ProseSegment> segments`; `StringBuilder buffer`;
  `List<(int Offset, int Line)> lineOffsets`; `ref int startLine`.
- *Returns*: `void`.

#### Error Handling

`Extract`, `FindInlineCodeSpans`, `OverlapsInlineCodeSpan`, and `MaskInlineCodeSpans`
propagate `ArgumentNullException` for null input. The extractor performs no file-system I/O
and does not catch regex exceptions; the compiled regex timeout bounds worst-case matching
time. Unclosed fenced code blocks are tolerated by remaining in fence mode until end of
input, which suppresses the remainder of the document rather than producing partial mixed
content.

#### Dependencies

- **.NET BCL** - `StringBuilder`, LINQ, and `Regex`.
- **SentenceAnalyzer** - consumes extracted segment text and the shared inline-code regex.
- **StructuralRules** and **DictionaryChecker** - consume extracted segments and the inline
  code span helper methods.

#### Callers

- **Linter** - extracts prose segments from each Markdown file before structural and
  dictionary evaluation.
