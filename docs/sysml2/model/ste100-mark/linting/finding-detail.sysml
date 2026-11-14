package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def FindingDetail {
        doc /* Builds the detail object of a dictionary finding for the detailed JSON format: location, sentence, context lines, match and tagger evidence, confidence class, and suppression hints. */

        // Verification: verified through detail-building, confidence-table, and suppression-hint scenarios
        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/FindingDetail.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/FindingDetailTests.cs", "test/DemaConsulting.Ste100Mark.Tests/Linting/DictionaryCheckerDetailTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/finding-detail.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting/finding-detail.yaml");
        }
    }
}
