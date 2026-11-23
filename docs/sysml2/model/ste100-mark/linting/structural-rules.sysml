package Ste100Mark_StructuralRules {
    private import SoftwareItemMetadata::*;
    private import Ste100Mark_Diagnostic::*;
    private import Ste100Mark_LintConfig::*;
    private import Ste100Mark_MarkdownProseExtractor::*;
    private import Ste100Mark_PartOfSpeechGuesser::*;
    private import Ste100Mark_RuleCodes::*;
    private import Ste100Mark_SentenceAnalyzer::*;
    private import Ste100Mark_Severity::*;

    part def StructuralRules {
        doc /* Evaluates official structural rules and advisory prose heuristics over extracted segments. */

        // Verification: verified through structural-rule scenarios
        // Requirements: mechanical and advisory rule requirements
        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/StructuralRules.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/StructuralRulesTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/structural-rules.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }

    // Structural rules read prose segments, split sentences, resolve -ing word roles, take
    // mode and rule tuning from LintConfig, and emit Diagnostic findings with rule codes and severities.
    dependency StructuralRules to MarkdownProseExtractor;
    dependency StructuralRules to SentenceAnalyzer;
    dependency StructuralRules to PartOfSpeechGuesser;
    dependency StructuralRules to LintConfig;
    dependency StructuralRules to Diagnostic;
    dependency StructuralRules to RuleCodes;
    dependency StructuralRules to Severity;
}
