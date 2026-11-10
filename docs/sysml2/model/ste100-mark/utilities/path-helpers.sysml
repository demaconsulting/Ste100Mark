package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def PathHelpers {
        doc /* Safe path-combination utility guarding against path-traversal. */

        metadata info : SoftwareInfo {
            description = "Safe path-combination utility guarding against path-traversal.";
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Utilities/PathHelpers.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Utilities/PathHelpersTests.cs");
            designFiles = ("docs/design/ste100-mark/utilities/path-helpers.md");
            verificationFiles = ("docs/verification/ste100-mark/utilities/path-helpers.md");
            requirementFiles = ("docs/reqstream/ste100-mark/utilities/path-helpers.yaml");
        }
    }
}
