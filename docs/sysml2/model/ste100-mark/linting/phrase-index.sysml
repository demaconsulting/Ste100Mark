package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def PhraseIndex {
        doc /* Token index over approved multi-word glossary phrases that tests whether a text span lies inside one of them. */

        // Verification: verified through phrase containment unit tests and a differential test against the former combined regex
        // Requirements: glossary phrase allowance requirement
        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/PhraseIndex.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/PhraseIndexTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/phrase-index.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }
}
