package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def Severity {
        doc /* Closed severity set shared by configuration, diagnostics, and exit-code logic. */

        // Verification: verified through subsystem-level scenarios
        // Requirements: documented as part of the linting subsystem
        metadata info : SoftwareInfo {
            description = "Closed severity set shared by configuration, diagnostics, and exit-code logic.";
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/Severity.cs");
            designFiles = ("docs/design/ste100-mark/linting/severity.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }
}
