package Ste100Mark_LintDictionary {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;
    private import Ste100Mark_LintConfig::*;

    part def LintDictionary {
        doc /* Effective merged dictionary built from embedded, project, and inline vocabulary sources. */

        // Verification: verified through dictionary scenarios
        // Requirements: dictionary requirements
        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/LintDictionary.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/LintDictionaryTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/lint-dictionary.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }

    // LintDictionary merges the dictionary configuration from LintConfig with the
    // YAML dictionary files it deserializes.
    dependency LintDictionary to LintConfig;
    dependency LintDictionary to YamlDotNet;
}
