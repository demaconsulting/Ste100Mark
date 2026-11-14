package Ste100Mark {
    private import SoftwareItemMetadata::*;

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
}
