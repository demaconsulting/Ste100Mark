---
title: Sentence and paragraph length
summary: Fix STE100-4.1 (too many words in a sentence) and STE100-ADV-PARA (too many sentences).
aliases:
  - sentence-length
  - word-limit
  - ste100-4-1
  - paragraphs
  - ste100-adv-para
tags:
  - reference
see-also:
  - configuration
examples:
  - name: long-sentence
    kind: command
    language: shell
    description: A 27-word sentence breaks the 25-word limit. Exits 1.
    content: '{{tool}} draft.md'
    exit: 1
    expect: 'exceeding the 25-word limit for descriptive mode'
  - name: long-paragraph
    kind: command
    language: shell
    description: A paragraph of seven sentences gives a warning. Exits 0.
    content: '{{tool}} paragraph.md'
    expect: 'STE100-ADV-PARA'
  - name: descriptive
    kind: command
    language: shell
    description: A 22-word instruction passes in descriptive mode.
    content: '{{tool}} procedure.md'
    expect: '0 error(s)'
---
**STE100-4.1** reports a sentence with more words than the limit of its
mode: 25 in `descriptive` mode (the default) and 20 in `procedure` mode.
The message gives the count, the limit, the mode, and the start of the
sentence ([[#long-sentence]]). Each list item and each table cell counts
as its own sentence, and inline code counts as one word.

To fix it, split the sentence where a condition or a second action
starts, and put the condition first:

```text
Before: When the operator opens the inlet valve and the pressure in the
        main supply line rises above the limit, the relief valve opens
        to release the pressure.
After:  When the pressure in the supply line rises above the limit, the
        relief valve opens.
```

Instructions use the shorter limit only in `procedure` mode, which a
profile sets for some files; [[#descriptive]] passes in the default mode.
See [[configuration]].

**STE100-ADV-PARA** is an advisory warning for a paragraph with more than
`max-sentences-paragraph` sentences (6 by default), as in
[[#long-paragraph]]. Split the paragraph, or make the steps a list. It
applies to paragraphs only, and 0 turns it off.
