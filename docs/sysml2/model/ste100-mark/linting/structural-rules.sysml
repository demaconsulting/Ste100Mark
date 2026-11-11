package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def StructuralRules {
        doc /* Evaluates official structural rules and advisory prose heuristics over extracted segments. */

        // Verification: verified through structural-rule scenarios
        // Requirements: mechanical and advisory rule requirements
        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/StructuralRules.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/StructuralRulesTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/structural-rules.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }
}
