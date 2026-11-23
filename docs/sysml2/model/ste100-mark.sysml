package Ste100Mark {
    public import Ste100Mark_Program::*;
    public import Ste100Mark_CliSubsystem::*;
    public import Ste100Mark_LintingSubsystem::*;
    public import Ste100Mark_SelfTestSubsystem::*;
    public import Ste100Mark_UtilitiesSubsystem::*;
    private import OtsDependencies::*;
    private import SoftwareItemMetadata::*;

    doc
    /*
     * Ste100Mark is a .NET command-line tool that lints Markdown prose against
     * ASD-STE100-style Simplified Technical English rules.
     */

    part def Ste100MarkSystem {
        doc /* The Ste100Mark system as a whole. */

        metadata info : SoftwareInfo {
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
