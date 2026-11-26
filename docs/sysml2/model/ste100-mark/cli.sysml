package Ste100Mark_CliSubsystem {
    public import Ste100Mark_Context::*;
    public import Ste100Mark_HelpCommand::*;
    private import SoftwareItemMetadata::*;

    part def CliSubsystem {
        doc /* Command-line argument parsing, output-channel management, exit-code tracking, and the built-in help command. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Cli/CliSubsystemTests.cs");
            designFiles = ("docs/design/ste100-mark/cli.md");
            verificationFiles = ("docs/verification/ste100-mark/cli.md");
            requirementFiles = ("docs/reqstream/ste100-mark/cli.yaml");
        }

        part context : Context;
        part helpCommand : HelpCommand;
    }
}
