package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def UtilitiesSubsystem {
        doc /* Shared, independently testable helper functions used by other subsystems. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Utilities/UtilitiesSubsystemTests.cs");
            designFiles = ("docs/design/ste100-mark/utilities.md");
            verificationFiles = ("docs/verification/ste100-mark/utilities.md");
            requirementFiles = ("docs/reqstream/ste100-mark/utilities.yaml");
        }

        part pathHelpers : PathHelpers;
    }
}
