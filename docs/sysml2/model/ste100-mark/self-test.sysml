package Ste100Mark {
    part def SelfTestSubsystem {
        doc /* Self-validation framework exercising the tool's own capabilities on demand. */

        comment testRef /* Test: test/DemaConsulting.Ste100Mark.Tests/SelfTest/SelfTestSubsystemTests.cs */
        comment designRef /* Design: docs/design/ste100-mark/self-test.md */
        comment verificationRef /* Verification: docs/verification/ste100-mark/self-test.md */
        comment reqRef /* Requirements: docs/reqstream/ste100-mark/self-test.yaml */

        part validation : Validation;
    }
}
