package Ste100Mark {
    part def PartOfSpeechGuesser {
        doc /* Deterministic regex/rule-based heuristic deciding whether an -ing word is a verb or a gerund/adjective. */

        comment sourceRef /* Source: src/DemaConsulting.Ste100Mark/Linting/PartOfSpeechGuesser.cs */
        comment testRef /* Test: test/DemaConsulting.Ste100Mark.Tests/Linting/StructuralRulesTests.cs (verified through -ing form scenarios) */
        comment designRef /* Design: docs/design/ste100-mark/linting/part-of-speech-guesser.md */
        comment verificationRef /* Verification: docs/verification/ste100-mark/linting.md (verified through -ing form scenarios) */
        comment reqRef /* Requirements: docs/reqstream/ste100-mark/linting.yaml (-ing form requirements) */
    }
}
