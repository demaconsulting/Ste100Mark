package Ste100Mark {
    private import SoftwareItemMetadata::*;

    part def YamlDotNet {
        doc /* YAML deserialization library used to parse lint configuration and dictionary files. */

        metadata info : SoftwareInfo {
            description = "YAML deserialization library used to parse lint configuration and dictionary files.";
            designFiles = ("docs/design/ots/yamldotnet.md");
            verificationFiles = ("docs/verification/ots/yamldotnet.md");
            requirementFiles = ("docs/reqstream/ots/yamldotnet.yaml");
        }
    }

    part def PartOfSpeech {
        doc /* Statistical part-of-speech tagger (DemaConsulting.PartOfSpeech) used to resolve the part of speech of dictionary term matches. */

        metadata info : SoftwareInfo {
            description = "Statistical part-of-speech tagger (DemaConsulting.PartOfSpeech) used to resolve the part of speech of dictionary term matches.";
            designFiles = ("docs/design/ots/part-of-speech.md");
            verificationFiles = ("docs/verification/ots/part-of-speech.md");
            requirementFiles = ("docs/reqstream/ots/part-of-speech.yaml");
        }
    }

    part def FileSystemGlobbing {
        doc /* Glob matching library used to resolve include/exclude and mode-override patterns. */

        metadata info : SoftwareInfo {
            description = "Glob matching library used to resolve include/exclude and mode-override patterns.";
            designFiles = ("docs/design/ots/file-system-globbing.md");
            verificationFiles = ("docs/verification/ots/file-system-globbing.md");
            requirementFiles = ("docs/reqstream/ots/file-system-globbing.yaml");
        }
    }
}
