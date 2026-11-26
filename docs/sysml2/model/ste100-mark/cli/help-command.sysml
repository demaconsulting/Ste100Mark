package Ste100Mark_HelpCommand {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;

    part def HelpCommand {
        doc /* Built-in help command: answers the help verb from the help cards embedded in the assembly. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Cli/HelpCommand.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Cli/HelpCommandTests.cs");
            designFiles = ("docs/design/ste100-mark/cli/help-command.md");
            verificationFiles = ("docs/verification/ste100-mark/cli/help-command.md");
            requirementFiles = ("docs/reqstream/ste100-mark/cli/help-command.yaml");
        }
    }

    // Serves the help command from the cards embedded in the assembly
    dependency HelpCommand to HelpCards;
}
