package Ste100Mark_PhraseIndex {
    private import SoftwareItemMetadata::*;

    part def PhraseIndex {
        doc /* Token index over approved phrases (glossary and allow-in-phrase) that tests whether a text span lies inside one of them. */

        // Verification: verified through phrase containment unit tests and a differential test against the former per-phrase regex implementation
        // Requirements: glossary phrase allowance and explicit phrase allowance requirements
        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/PhraseIndex.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/PhraseIndexTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/phrase-index.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }
}
