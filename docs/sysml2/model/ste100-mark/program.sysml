package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def Program {
        doc /* Entry point and execution orchestrator. */

        metadata info : SoftwareInfo {
            description = "Entry point and execution orchestrator.";
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Program.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/ProgramTests.cs");
            designFiles = ("docs/design/ste100-mark/program.md");
            verificationFiles = ("docs/verification/ste100-mark/program.md");
            requirementFiles = ("docs/reqstream/ste100-mark/program.yaml");
        }
    }
}
