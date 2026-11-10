package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def RuleCatalog {
        doc /* Static, hand-authored source of truth for every rule code the Linting subsystem can emit, exposed as machine-readable metadata (title, official/mechanical/advisory classification, suggestion kind, applicable mode(s)) and serialized as JSON for the --list-rules CLI dispatch path. */

        // Verification: verified through rule-catalog scenarios
        // Requirements: rule catalog requirements
        metadata info : SoftwareInfo {
            description = "Static, hand-authored source of truth for every rule code the Linting subsystem can emit, exposed as machine-readable metadata (title, official/mechanical/advisory classification, suggestion kind, applicable mode(s)) and serialized as JSON for the --list-rules CLI dispatch path.";
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/RuleCatalog.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/RuleCatalogTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/rule-catalog.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting.yaml");
        }
    }
}
