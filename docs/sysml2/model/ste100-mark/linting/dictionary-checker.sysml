package Ste100Mark_DictionaryChecker {
    private import SoftwareItemMetadata::*;
    private import Ste100Mark_Diagnostic::*;
    private import Ste100Mark_FindingDetail::*;
    private import Ste100Mark_LintDictionary::*;
    private import Ste100Mark_MarkdownProseExtractor::*;
    private import Ste100Mark_PartOfSpeechResolver::*;
    private import Ste100Mark_PhraseIndex::*;
    private import Ste100Mark_RuleCodes::*;
    private import Ste100Mark_Severity::*;
    private import Ste100Mark_WordFormInflector::*;

    part def DictionaryChecker {
        doc /* Matches extracted prose against the effective dictionary and reports vocabulary findings. */

        // Verification: verified through dictionary-check scenarios
        // Requirements: dictionary requirements
        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/DictionaryChecker.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/DictionaryCheckerTests.cs", "test/DemaConsulting.Ste100Mark.Tests/Linting/DictionaryCheckerWordFormTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/dictionary-checker.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }

    // The checker matches dictionary entries (with inflected forms) in extracted prose,
    // resolves word senses and phrase exclusions, and emits Diagnostic findings with detail.
    dependency DictionaryChecker to Diagnostic;
    dependency DictionaryChecker to FindingDetail;
    dependency DictionaryChecker to LintDictionary;
    dependency DictionaryChecker to MarkdownProseExtractor;
    dependency DictionaryChecker to PartOfSpeechResolver;
    dependency DictionaryChecker to PhraseIndex;
    dependency DictionaryChecker to RuleCodes;
    dependency DictionaryChecker to Severity;
    dependency DictionaryChecker to WordFormInflector;
}
