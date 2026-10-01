## Linting

![Linting Structure](LintingView.svg)

### Purpose

The `Linting` subsystem performs the tool's Markdown prose analysis for ASD-STE100-style
writing. It loads the effective lint configuration and dictionary, selects the Markdown
files in scope, extracts prose from each file, evaluates structural and vocabulary rules,
and reports the aggregated diagnostics in text or JSON format. Its role in Ste100Mark is to
provide the primary command-path implementation behind the default lint workflow exposed
through `Linter.Run`.

### Overview

The subsystem contains one orchestration unit (`Linter`), one reporting unit
(`DiagnosticReporter`), four analysis units (`MarkdownProseExtractor`, `SentenceAnalyzer`,
`StructuralRules`, and `DictionaryChecker`), two configuration/data units (`LintConfig` and
`LintDictionary`), one heuristic helper (`PartOfSpeechGuesser`), two rule metadata units
(`RuleCodes` and `RuleCatalog`), and two supporting value
types (`Diagnostic` and `Severity`). Together they convert raw Markdown input into
deterministic lint findings aligned with the tool's official STE100 rule checks and
advisory heuristics.

The source folder contains eleven primary behavioral units plus two supporting value types:

- `Severity` - closed severity set (`Off`, `Warn`, `Error`) shared by configuration and
  diagnostics.
- `Diagnostic` - immutable finding record carrying file, location, rule code, severity,
  message, and optional suggestion.
- `LintConfig` - YAML-backed configuration model for file selection, mode overrides, rule
  tuning, and dictionary settings.
- `LintDictionary` - merged effective dictionary built from the embedded baseline,
  project-supplied dictionary file, and inline allow/disallow/ignore lists, with each term
  carrying one or more part-of-speech-tagged senses.
- `PartOfSpeechGuesser` - lightweight, deterministic regex/rule-based heuristic that
  guesses whether a matched term is used as a noun or a verb at its match site, used to
  select the applicable sense(s) of a multi-sense dictionary entry. Signals are split into
  a *strong* tier (specific, local, hard-to-confuse evidence such as a preceding
  article/possessive/modal, a following finite verb, a following bare numeral/identifier,
  or a noun-modifier directly before another noun) and a *weak* tier (the broad
  plural-noun-suffix and whole-segment-verbless fallbacks). `Guess` treats both tiers as a
  plain OR; `GuessEffective` additionally resolves on strong-tier evidence alone when the
  tiers disagree, and is the method `DictionaryChecker` and `StructuralRules`'s `-ing`-form
  check call. A dedicated `GuessIngFormRole` variant applies the same strong/weak-signal
  design to `-ing` words specifically, distinguishing a genuine present-participle verb
  use from a gerund/process noun, a material noun (for example a substance or component
  name ending in `-ing`), or a participial adjective modifying a following noun.
- `MarkdownProseExtractor` - line-based Markdown extractor that keeps headings, list items,
  table cells, and paragraphs, removing fenced code blocks and link destinations while
  retaining inline code spans verbatim. A table's header row (the row immediately followed
  by its `---` separator row) is tagged with the distinct `SegmentRole.TableHeader`, rather
  than `SegmentRole.TableRow`, so column-header label cells (for example "Hazard", "Use")
  can be treated as labels, not prose. The extractor also exposes
  `FindAdmonitionLabelSpans` (a bold label such as `**Caution.**`/`**Warning:**` at the
  very start of a block) and `FindQuotedOrEmphasisSpans` (a double-quoted or
  italicized/emphasized span, such as a cited document title) as span finders that
  `StructuralRules` and `DictionaryChecker` use to exclude label text and mentioned-not-used
  text from prose-oriented checks.
- `SentenceAnalyzer` - sentence splitter and rule-aware word counter for Rules 4.1 and
  8.4-8.7.
- `StructuralRules` - official Rule 4.1, Rule 8.1, and Rule 4.2 enforcement plus advisory
  paragraph-length, passive-voice, complex-verb (perfect/modal-perfect tense), and `-ing`
  form heuristics. The `-ing`-form check (`STE100-ADV-INGFORM`) delegates its role decision
  to `PartOfSpeechGuesser.GuessIngFormRole` and skips `Heading`/`TableHeader` segments and
  any admonition-label or quoted/emphasis span, so a heading, table header cell, label, or
  cited title never triggers the advisory. The passive-voice check
  (`STE100-ADV-PASSIVE`) treats an "is"/"are" + participle match as a predicate adjective,
  not a passive construction, when the participle is one of a curated set of common
  stative/adjectival participles (for example "energized", "closed", "seated",
  "unobstructed") or when the sentence is led by a common imperative verb (for example
  "Keep", "Confirm", "Stop") - unless an explicit "by \<agent\>" phrase immediately follows
  the participle, which always overrides both exemptions back to a genuine passive finding.
  This exemption is restricted to "is"/"are" auxiliary forms; "was"/"were"/"be"/"being"/
  "been" constructions are always evaluated as passive voice.
- `DictionaryChecker` - case-insensitive whole-term vocabulary checker using the effective
  merged dictionary and `PartOfSpeechGuesser` sense selection (via `GuessEffective`).
  `TableHeader` segments are skipped entirely (column-header labels are not prose), and a
  match that falls entirely inside an admonition-label or quoted/emphasis span (see
  `MarkdownProseExtractor` above) is also skipped, alongside the pre-existing inline-code-
  span and allowed-phrase exclusions. The flagged word's own text is always filtered
  (case-insensitively) out of its reported suggestion/citations, so a diagnostic never
  recommends the exact word it just flagged as one of its own alternatives.
- `DiagnosticReporter` - formatter for text and JSON diagnostic output.
- `Linter` - orchestration entry point that ties the subsystem together and drives the exit
  code.
- `RuleCodes` - single shared registry of rule-code string constants, referenced by
  `StructuralRules`, `DictionaryChecker`, and `RuleCatalog` so every emitted code has exactly
  one authoritative spelling.
- `RuleCatalog` - static, hand-authored catalog of every rule code the subsystem can emit,
  with title, classification (official/mechanical/advisory), suggestion kind, and applicable
  mode(s), serializable as JSON for `Program`'s `--list-rules` dispatch path.

The unit design details for the subsystem are documented in the companion unit files in
this folder: *Diagnostic Design*, *DiagnosticReporter Design*, *DictionaryChecker Design*,
*LintConfig Design*, *LintDictionary Design*, *Linter Design*, *MarkdownProseExtractor
Design*, *PartOfSpeechGuesser Design*, *RuleCatalog Design*, *RuleCodes Design*,
*SentenceAnalyzer Design*, *Severity Design*, and *StructuralRules Design*.

> **Dictionary notice:** The embedded default dictionary in
> `src/DemaConsulting.Ste100Mark/Linting/DefaultDictionary.yaml` is a small,
> originally-authored, representative example. It is **not** the official ASD-STE100 Part 2
> Dictionary. Each term carries one or more part-of-speech-tagged senses (noun/verb/
> adjective/adverb/any); a term with a single sense is always reported directly, while a
> multi-sense term is disambiguated by `PartOfSpeechGuesser` at lint time. Projects that
> require true ASD-STE100 Issue 9 compliance must supply your organization's licensed
> ASD-STE100 dictionary through `dictionary.file`, and should set
> `dictionary.use-embedded: false` to avoid mixing illustrative content with licensed
> vocabulary.

```mermaid
flowchart TD
    Linter --> LintConfig
    Linter --> LintDictionary
    Linter --> MarkdownProseExtractor
    MarkdownProseExtractor --> SentenceAnalyzer
    MarkdownProseExtractor --> DictionaryChecker
    SentenceAnalyzer --> StructuralRules
    LintConfig --> StructuralRules
    LintConfig --> LintDictionary
    LintDictionary --> DictionaryChecker
    DictionaryChecker --> PartOfSpeechGuesser
    StructuralRules --> DiagnosticReporter
    DictionaryChecker --> DiagnosticReporter
    Severity --> Diagnostic
    Diagnostic --> DiagnosticReporter
    RuleCodes --> RuleCatalog
    RuleCodes --> StructuralRules
    RuleCodes --> DictionaryChecker
```

### Interfaces

**Linter.Run**: Executes one complete lint pass.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Accepts a parsed `Context`. Resolves the configuration path, loads
  `LintConfig`, loads the effective `LintDictionary`, resolves the Markdown file set,
  extracts prose segments, evaluates structural and dictionary rules, reports the
  aggregated `Diagnostic` list through `DiagnosticReporter`, and sets the exit code through
  `Context.WriteError` or `Context.MarkFailure` when a failure condition is present.
- *Constraints*: Throws `ArgumentNullException` for a null `Context`. Configuration and
  dictionary load failures are caught within `Linter.Run` and converted into reported
  errors instead of propagating to `Program.Main`.

### Design

`Linter.Run` is the subsystem's only entry point. Its collaboration sequence is:

1. `LintConfig.Load` resolves the effective YAML configuration, including include/exclude
   globs, default writing mode, override globs, rule tuning, and dictionary options.
2. `LintDictionary.Load` merges the embedded illustrative dictionary (unless disabled),
   the optional project dictionary file, inline `disallow` entries, and the
   `allow`/`ignore` removal lists.
3. `Linter.ResolveFiles` computes the Markdown file set. Positional globs from the command
   line replace configured include/exclude patterns; otherwise the configuration controls
   the scope. Immediately afterward, `Linter` determines whether the resolved set is empty
   and `--allow-empty` was not specified; if so, it reports the zero-files-matched failure
   (exit code 2, `noFilesMatched: true`) and returns without evaluating rules, since there is
   nothing to lint.
4. For each file, `MarkdownProseExtractor.Extract` emits prose segments in document order.
   `SentenceAnalyzer.Split` then interprets those segments for Rule 4.1 word-limit and
   advisory paragraph/passive checks. `DictionaryChecker.Evaluate` inspects the same
   segment text against the merged dictionary.
5. `StructuralRules.Evaluate` and `DictionaryChecker.Evaluate` each return immutable
   `Diagnostic` values. `DiagnosticReporter.Report` formats the aggregated list either as
   line-oriented text or as one JSON document, threading through the zero-files-matched
   indicator computed in step 3.
6. `Linter` applies exit-code semantics: error-severity diagnostics always fail the run;
   warn-severity diagnostics also fail when `--strict` is active; a zero-matched file
   selection fails the run with the distinct `NoFilesMatchedExitCode` (2) unless
   `--allow-empty` was specified.

The subsystem reports the following rule codes:

- `STE100-4.1` - Sentence word-count limit, using the counting rules implemented for Rules
  8.4-8.7. Official STE100 rule.
- `STE100-8.1` - Semicolon prohibition in prose. Official STE100 rule.
- `STE100-4.2` - Contraction prohibition in prose. Official STE100 rule.
- `STE100-ADV-PARA` - Paragraph sentence-count cap. Advisory heuristic; not an official
  STE100 rule.
- `STE100-ADV-PASSIVE` - Passive-voice detection (`to be` + past participle heuristic).
  Advisory heuristic; not an official STE100 rule.
- `STE100-ADV-COMPLEXVERB` - Perfect and modal-perfect tense detection. Advisory
  heuristic; not an official STE100 rule.
- `STE100-ADV-INGFORM` - `-ing` form detection. Advisory heuristic; not an official
  STE100 rule.
- `STE100-DICT` - Effective dictionary/disallow-list enforcement. Tool-defined dictionary
  check; reported as an error.

`STE100-4.1`, `STE100-8.1`, and `STE100-4.2` are always emitted at error severity when the
corresponding check is enabled. `STE100-ADV-PARA` emits a warning when
`rules.max-sentences-paragraph` is greater than zero and a paragraph exceeds the
configured cap. `STE100-ADV-PASSIVE` emits at the severity configured by
`rules.passive-voice`. `STE100-ADV-COMPLEXVERB` emits at the severity configured by
`rules.complex-verb`, and `STE100-ADV-INGFORM` emits at the severity configured by
`rules.ing-form`. `STE100-DICT` depends on the effective dictionary content; when a
project provides its own licensed dictionary file, that file becomes the authoritative
vocabulary source for the subsystem.

`RuleCatalog` is now the single source of truth for the rule-code metadata above (title,
classification, suggestion kind, and applicable modes), consumed by
`Program`'s `--list-rules` dispatch. `RuleCodes` is the single source of truth for the code
strings themselves: `RuleCatalog`, `StructuralRules`, and `DictionaryChecker` all reference
the same `RuleCodes` constants rather than duplicating rule-code literals, so a code cannot
be added to an emitter without a compile-time-visible corresponding constant. Every rule code
emitted by `StructuralRules` or `DictionaryChecker` has a corresponding `RuleCatalog.Entries`
row.
