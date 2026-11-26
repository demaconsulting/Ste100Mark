# OTS Software Verification

This section provides verification evidence for all Off-The-Shelf (OTS) software items
used by the Ste100Mark.

## Scope

Each OTS item is verified by a combination of:

- The OTS tool's own self-validation suite (where the tool supports `--validate`)
- Pipeline output assertions performed by FileAssert on documents produced by the tool
- Transitive evidence from the project's own passing test suite (for library-type OTS items
  with no self-validation suite of their own, such as xUnit, YamlDotNet, PartOfSpeech,
  Microsoft.Extensions.FileSystemGlobbing, and HelpCards)

Internal OTS tool design is out of scope; only integration and usage evidence is documented.

## OTS Items

The following OTS items have verification evidence in this section:

- BuildMark (*buildmark.md*) — build-notes documentation tool
- FileAssert (*fileassert.md*) — document assertion tool
- HelpCards (*help-cards.md*) — built-in help library
- Microsoft.Extensions.FileSystemGlobbing (*file-system-globbing.md*) — glob matching library
- Pandoc (*pandoc.md*) — Markdown-to-HTML conversion tool
- ReqStream (*reqstream.md*) — requirements traceability tool
- ReviewMark (*reviewmark.md*) — file review enforcement tool
- SarifMark (*sarifmark.md*) — SARIF report conversion tool
- SonarMark (*sonarmark.md*) — SonarCloud quality report tool
- SysML2Tools (*sysml2tools.md*) — architecture model validation and diagram rendering tool
- VersionMark (*versionmark.md*) — tool-version documentation tool
- WeasyPrint (*weasyprint.md*) — HTML-to-PDF conversion tool
- PartOfSpeech (*part-of-speech.md*) — part-of-speech tagging library
- xUnit (*xunit.md*) — unit-testing framework
- YamlDotNet (*yamldotnet.md*) — YAML parsing library
