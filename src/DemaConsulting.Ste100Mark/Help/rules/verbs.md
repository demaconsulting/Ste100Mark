---
title: Passive voice, complex verbs, and -ing forms
summary: Fix the advisory STE100-ADV-PASSIVE, STE100-ADV-COMPLEXVERB, and STE100-ADV-INGFORM warnings.
aliases:
  - passive
  - passive-voice
  - complex-verb
  - ing-form
  - ste100-adv-passive
  - ste100-adv-complexverb
  - ste100-adv-ingform
tags:
  - reference
see-also:
  - configuration
examples:
  - name: warning
    kind: command
    language: shell
    description: An -ing verb form gives a warning. Exits 0.
    content: '{{tool}} advisory.md'
    expect: 'STE100-ADV-INGFORM'
  - name: strict
    kind: command
    language: shell
    description: With --strict, the warning fails the run. Exits 1.
    content: '{{tool}} advisory.md --strict'
    exit: 1
    expect: 'promoted to errors by --strict'
---
These three rules are heuristics of this tool, not ASD-STE100 rules. They
are warnings by default ([[#warning]]), so they fail the run only with
`--strict` ([[#strict]]) or when the configuration makes them errors.

**STE100-ADV-PASSIVE** reports a form of "be" followed by a past
participle, such as `is replaced` or `was completed`. A state after `is`
or `are`, such as `is closed` or `are energized`, is not reported, and
neither is such a state in an instruction such as `Keep the cover
closed`. Name the actor as the subject.

**STE100-ADV-COMPLEXVERB** reports a perfect tense (`has been replaced`)
or a modal perfect (`should have checked`). Use the simple past, present,
or future.

**STE100-ADV-INGFORM** reports an `-ing` word used as a verb, such as
`is checking`. Use a simple tense instead. A word used as a noun or an
adjective is not reported, and neither are headings, table header rows,
or a word in the `allow` list of the dictionary.

```text
Before: The valve has been replaced by the technician after the
        pressure test was completed.
After:  The technician replaced the valve after the pressure test.
Before: The technician is checking the gauge.
After:  The technician checks the gauge.
```

To change their severity, set `passive-voice`, `complex-verb`, or
`ing-form` to `off`, `warn`, or `error`; see [[configuration]].
