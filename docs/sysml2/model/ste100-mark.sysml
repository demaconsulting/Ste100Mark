package Ste100Mark {
    doc
    /*
     * Ste100Mark is a template .NET command-line application demonstrating
     * DemaConsulting's compliance/documentation tooling conventions.
     */

    part def Ste100MarkSystem {
        doc /* The Ste100Mark system as a whole. */

        comment testRef /* Test: test/DemaConsulting.Ste100Mark.Tests/IntegrationTests.cs */
        comment designRef /* Design: docs/design/ste100-mark.md */
        comment verificationRef /* Verification: docs/verification/ste100-mark.md */
        comment reqRef /* Requirements: docs/reqstream/ste100-mark.yaml */

        part program : Program;
        part cli : CliSubsystem;
        part linting : LintingSubsystem;
        part selfTest : SelfTestSubsystem;
        part utilities : UtilitiesSubsystem;
        part yamlDotNet : YamlDotNet;
        part fileSystemGlobbing : FileSystemGlobbing;
    }
}
