package Ste100Mark_LintingSubsystem {
    public import Ste100Mark_Severity::*;
    public import Ste100Mark_Diagnostic::*;
    public import Ste100Mark_LintConfig::*;
    public import Ste100Mark_LintDictionary::*;
    public import Ste100Mark_LintGlossary::*;
    public import Ste100Mark_PhraseIndex::*;
    public import Ste100Mark_WordFormInflector::*;
    public import Ste100Mark_PartOfSpeechResolver::*;
    public import Ste100Mark_PartOfSpeechGuesser::*;
    public import Ste100Mark_MarkdownProseExtractor::*;
    public import Ste100Mark_SentenceAnalyzer::*;
    public import Ste100Mark_StructuralRules::*;
    public import Ste100Mark_DictionaryChecker::*;
    public import Ste100Mark_FindingDetail::*;
    public import Ste100Mark_DiagnosticReporter::*;
    public import Ste100Mark_RuleCodes::*;
    public import Ste100Mark_RuleCatalog::*;
    public import Ste100Mark_Linter::*;
    private import SoftwareItemMetadata::*;

    part def LintingSubsystem {
        doc /* Markdown linting, configuration, dictionary merge, and diagnostic reporting. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ste100-mark/linting.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }

        part severity : Severity;
        part diagnostic : Diagnostic;
        part lintConfig : LintConfig;
        part lintDictionary : LintDictionary;
        part lintGlossary : LintGlossary;
        part phraseIndex : PhraseIndex;
        part wordFormInflector : WordFormInflector;
        part partOfSpeechResolver : PartOfSpeechResolver;
        part partOfSpeechGuesser : PartOfSpeechGuesser;
        part markdownProseExtractor : MarkdownProseExtractor;
        part sentenceAnalyzer : SentenceAnalyzer;
        part structuralRules : StructuralRules;
        part dictionaryChecker : DictionaryChecker;
        part findingDetail : FindingDetail;
        part diagnosticReporter : DiagnosticReporter;
        part ruleCodes : RuleCodes;
        part ruleCatalog : RuleCatalog;
        part linter : Linter;
    }
}
