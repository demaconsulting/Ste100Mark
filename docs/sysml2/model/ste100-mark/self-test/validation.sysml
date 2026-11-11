package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def Validation {
        doc /* Orchestrates the self-validation test suite and reports pass/fail results. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/SelfTest/Validation.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/SelfTest/ValidationTests.cs");
            designFiles = ("docs/design/ste100-mark/self-test/validation.md");
            verificationFiles = ("docs/verification/ste100-mark/self-test/validation.md");
            requirementFiles = ("docs/reqstream/ste100-mark/self-test/validation.yaml");
        }
    }
}
