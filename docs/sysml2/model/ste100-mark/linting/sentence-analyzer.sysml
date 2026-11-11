package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def SentenceAnalyzer {
        doc /* Splits prose into sentences and counts words using the implemented STE100 counting rules. */

        // Verification: verified through sentence-analysis scenarios
        // Requirements: word-limit requirements
        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/SentenceAnalyzer.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/SentenceAnalyzerTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/sentence-analyzer.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }
}
