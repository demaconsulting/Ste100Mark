package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def Context {
        doc /* Command-line argument parser and I/O owner for one tool invocation. */

        metadata info : SoftwareInfo {
            description = "Command-line argument parser and I/O owner for one tool invocation.";
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Cli/Context.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Cli/ContextTests.cs");
            designFiles = ("docs/design/ste100-mark/cli/context.md");
            verificationFiles = ("docs/verification/ste100-mark/cli/context.md");
            requirementFiles = ("docs/reqstream/ste100-mark/cli/context.yaml");
        }
    }
}
