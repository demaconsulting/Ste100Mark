package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def LintingSubsystem {
        doc /* Markdown linting, configuration, dictionary merge, and diagnostic reporting. */

        metadata info : SoftwareInfo {
            description = "Markdown linting, configuration, dictionary merge, and diagnostic reporting.";
            designFiles = ("docs/design/ste100-mark/linting.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }

        part severity : Severity;
        part diagnostic : Diagnostic;
        part lintConfig : LintConfig;
        part lintDictionary : LintDictionary;
        part partOfSpeechResolver : PartOfSpeechResolver;
        part partOfSpeechGuesser : PartOfSpeechGuesser;
        part markdownProseExtractor : MarkdownProseExtractor;
        part sentenceAnalyzer : SentenceAnalyzer;
        part structuralRules : StructuralRules;
        part dictionaryChecker : DictionaryChecker;
        part diagnosticReporter : DiagnosticReporter;
        part ruleCodes : RuleCodes;
        part ruleCatalog : RuleCatalog;
        part linter : Linter;
    }
}
