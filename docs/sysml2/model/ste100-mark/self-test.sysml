package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def SelfTestSubsystem {
        doc /* Self-validation framework exercising the tool's own capabilities on demand. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/SelfTest/SelfTestSubsystemTests.cs");
            designFiles = ("docs/design/ste100-mark/self-test.md");
            verificationFiles = ("docs/verification/ste100-mark/self-test.md");
            requirementFiles = ("docs/reqstream/ste100-mark/self-test.yaml");
        }

        part validation : Validation;
    }
}
