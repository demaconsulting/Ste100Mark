package Ste100Mark {
    part def YamlDotNet {
        doc /* YAML deserialization library used to parse lint configuration and dictionary files. */

        comment designRef /* Design: docs/design/ots/yamldotnet.md */
        comment verificationRef /* Verification: docs/verification/ots/yamldotnet.md */
        comment reqRef /* Requirements: docs/reqstream/ots/yamldotnet.yaml */
    }

    part def FileSystemGlobbing {
        doc /* Glob matching library used to resolve include/exclude and mode-override patterns. */

        comment designRef /* Design: docs/design/ots/file-system-globbing.md */
        comment verificationRef /* Verification: docs/verification/ots/file-system-globbing.md */
        comment reqRef /* Requirements: docs/reqstream/ots/file-system-globbing.yaml */
    }
}
