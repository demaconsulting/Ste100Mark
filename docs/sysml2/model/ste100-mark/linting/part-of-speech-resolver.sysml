package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def PartOfSpeechResolver {
        doc /* Resolves noun/verb/adjective/adverb usage and preserves confident determiner evidence for a dictionary term match by classifying its sentence with the PartOfSpeech tagger. */

        // Verification: verified through part-of-speech resolution scenarios
        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/PartOfSpeechResolver.cs", "src/DemaConsulting.Ste100Mark/Linting/IPartOfSpeechResolver.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/PartOfSpeechResolverTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/part-of-speech-resolver.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting/part-of-speech-resolver.yaml");
        }
    }
}
