package Ste100Mark {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * Ste100Mark is a .NET command-line tool that lints Markdown prose against
     * ASD-STE100-style Simplified Technical English rules.
     */

    part def Ste100MarkSystem {
        doc /* The Ste100Mark system as a whole. */

        metadata info : SoftwareInfo {
            description = "The Ste100Mark system as a whole.";
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/IntegrationTests.cs");
            designFiles = ("docs/design/ste100-mark.md");
            verificationFiles = ("docs/verification/ste100-mark.md");
            requirementFiles = ("docs/reqstream/ste100-mark.yaml");
        }

        part program : Program;
        part cli : CliSubsystem;
        part linting : LintingSubsystem;
        part selfTest : SelfTestSubsystem;
        part utilities : UtilitiesSubsystem;
        part yamlDotNet : YamlDotNet;
        part partOfSpeech : PartOfSpeech;
        part fileSystemGlobbing : FileSystemGlobbing;
    }
}
