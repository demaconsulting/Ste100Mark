---
title: Continuous integration
summary: Run the tool as a writing-quality gate in a CI pipeline.
aliases:
  - pipeline
  - github-actions
  - quality-gate
  - gate
  - continuous-integration
tags:
  - tutorial
order: 8
see-also:
  - getting-started
  - output
  - self-validation
examples:
  - name: github-actions
    kind: snippet
    language: yaml
    save-as: lint-docs.yml
    description: A GitHub Actions workflow that checks the docs.
    content: |
      name: Docs
      on: [push, pull_request]
      jobs:
        ste100:
          runs-on: ubuntu-latest
          steps:
            - uses: actions/checkout@v7
            - uses: actions/setup-dotnet@v6
              with:
                dotnet-version: 10.x
            - run: dotnet tool restore
            - run: dotnet {{tool}} "docs/**/*.md" --strict
  - name: no-files
    kind: command
    language: shell
    description: A glob that matches no files fails the run. Exits 2.
    content: '{{tool}} "missing/**/*.md"'
    exit: 2
    expect: 'found no files to check'
  - name: allow-empty
    kind: command
    language: shell
    description: Accept an empty selection on purpose. Exits 0.
    content: '{{tool}} "missing/**/*.md" --allow-empty'
    expect: 'Checked 0 file(s)'
  - name: json-gate
    kind: command
    language: shell
    description: A strict gate with JSON output for other tools. Exits 1.
    content: '{{tool}} "*.md" --format json --strict'
    exit: 1
    expect: '"errorCount"'
---

1. **Pin the tool.** Install it in the tool manifest of the repository,
   as [[getting-started]] shows, and run `dotnet tool restore` in the
   pipeline before `dotnet {{tool}}`. [[#github-actions]] prints a
   complete workflow.
2. **Run from the repository root.** Globs, `.ste100mark.yaml`, and the
   globs of profiles are relative to the current folder. Quote the globs
   so that the shell does not expand them.
3. **Choose the gate.** By default, only errors fail the run. Add
   `--strict` to fail on warnings too. A selection that matches no files
   fails ([[#no-files]]), so that a mistyped glob cannot pass; add
   `--allow-empty` when that is expected ([[#allow-empty]]). Use
   `--format json` when another tool reads the findings
   ([[#json-gate]]); see [[output]].

| Exit code | Meaning |
| --- | --- |
| 0 | No errors (warnings pass without `--strict`). |
| 1 | Errors, or an argument or configuration error. |
| 2 | The file selection matched no files. |

For qualification evidence of the tool itself, add a step that runs
`--validate --results`; see [[self-validation]].
