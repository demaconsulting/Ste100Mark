package Ste100Mark_Validation {
    private import SoftwareItemMetadata::*;
    private import Ste100Mark_Context::*;
    private import Ste100Mark_PathHelpers::*;

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

    // Validation writes results through Context, builds log paths with PathHelpers, and
    // runs Program in each self-test. Program imports Validation, so Program is qualified.
    dependency Validation to Context;
    dependency Validation to PathHelpers;
    dependency Validation to Ste100Mark_Program::Program;
}
