---
title: Configuration file
summary: 'The .ste100mark.yaml file: files to check, modes, profiles, and rules.'
aliases:
  - config
  - ste100mark-yaml
  - settings
  - profiles
  - modes
tags:
  - reference
order: 4
see-also:
  - rules
  - dictionaries
  - suppressions
examples:
  - name: sample
    kind: source
    language: yaml
    save-as: ste100mark.yaml
    description: A complete configuration file with two profiles.
    content: |
      include:
        - "**/*.md"
      exclude:
        - "**/generated/**"
      default-mode: descriptive
      profiles:
        - glob: "procedure*.md"
          mode: procedure
          dictionary:
            allow-in-phrase:
              - check valve
        - glob: "paragraph.md"
          rules:
            max-sentences-paragraph: 0
      rules:
        max-words-procedure: 20
        max-words-descriptive: 25
        allow-semicolons: false
        allow-contractions: false
        max-sentences-paragraph: 6
        passive-voice: warn
        complex-verb: warn
        ing-form: warn
      dictionary:
        enabled: true
        use-embedded: true
        match-word-forms: true
        allow:
          - ste100mark
        ignore: []
        allow-in-phrase:
          - trail mix
        disallow:
          leverage:
            - pos: verb
              alternatives: [use]
              note: Prefer the plain verb.
        glossary-file: []
        deprecated-file: []
        glossary-mode: hard
        glossary:
          Zorbix: The brand name of the pump.
  - name: procedure
    kind: command
    language: shell
    description: A profile puts a file in procedure mode. Exits 1.
    content: '{{tool}} --config ste100mark.yaml procedure.md'
    exit: 1
    expect: '20-word limit for procedure mode'
  - name: profile-rules
    kind: command
    language: shell
    description: A profile turns off the paragraph warning for one file.
    content: '{{tool}} --config ste100mark.yaml paragraph.md'
    expect: '0 warning(s)'
  - name: missing
    kind: command
    language: shell
    description: A missing configuration file is an error. Exits 1.
    content: '{{tool}} --config missing.yaml fixed.md'
    exit: 1
    expect: "Lint configuration file 'missing.yaml' not found."
---
The tool reads `.ste100mark.yaml` from the current folder only, or the
file that `--config` names. Without one, it checks `**/*.md` with the
defaults. [[#sample]] prints a complete file; its `rules` and
`dictionary` values are the defaults, apart from the lists and the
glossary.

| Key | Meaning |
| --- | --- |
| `include` | Globs of the files to check; default `**/*.md`. |
| `exclude` | Globs of files to leave out. |
| `default-mode` | `descriptive` (25 words) or `procedure` (20 words). |
| `profiles` | Settings for the files that a `glob` matches. |
| `rules` | Limits, switches, and severities; see [[rules]]. |
| `dictionary` | Word lists and files; see [[dictionaries]]. |

**Paths.** The globs of `include`, `exclude`, and `profiles` are relative
to the current folder, so run the tool from the repository root. The
file paths in `dictionary` are relative to the folder of the
configuration file. Globs on the command line replace `include`, and
`exclude` is then not applied.

**Profiles.** A profile has a `glob` and any of `mode`, `rules`, and
`dictionary`. The first matching profile that sets a `mode` decides the
mode ([[#procedure]]). The `rules` of every matching profile apply in
order ([[#profile-rules]]). The `dictionary` of a profile takes only
`allow`, `ignore`, and `allow-in-phrase`.

**Mistakes.** An unknown key is silently ignored, so check the spelling
of every key. A wrong value, such as `passive-voice: sometimes`, fails
with `Failed to parse lint configuration file`. A missing file named by
`--config` is an error ([[#missing]]).
