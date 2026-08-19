package Ste100Mark {
    part def LintingSubsystem {
        doc /* Markdown linting, configuration, dictionary merge, and diagnostic reporting. */

        comment designRef /* Design: docs/design/ste100-mark/linting.md */
        comment verificationRef /* Verification: docs/verification/ste100-mark/linting.md */
        comment reqRef /* Requirements: docs/reqstream/ste100-mark/linting.yaml */

        part severity : Severity;
        part diagnostic : Diagnostic;
        part lintConfig : LintConfig;
        part lintDictionary : LintDictionary;
        part partOfSpeechGuesser : PartOfSpeechGuesser;
        part markdownProseExtractor : MarkdownProseExtractor;
        part sentenceAnalyzer : SentenceAnalyzer;
        part structuralRules : StructuralRules;
        part dictionaryChecker : DictionaryChecker;
        part diagnosticReporter : DiagnosticReporter;
        part linter : Linter;
    }
}
