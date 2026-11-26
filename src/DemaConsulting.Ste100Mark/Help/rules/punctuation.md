---
title: Semicolons and contractions
summary: Fix STE100-8.1 (semicolons) and STE100-4.2 (contractions).
aliases:
  - semicolons
  - contractions
  - ste100-8-1
  - ste100-4-2
tags:
  - reference
see-also:
  - configuration
examples:
  - name: draft
    kind: command
    language: shell
    description: A sentence with a semicolon and a contraction. Exits 1.
    content: '{{tool}} draft.md'
    exit: 1
    expect: "Contraction 'can't' is not permitted"
  - name: fixed
    kind: command
    language: shell
    description: The same text, rewritten. Exits 0.
    content: '{{tool}} fixed.md'
    expect: '0 error(s)'
---
**STE100-8.1** reports a semicolon. It reports only the first semicolon
of each paragraph, list item, or table cell, so fix them all before you
run the tool again. Make two sentences instead.

**STE100-4.2** reports each contraction, such as `can't` or `don't`, and
the fix is to write the words in full. A possessive `'s` is not reported,
except after words such as `it`, `that`, and `there`, where `'s` means
"is" or "has".

```text
Before: Do not utilize the old valve; it can't hold the pressure.
After:  Do not use the old valve. It cannot hold the pressure.
```

[[#draft]] reports both rules for the first line.
[[#fixed]] passes.
Text in inline code is never reported. To turn the rules off, set
`allow-semicolons: true` or `allow-contractions: true`; see
[[configuration]].
