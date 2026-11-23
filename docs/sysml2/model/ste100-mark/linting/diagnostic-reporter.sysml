package Ste100Mark_DiagnosticReporter {
    private import SoftwareItemMetadata::*;
    private import Ste100Mark_Context::*;
    private import Ste100Mark_Diagnostic::*;
    private import Ste100Mark_FindingDetail::*;
    private import Ste100Mark_Severity::*;

    part def DiagnosticReporter {
        doc /* Formats aggregated lint diagnostics as text lines, a single JSON document, or a single detailed JSON document. */

        // Verification: verified through output-format scenarios
        // Requirements: output-format requirements
        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/DiagnosticReporter.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/DiagnosticReporterTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/diagnostic-reporter.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }

    // The reporter writes through Context and formats Diagnostic payloads, severity counts,
    // and the FindingDetail objects of the detailed format.
    dependency DiagnosticReporter to Context;
    dependency DiagnosticReporter to Diagnostic;
    dependency DiagnosticReporter to FindingDetail;
    dependency DiagnosticReporter to Severity;
}
