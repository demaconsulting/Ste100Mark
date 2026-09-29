package Ste100Mark {
    part def RuleCodes {
        doc /* Single, shared registry of rule-code string constants referenced by StructuralRules, DictionaryChecker, and RuleCatalog, eliminating duplicated rule-code literals across the emitters and the published catalog. */

        comment sourceRef /* Source: src/DemaConsulting.Ste100Mark/Linting/RuleCodes.cs */
        comment designRef /* Design: docs/design/ste100-mark/linting/rule-codes.md */
        comment verificationRef /* Verification: docs/verification/ste100-mark/linting.md (verified through rule-catalog completeness scenarios) */
        comment reqRef /* Requirements: docs/reqstream/ste100-mark/linting.yaml (documented as part of the linting subsystem) */
    }
}
