---
title: Getting started
summary: Install the tool, check a first file, and read and fix its findings.
aliases:
  - start
  - quickstart
  - examples
  - install
tags:
  - tutorial
order: 1
see-also:
  - command-line
  - rules
  - ci
examples:
  - name: install-local
    kind: snippet
    language: shell
    save-as: install-local.sh
    description: Add the tool to the tool manifest of a repository.
    content: |
      dotnet new tool-manifest
      dotnet tool install --local DemaConsulting.Ste100Mark
  - name: ci-steps
    kind: snippet
    language: yaml
    save-as: ci-steps.yaml
    description: GitHub Actions steps that set up .NET and run the tool.
    content: |
      - name: Check out the repository
        uses: actions/checkout@v4
      - name: Set up .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.x
      - name: Restore the local .NET tools
        run: dotnet tool restore
      - name: Run {{tool}}
        run: dotnet {{tool}}
  - name: version
    kind: command
    language: shell
    description: Print the version and exit.
    content: '{{tool}} --version'
  - name: lint
    kind: command
    language: shell
    description: Check a draft with findings. Exits 1 on errors.
    content: '{{tool}} draft.md'
    exit: 1
    expect: 'STE100-DICT'
  - name: clean
    kind: command
    language: shell
    description: Check the rewritten text. Exits 0.
    content: '{{tool}} fixed.md'
    expect: 'Checked 1 file(s): 0 error(s), 0 warning(s).'
  - name: validate
    kind: command
    language: shell
    description: Run the built-in self-tests. Exits 0 when all pass.
    content: '{{tool}} --validate'
    expect: 'Failed: 0'
  - name: topic
    kind: command
    language: shell
    description: Print a help topic, here the rule codes.
    content: '{{tool}} help rules'
    expect: 'STE100-4.1'
---
{{tool}} {{version}} is a .NET tool.
It reads only the Markdown, configuration, dictionary, and glossary files
that you name or that its configuration names. It writes only the files
of `--log` and `--results`, apart from a temporary folder that
`--validate` uses.

1. **Install it.** The tool needs the .NET 10 SDK or later. For a
   repository or CI, install it as a local tool. Run the commands
   that [[#install-local]] prints once, without
   `dotnet new tool-manifest` when `.config/dotnet-tools.json` exists, and
   commit that file. In GitHub Actions, copy the steps that
   [[#ci-steps]] prints. They set up .NET 10 with
   `actions/setup-dotnet`, run `dotnet tool restore`, and then run
   `dotnet {{tool}}` from the repository root, where it reads
   `.ste100mark.yaml`; see [[ci]].
   For one machine, install it globally and run it as `{{tool}}`:

   ```bash
   dotnet tool install -g DemaConsulting.Ste100Mark
   ```

2. **Check a file.** Give the files to check as paths or globs, as in
   [[#lint]].
   With no arguments, the tool checks `**/*.md` below the current
   folder, or the files that `.ste100mark.yaml` names.
3. **Read the findings.** Each finding prints as
   `file:line: [ERROR] CODE — message (Suggestion: ...)`, or with `[WARN]`
   for an advisory finding. A summary line counts the files, errors, and
   warnings.
4. **Fix them.** Rewrite the text as the suggestion says.
   [[rules]] explains every rule code and how to rewrite the text.
   The rewrite in [[#clean]] passes.
   To tune the rules, see [[configuration]].
   To accept a false positive, see [[suppressions]].

The exit code is 0 when no errors were found (warnings alone pass), 1 for
errors, and 2 when the file selection matched no files; see [[ci]].

The command that [[#version]] prints shows the version.
The command that [[#validate]] prints runs the self-tests;
see [[self-validation]]. Print any help topic with
`{{tool}} help <topic>`, as in [[#topic]]. Add `--search <words>` to
find a topic, and `--json` to get any help answer as JSON.
