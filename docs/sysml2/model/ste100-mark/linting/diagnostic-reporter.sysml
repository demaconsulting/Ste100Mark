package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def DiagnosticReporter {
        doc /* Formats aggregated lint diagnostics as text lines or a single JSON document. */

        // Verification: verified through output-format scenarios
        // Requirements: output-format requirements
        metadata info : SoftwareInfo {
            description = "Formats aggregated lint diagnostics as text lines or a single JSON document.";
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/DiagnosticReporter.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/DiagnosticReporterTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/diagnostic-reporter.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }
}
