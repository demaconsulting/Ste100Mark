package Ste100Mark_Linter {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;
    private import Ste100Mark_Context::*;
    private import Ste100Mark_Diagnostic::*;
    private import Ste100Mark_DiagnosticReporter::*;
    private import Ste100Mark_DictionaryChecker::*;
    private import Ste100Mark_FindingDetail::*;
    private import Ste100Mark_LintConfig::*;
    private import Ste100Mark_LintDictionary::*;
    private import Ste100Mark_LintGlossary::*;
    private import Ste100Mark_MarkdownProseExtractor::*;
    private import Ste100Mark_PartOfSpeechResolver::*;
    private import Ste100Mark_PhraseIndex::*;
    private import Ste100Mark_Severity::*;
    private import Ste100Mark_StructuralRules::*;

    part def Linter {
        doc /* End-to-end lint orchestrator and the subsystem's single externally called entry point. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/Linter.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/LinterTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/linter.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }

    // The Linter orchestrates a run: it reads inputs from Context, loads configuration,
    // dictionary, and glossary, extracts prose, runs both rule engines, and reports the
    // results, using Severity to compute the exit code.
    dependency Linter to Context;
    dependency Linter to LintConfig;
    dependency Linter to LintDictionary;
    dependency Linter to LintGlossary;
    dependency Linter to PhraseIndex;
    dependency Linter to PartOfSpeechResolver;
    dependency Linter to MarkdownProseExtractor;
    dependency Linter to StructuralRules;
    dependency Linter to DictionaryChecker;
    dependency Linter to FindingDetail;
    dependency Linter to Diagnostic;
    dependency Linter to DiagnosticReporter;
    dependency Linter to Severity;
    dependency Linter to FileSystemGlobbing;
}
