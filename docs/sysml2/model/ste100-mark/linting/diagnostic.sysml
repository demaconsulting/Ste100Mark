package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def Diagnostic {
        doc /* Immutable lint finding record carrying location, rule, severity, and message data. */

        // Verification: verified through subsystem-level scenarios
        // Requirements: documented as part of the linting subsystem
        metadata info : SoftwareInfo {
            description = "Immutable lint finding record carrying location, rule, severity, and message data.";
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/Diagnostic.cs");
            designFiles = ("docs/design/ste100-mark/linting/diagnostic.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }
}
