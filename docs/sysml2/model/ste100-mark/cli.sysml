package Ste100Mark {
    part def CliSubsystem {
        doc /* Command-line argument parsing, output-channel management, and exit-code tracking. */

        comment testRef /* Test: test/DemaConsulting.Ste100Mark.Tests/Cli/CliSubsystemTests.cs */
        comment designRef /* Design: docs/design/ste100-mark/cli.md */
        comment verificationRef /* Verification: docs/verification/ste100-mark/cli.md */
        comment reqRef /* Requirements: docs/reqstream/ste100-mark/cli.yaml */

        part context : Context;
    }
}
