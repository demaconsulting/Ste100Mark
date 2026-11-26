---
name: ste100mark
schema: 2
title: Ste100Mark
summary: Built-in help of the ASD-STE100 prose linter for Markdown.
---
{{tool}} checks the prose of Markdown files against ASD-STE100 (Issue 9)
Simplified Technical English rules, and reports each finding with a rule
code and an exit code for CI.

Every operation is an option, and other arguments are file globs. The only
command is `{{tool}} help`, which must be the first argument and takes topic
names, such as `{{tool}} help rules`. To check a file named `help`, write
`./help`.
