package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def WordFormInflector {
        doc /* Generates the inflected spellings of a dictionary term and inflects replacement words to a given form. */

        // Verification: verified through verb, noun, phrasal-verb, and unsafe-case scenarios
        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/WordFormInflector.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/WordFormInflectorTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/word-form-inflector.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }
}
