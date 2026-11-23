package Ste100Mark_Diagnostic {
    private import SoftwareItemMetadata::*;
    private import Ste100Mark_Severity::*;

    part def Diagnostic {
        doc /* Immutable lint finding record carrying location, rule, severity, and message data. */

        // Verification: verified through subsystem-level scenarios
        // Requirements: documented as part of the linting subsystem
        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/Diagnostic.cs");
            designFiles = ("docs/design/ste100-mark/linting/diagnostic.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }

    // Diagnostic stores the effective severity of each finding.
    dependency Diagnostic to Severity;
}
