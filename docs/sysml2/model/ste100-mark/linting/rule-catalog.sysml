package Ste100Mark {
    part def RuleCatalog {
        doc /* Static, hand-authored source of truth for every rule code the Linting subsystem can emit, exposed as machine-readable metadata (title, official/mechanical/advisory classification, suggestion kind, applicable mode(s)) and serialized as JSON for the --list-rules CLI dispatch path. */

        comment sourceRef /* Source: src/DemaConsulting.Ste100Mark/Linting/RuleCatalog.cs */
        comment testRef /* Test: test/DemaConsulting.Ste100Mark.Tests/Linting/RuleCatalogTests.cs */
        comment designRef /* Design: docs/design/ste100-mark/linting/rule-catalog.md */
        comment verificationRef /* Verification: docs/verification/ste100-mark/linting.md (verified through rule-catalog scenarios) */
        comment reqRef /* Requirements: docs/reqstream/ste100-mark/linting.yaml (rule catalog requirements) */
    }
}
