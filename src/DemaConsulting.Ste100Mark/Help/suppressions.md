---
title: Suppressing findings
summary: Accept a false positive in the narrowest scope; there are no inline comments.
aliases:
  - suppress
  - ignore
  - allow
  - false-positives
  - disable
  - exceptions
tags:
  - reference
order: 6
see-also:
  - configuration
  - dictionaries
  - rules
examples:
  - name: config
    kind: source
    language: yaml
    save-as: suppress.yaml
    description: A configuration that accepts some findings.
    content: |
      rules:
        ing-form: off
      dictionary:
        allow:
          - utilize
        allow-in-phrase:
          - trail mix
      profiles:
        - glob: "docs/requirements/**/*.md"
          dictionary:
            allow:
              - shall
  - name: rule-off
    kind: command
    language: shell
    description: The -ing warning is turned off.
    content: '{{tool}} --config suppress.yaml advisory.md'
    expect: '0 warning(s)'
  - name: words
    kind: command
    language: shell
    description: The allowed word and phrase are not reported.
    content: '{{tool}} --config suppress.yaml vocabulary.md'
    expect: '0 error(s)'
  - name: snippets
    kind: command
    language: shell
    description: Print ready-made suppression entries for each word finding.
    content: '{{tool}} vocabulary.md --format json-detailed'
    exit: 1
    expect: '"kind": "allow-in-phrase"'
---
There are no inline suppression comments: an HTML comment is checked as
prose like any other text. Accept a finding in the configuration
instead ([[#config]]), and choose the narrowest scope that works:

1. Rewrite the text. This is the best fix.
2. Add an approved project term to a glossary; see [[dictionaries]].
3. Allow a phrase with `allow-in-phrase`, such as `trail mix`. The word
   `mix` stays reported everywhere else ([[#words]]).
4. Allow a word for some files with the `dictionary` of a profile.
5. Allow a word for all files with `allow` or `ignore`. An allowed word
   also gets no `STE100-ADV-INGFORM` warning.
6. Change a rule switch or severity, such as `ing-form: off`
   ([[#rule-off]]), for all files or in a profile; see [[rules]].
7. Leave files out with `exclude`. Globs on the command line replace
   `include`, and `exclude` is then not applied.

`--format json-detailed` prints, for each word finding, ready-made
`allow` and `allow-in-phrase` entries with the warning
`Use only for a true false positive` ([[#snippets]]).
