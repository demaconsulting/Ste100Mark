package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def PartOfSpeechGuesser {
        doc /* Deterministic regex/rule-based heuristic deciding whether an -ing word is a verb or a gerund/adjective. */

        // Test: verified through -ing form scenarios
        // Verification: verified through -ing form scenarios
        // Requirements: -ing form requirements
        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/PartOfSpeechGuesser.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/StructuralRulesTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/part-of-speech-guesser.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }
}
