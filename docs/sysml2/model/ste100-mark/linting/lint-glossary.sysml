package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def LintGlossary {
        doc /* Loads, merges, and validates the project glossary and deprecated terms, and builds the part-of-speech tagger glossary. */

        // Verification: verified through glossary loading, conflict, and path-resolution scenarios
        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/LintGlossary.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/LintGlossaryTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/lint-glossary.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting/lint-glossary.yaml");
        }
    }
}
