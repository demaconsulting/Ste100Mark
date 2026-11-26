---
title: Output formats
summary: The text and JSON reports, the detailed JSON evidence, and the rule catalog.
aliases:
  - formats
  - format
  - json
  - json-detailed
  - reports
tags:
  - reference
order: 7
see-also:
  - command-line
  - ci
examples:
  - name: json
    kind: command
    language: shell
    description: Print the findings as one JSON document. Exits 1.
    content: '{{tool}} draft.md --format json'
    exit: 1
    expect: '"ruleCode": "STE100-8.1"'
  - name: detailed
    kind: command
    language: shell
    description: Add the evidence of each word finding. Exits 1.
    content: '{{tool}} vocabulary.md --format json-detailed --context-lines 0'
    exit: 1
    expect: '"schema": "ste100mark-detailed/1"'
  - name: log
    kind: command
    language: shell
    description: Keep the text report in a log file only. Exits 1.
    content: '{{tool}} draft.md --silent --log lint.log'
    exit: 1
---
**Text** (the default). The banner, one line per finding, and a summary
line such as `Checked 1 file(s): 4 error(s), 3 warning(s).` A failed run
also writes a line such as `Ste100Mark found linting issues (4 error(s)).`
to the error output. When no file matches, a message says so.

**JSON** (`--format json`, [[#json]]). One JSON document, with no banner
and no failure line. Its root has `filesChecked`, `noFilesMatched`,
`errorCount`, `warningCount`, `files` (each with `path` and `status`),
and `diagnostics`. Each diagnostic has `file`, `line`, `column`,
`ruleCode`, `severity`, `message`, `suggestion`, and `citations`.

**Detailed JSON** (`--format json-detailed`, [[#detailed]]). The JSON
document with `schema` and `contextLines` at the root. Each word finding
also has a `detail` with the span, the sentence, the context lines, the
matched term, the tagger result, a confidence class (`certain`,
`probable`, or `doubtful`) with a reason, the suggestions, and the
`suppression` entries; see [[suppressions]]. `--context-lines` sets the
lines of context, 0 to 10 (default 2), and is valid only with this
format.

**Rule catalog.** `--list-rules` prints a JSON array with the `code`,
`title`, `classification`, `suggestionKind`, and `modes` of each rule.

**Log.** `--log <file>` also writes the console output to a file, and
with `--silent` only to the file ([[#log]]).
