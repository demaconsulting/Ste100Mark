package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def RuleCodes {
        doc /* Single, shared registry of rule-code string constants referenced by StructuralRules, DictionaryChecker, and RuleCatalog, eliminating duplicated rule-code literals across the emitters and the published catalog. */

        // Verification: verified through rule-catalog completeness scenarios
        // Requirements: documented as part of the linting subsystem
        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/RuleCodes.cs");
            designFiles = ("docs/design/ste100-mark/linting/rule-codes.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }
}
