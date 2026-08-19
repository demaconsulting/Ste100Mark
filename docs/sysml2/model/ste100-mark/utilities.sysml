package Ste100Mark {
    part def UtilitiesSubsystem {
        doc /* Shared, independently testable helper functions used by other subsystems. */

        comment testRef /* Test: test/DemaConsulting.Ste100Mark.Tests/Utilities/UtilitiesSubsystemTests.cs */
        comment designRef /* Design: docs/design/ste100-mark/utilities.md */
        comment verificationRef /* Verification: docs/verification/ste100-mark/utilities.md */
        comment reqRef /* Requirements: docs/reqstream/ste100-mark/utilities.yaml */

        part pathHelpers : PathHelpers;
    }
}
