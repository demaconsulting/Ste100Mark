---
title: Dictionaries and glossaries
summary: Supply your own word lists, approved terms, and deprecated terms.
aliases:
  - dictionary
  - glossary
  - deprecated
  - approved-words
  - word-list
  - terms
tags:
  - reference
order: 5
see-also:
  - rules/dictionary
  - configuration
  - suppressions
examples:
  - name: terms
    kind: source
    language: yaml
    save-as: terms.yaml
    description: A dictionary file of words to avoid.
    content: |
      utilize:
        - pos: verb
          alternatives: [use]
          note: Prefer the shorter, more common word.
      mix:
        - pos: noun
          alternatives: [mixture]
  - name: glossary
    kind: source
    language: yaml
    save-as: glossary.yaml
    description: A glossary of approved project terms.
    content: |
      Zorbix:
        definition: The brand name of the pump.
        pos: proper-noun
      test mode: A mode for acceptance tests.
  - name: deprecated
    kind: source
    language: yaml
    save-as: deprecated.yaml
    description: Old terms and the terms to use instead.
    content: |
      Product A:
        use: Product B
        where: Documents before 2.0.
  - name: config
    kind: source
    language: yaml
    save-as: vocabulary.yaml
    description: A configuration that uses only these three files.
    content: |
      dictionary:
        file: terms.yaml
        use-embedded: false
        glossary-file: glossary.yaml
        deprecated-file: deprecated.yaml
  - name: check
    kind: command
    language: shell
    description: Check a file with the project word lists. Exits 1.
    content: '{{tool}} --config vocabulary.yaml vocabulary.md'
    exit: 1
    expect: "Avoid 'Product A'; use 'Product B' instead."
  - name: embedded
    kind: command
    language: shell
    description: The same file with the embedded dictionary only. Exits 1.
    content: '{{tool}} vocabulary.md'
    exit: 1
    expect: "Avoid 'utilize'"
---
The dictionary lists the words to avoid. The embedded dictionary is a
short example, not the ASD-STE100 dictionary. For compliance, supply your
licensed dictionary with `file` and set `use-embedded: false`, as in
[[#config]]. All keys below go in the `dictionary` section.

**Dictionary file** ([[#terms]]). Each word or phrase has a list of
senses with `pos` (`noun`, `verb`, `adjective`, `adverb`, or `any`), the
`alternatives` to use, and an optional `note`. The tool merges the
embedded dictionary, then `file`, then the inline `disallow` list; a
later entry replaces an earlier one for the same word. `allow` and
`ignore` remove words. With `match-word-forms: true` (the default),
inflected forms such as `utilized` are also reported.

**Glossary** ([[#glossary]]). Approved terms, which are never reported.
An entry is `term: definition`, or a map with `definition` (required),
`pos` (`noun`, `verb`, `adjective`, `adverb`, or `proper-noun`),
`variants`, and `visible`. A term of several words also protects the
words inside it. `glossary-mode: hard` (the default) makes the `pos` of
a term final; `soft` lets the tagger disagree.

**Deprecated terms** ([[#deprecated]]). Each old term names the term to
`use` and, optionally, `where` it applies. It is reported as
`Avoid 'old'; use 'new' instead.`, as in [[#check]].

`glossary-file` and `deprecated-file` take a path, a glob, or a list, and
may use `${VAR}` environment variables. Relative paths start at the
folder of the configuration file. A missing file is an error, and so is a
conflict, such as a glossary term that is also a dictionary entry. With
`enabled: false`, no word is checked.
