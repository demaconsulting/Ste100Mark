---
title: Rule codes
summary: What each rule code means, its severity, and how to turn it off.
aliases:
  - rule-codes
  - codes
  - checks
  - findings
  - list-rules
tags:
  - reference
order: 3
see-also:
  - configuration
  - suppressions
examples:
  - name: list
    kind: command
    language: shell
    description: Print the rule catalog as JSON.
    content: '{{tool}} --list-rules'
    expect: '"code": "STE100-ADV-INGFORM"'
  - name: draft
    kind: command
    language: shell
    description: Check a draft that breaks most rules. Exits 1.
    content: '{{tool}} draft.md'
    exit: 1
    expect: '[WARN] STE100-ADV-PASSIVE'
---
Each finding names a rule code. Official rules are ASD-STE100 rules and
are errors. Advisory rules are heuristics of this tool, not ASD-STE100
rules, and are warnings by default.

| Code | Kind | Severity | Change it in `rules` |
| --- | --- | --- | --- |
| `STE100-4.1` | official | error | `max-words-descriptive`, `max-words-procedure` |
| `STE100-8.1` | official | error | `allow-semicolons: true` |
| `STE100-4.2` | official | error | `allow-contractions: true` |
| `STE100-DICT` | dictionary | error | `enabled: false` in `dictionary` |
| `STE100-ADV-PARA` | advisory | warn | `max-sentences-paragraph: 0` |
| `STE100-ADV-PASSIVE` | advisory | warn | `passive-voice: off`, `warn`, `error` |
| `STE100-ADV-COMPLEXVERB` | advisory | warn | `complex-verb: off`, `warn`, `error` |
| `STE100-ADV-INGFORM` | advisory | warn | `ing-form: off`, `warn`, `error` |

`STE100-4.1` has no off switch; raise the limit instead. `STE100-ADV-PARA`
is always a warning. Errors fail the run (exit 1). Warnings fail it only
with `--strict`.

**What is checked.** Headings, paragraphs, list items, and each table
cell. Fenced code, YAML front matter, and link URLs are skipped. Inline
code counts as one word, and no rule checks the text inside it.

**How to fix each rule.** Look up a code in lower case, as in
`{{tool}} help rules ste100-adv-passive`.

- Sentence and paragraph length: [[rules/length]].
- Semicolons and contractions: [[rules/punctuation]].
- Word findings (`STE100-DICT`): [[rules/dictionary]].
- Passive voice, complex verbs, and `-ing` forms: [[rules/verbs]].

[[#draft]] shows most of the rules at once.
[[#list]] prints the catalog, with the modes that each rule applies to.
