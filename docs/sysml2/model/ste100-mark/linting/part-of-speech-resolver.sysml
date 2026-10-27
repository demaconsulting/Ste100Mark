package Ste100Mark {
    part def PartOfSpeechResolver {
        doc /* Resolves noun/verb usage of a dictionary term match by classifying its sentence with the selected WordSense engine (basic, minilm, or bert); owns and disposes the model. */

        comment sourceRef /* Source: src/DemaConsulting.Ste100Mark/Linting/PartOfSpeechResolver.cs (and IPartOfSpeechResolver.cs, Cli/PartOfSpeechEngine.cs) */
        comment testRef /* Test: test/DemaConsulting.Ste100Mark.Tests/Linting/PartOfSpeechResolverTests.cs */
        comment designRef /* Design: docs/design/ste100-mark/linting/part-of-speech-resolver.md */
        comment verificationRef /* Verification: docs/verification/ste100-mark/linting.md (verified through part-of-speech resolution scenarios) */
        comment reqRef /* Requirements: docs/reqstream/ste100-mark/linting/part-of-speech-resolver.yaml */
    }
}
