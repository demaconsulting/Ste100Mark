package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def MarkdownProseExtractor {
        doc /* Extracts lintable prose segments from Markdown while skipping code and link targets. */

        // Verification: verified through extraction scenarios
        // Requirements: word-limit and extraction-related requirements
        metadata info : SoftwareInfo {
            description = "Extracts lintable prose segments from Markdown while skipping code and link targets.";
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/MarkdownProseExtractor.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/MarkdownProseExtractorTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/markdown-prose-extractor.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }
}
