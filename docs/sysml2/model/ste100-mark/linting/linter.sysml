package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def Linter {
        doc /* End-to-end lint orchestrator and the subsystem's single externally called entry point. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/Linter.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/LinterTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/linter.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }
}
