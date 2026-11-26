---
title: Word findings
summary: Fix STE100-DICT findings for words that are not approved, or approved only in one role.
aliases:
  - ste100-dict
  - dict
  - word-findings
  - vocabulary
tags:
  - reference
see-also:
  - dictionaries
  - suppressions
examples:
  - name: word
    kind: command
    language: shell
    description: A word that the dictionary does not approve. Exits 1.
    content: '{{tool}} vocabulary.md'
    exit: 1
    expect: "Avoid 'utilize'; use 'use' instead."
---
**STE100-DICT** reports a word or phrase of the dictionary, and it is
always an error. The message tells you what to do:

- `Avoid 'x'; use 'y' instead.` Use the suggested word ([[#word]]). When
  the message says that the alternative is in its base form, adjust it to
  fit the sentence.
- `Avoid 'x' (a form of 'y')` names the dictionary word that an inflected,
  split, or prefixed form came from.
- `Avoid using 'x' as a noun` (or another role): the word is approved
  only in a different role. Rewrite the sentence so that it uses the word
  in that role, or use another word.
- `Ambiguous part of speech for 'x'`: the tagger is not sure of the role.
  Choose the correction that matches your meaning.
- `... not an approved ASD-STE100-style term`: replace the word.

```text
Before: Do not utilize the old valve.
After:  Do not use the old valve.
```

The tool decides the role of a word with a part-of-speech tagger that is
part of the tool and runs offline. Words in quotes or in `*emphasis*`,
the header row of a table, and a bold label such as `**Note.**` at the
start of a block are not checked.

The words come from a dictionary; see [[dictionaries]]. For a false
positive, see [[suppressions]].
