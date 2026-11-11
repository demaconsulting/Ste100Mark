package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def CliSubsystem {
        doc /* Command-line argument parsing, output-channel management, and exit-code tracking. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Cli/CliSubsystemTests.cs");
            designFiles = ("docs/design/ste100-mark/cli.md");
            verificationFiles = ("docs/verification/ste100-mark/cli.md");
            requirementFiles = ("docs/reqstream/ste100-mark/cli.yaml");
        }

        part context : Context;
    }
}
