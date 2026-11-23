package Ste100Mark_LintGlossary {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;
    private import Ste100Mark_LintConfig::*;
    private import Ste100Mark_LintDictionary::*;

    part def LintGlossary {
        doc /* Loads, merges, and validates the project glossary and deprecated terms, and builds the part-of-speech tagger glossary. */

        // Verification: verified through glossary loading, conflict, and path-resolution scenarios
        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Linting/LintGlossary.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/Linting/LintGlossaryTests.cs");
            designFiles = ("docs/design/ste100-mark/linting/lint-glossary.md");
            verificationFiles = ("docs/verification/ste100-mark/linting.md");
            requirementFiles = ("docs/reqstream/ste100-mark/linting/lint-glossary.yaml");
        }
    }

    // LintGlossary reads glossary settings from LintConfig, checks conflicts against the
    // effective LintDictionary, parses YAML, builds the tagger glossary, and resolves glob
    // patterns through Linter.ResolvePatterns. Linter imports LintGlossary, so Linter is qualified.
    dependency LintGlossary to LintConfig;
    dependency LintGlossary to LintDictionary;
    dependency LintGlossary to YamlDotNet;
    dependency LintGlossary to PartOfSpeech;
    dependency LintGlossary to Ste100Mark_Linter::Linter;
}
