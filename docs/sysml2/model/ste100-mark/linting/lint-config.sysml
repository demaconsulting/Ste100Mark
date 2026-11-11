package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def LintConfig {
        doc /* YAML-backed lint configuration model; resolves per-file mode, rules, and dictionary allowances via profiles. */

        // Verification: verified through configuration scenarios
        // Requirements: configuration, profiles, and CLI integration requirements
        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/LintConfig.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/LintConfigTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/lint-config.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }
}
