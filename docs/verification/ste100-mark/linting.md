## Linting

### Verification Approach

The `Linting` subsystem is verified by a combination of unit tests and end-to-end integration
 tests. The unit tests exercise `MarkdownProseExtractor`, `SentenceAnalyzer`, `StructuralRules`,
`LintConfig`, `LintDictionary`, `PartOfSpeechGuesser`, `DictionaryChecker`, `DiagnosticReporter`,
and `Linter` directly with controlled inputs. Integration tests invoke the published tool
assembly through `Runner.RunInDirectory`, confirming that command-line parsing, dispatch,
reporting, and exit-code behavior all work together.

### Test Environment

N/A - standard test environment.

### Acceptance Criteria

- All unit and integration tests pass with zero failures.
- Official lint rules emit error-severity diagnostics when enabled.
- Advisory heuristics emit the configured advisory severity and only fail the run under
  `--strict` or explicit error configuration.
- Configuration and dictionary files are parsed correctly and produce clear errors when invalid.
- Text and JSON output formats remain stable and machine-consumable.

### Test Scenarios

**Template-Linting-WordLimits**: Sentence splitting and Rule 4.1 counting are verified for
normal punctuation, colon termination, parentheticals, hyphenated words, number-plus-unit
spans, quoted text, title-style sequences, and procedure-versus-descriptive limits. This
scenario is tested by `Split_SingleSimpleSentence_ReturnsOneSentenceWithWordCount`,
`Split_MultipleSentences_ReturnsEachSentenceSeparately`,
`Split_ColonFollowedByCapital_TreatedAsSentenceTerminator`,
`CountWords_ParentheticalSpan_CountsAsOneWord`,
`Split_ParentheticalFormingCompleteSentence_ExtractedSeparately`,
`CountWords_HyphenatedWord_CountsAsOneWord`, `CountWords_NumberWithUnit_CountsAsOneWord`,
`CountWords_QuotedText_CountsAsOneWord`, `CountWords_TitleCaseSequence_CountsAsOneWord`,
`Evaluate_SentenceWithinDescriptiveLimit_NoWordLimitDiagnostic`,
`Evaluate_SentenceExceedingDescriptiveLimit_FlagsWordLimitDiagnostic`, and
`Evaluate_SentenceExceedingProcedureLimit_FlagsOnlyInProcedureMode`.

**Template-Linting-Semicolons**: Rule 8.1 enforcement is verified both in-process and through
the published CLI, including the configuration path that disables the rule. This scenario is
tested by `Evaluate_Semicolon_FlagsSemicolonDiagnostic`,
`Evaluate_SemicolonWithAllowSemicolons_NoDiagnostic`,
`Run_FileWithSemicolon_ProducesFailureExitCode`, and
`Ste100Mark_LintFileWithSemicolon_ReportsErrorAndReturnsNonZero`.

**Template-Linting-Contractions**: Rule 4.2 contraction detection is verified with the rule
enabled and disabled. This scenario is tested by `Evaluate_Contraction_FlagsContractionDiagnostic`
and `Evaluate_ContractionWithAllowContractions_NoDiagnostic`.

**Template-Linting-ParagraphAdvisory**: The advisory paragraph-length heuristic is verified for
warning output, disablement through `0`, and the exemption of heading segments. This scenario is
tested by `Evaluate_ParagraphExceedingSentenceCap_FlagsAdvisoryWarning`,
`Evaluate_ParagraphLengthDisabled_NoAdvisoryDiagnostic`, and
`Evaluate_HeadingSegment_ExemptFromParagraphLengthCheck`.

**Template-Linting-PassiveVoiceAdvisory**: The passive-voice heuristic is verified for warning,
off, and error-severity configurations, plus a case proving a simple (non-perfect-tense)
passive construction is still flagged after the complex-verb precedence amendment. This
scenario is tested by `Evaluate_PassiveVoicePattern_FlagsAdvisoryAtConfiguredSeverity`,
`Evaluate_PassiveVoiceOff_NoDiagnostic`, `Evaluate_PassiveVoiceError_FlagsAtErrorSeverity`, and
`Evaluate_WasOpened_StillFlagsPassiveVoice`.

**Template-Linting-ComplexVerbAdvisory**: The complex-verb (perfect/modal-perfect tense)
heuristic is verified for perfect-tense and modal-perfect-tense matches, off-configuration,
inline-code exclusion, and the precedence case proving "has been X" is reported only as a
complex-verb finding and not also as a passive-voice finding. This scenario is tested by
`Evaluate_PerfectTensePattern_FlagsComplexVerbAdvisory`,
`Evaluate_ModalPerfectTensePattern_FlagsComplexVerbAdvisory`,
`Evaluate_ComplexVerbOff_NoDiagnostic`, `Evaluate_ComplexVerbOnlyInsideInlineCode_NoDiagnostic`,
and `Evaluate_HasBeenOpened_FlagsComplexVerbOnlyNotPassiveVoice`.

**Template-Linting-IngFormAdvisory**: The "-ing" form heuristic is verified for a mid-sentence
match, exclusion of a match touching a sentence-ending period before or after the word,
off-configuration, and inline-code exclusion. This scenario is tested by
`Evaluate_IngWordMidSentence_FlagsIngFormAdvisory`,
`Evaluate_IngWordFollowedByPeriod_NotFlagged`, `Evaluate_IngWordPrecededByPeriod_NotFlagged`,
`Evaluate_IngFormOff_NoDiagnostic`, and `Evaluate_IngWordOnlyInsideInlineCode_NoDiagnostic`.

**Template-Linting-Dictionary**: Dictionary merge and lookup behavior is verified for the
embedded baseline (including a multi-sense term), explicit project dictionaries, inline
overrides (including full sense-list replacement), allow/ignore removal, case-insensitive
lookup, whole-term matching, and suggestions. This scenario is tested by
`Load_DefaultConfig_IncludesEmbeddedEntries`, `Load_MultiSenseEmbeddedTerm_IncludesBothSenses`,
`Load_UseEmbeddedFalse_ExcludesEmbeddedEntries`,
`Load_InlineDisallowEntry_AddedToMergedDictionary`,
`Load_InlineDisallowOverridesEmbeddedTerm_ReplacesAllSenses`,
`Load_AllowListedTerm_RemovedFromMergedDictionary`,
`Load_IgnoreListedTerm_RemovedFromMergedDictionary`,
`Load_ProjectDictionaryFile_MergedOverEmbedded`,
`Load_MissingProjectDictionaryFile_ThrowsInvalidOperationException`,
`TryGetEntry_DifferentCasing_MatchesEntry`,
`Evaluate_DisallowedEmbeddedTerm_FlagsDiagnosticWithSuggestion`,
`Evaluate_MultiWordPhrase_FlagsDiagnostic`, `Evaluate_DifferentCasing_StillFlagsDiagnostic`,
`Evaluate_TermEmbeddedInLongerWord_NotFlagged`, `Evaluate_NoDisallowedTerms_ReturnsNoDiagnostics`,
`Evaluate_InlineOverriddenTerm_UsesOverriddenSuggestion`, and
`Evaluate_AllowListedTerm_NotFlagged`.

**Template-Linting-InlineCodeSpans**: Inline code span handling is verified for verbatim
retention in extracted prose, continued full exclusion of fenced code blocks, one-word
counting toward Rule 4.1/8.4-8.7 limits, verbatim display in a word-limit diagnostic message,
and exclusion of inline-code-span content from the semicolon (Rule 8.1), contraction
(Rule 4.2), passive-voice, complex-verb, -ing form, and dictionary checks, including a case
where the same disallowed term appears both inside a code span and in surrounding prose in the
same segment. This scenario is tested by `Extract_InlineCodeSpan_KeptVerbatimInProse`,
`Extract_FencedCodeBlock_ExcludedFromProse`, `CountWords_InlineCodeSpan_CountsAsOneWord`,
`Split_SentenceWithInlineCodeSpan_KeepsVerbatimText`,
`Evaluate_SemicolonOnlyInsideInlineCode_NoDiagnostic`,
`Evaluate_ContractionOnlyInsideInlineCode_NoDiagnostic`,
`Evaluate_PassiveVoiceOnlyInsideInlineCode_NoDiagnostic`,
`Evaluate_ComplexVerbOnlyInsideInlineCode_NoDiagnostic`,
`Evaluate_IngWordOnlyInsideInlineCode_NoDiagnostic`,
`Evaluate_WordLimitDiagnosticMessage_ShowsInlineCodeVerbatim`,
`Evaluate_DisallowedTermOnlyInsideInlineCode_NotFlagged`, and
`Evaluate_DisallowedTermInsideAndOutsideInlineCode_FlagsOnlyProseOccurrence`.

**Template-Linting-DictionaryPos**: Part-of-speech sense selection is verified for every
heuristic signal in isolation (infinitive marker, modal auxiliary, progressive auxiliary,
verb-inflection suffix, imperative sentence start in both Procedure and Descriptive mode,
article, possessive, quantifier/demonstrative, preposition, plural-noun suffix, and
noun-phrase continuation), plus the conflicting-signal and no-signal ambiguous fallbacks, and
end-to-end through `DictionaryChecker` for single-sense terms, confident noun/verb contexts,
an ambiguous context, and a single-sense `pos: any` connector phrase. This scenario is tested
by `Guess_PrecededByTo_ReturnsVerb`, `Guess_PrecededByModal_ReturnsVerb`,
`Guess_PrecededByBeAuxiliaryWithIngSuffix_ReturnsVerb`, `Guess_InflectionSuffixAlone_ReturnsVerb`,
`Guess_ImperativeSentenceStartInProcedureMode_ReturnsVerb`,
`Guess_SentenceStartInDescriptiveMode_ReturnsNull`, `Guess_PrecededByArticle_ReturnsNoun`,
`Guess_PrecededByPossessive_ReturnsNoun`, `Guess_PrecededByQuantifier_ReturnsNoun`,
`Guess_PrecededByPreposition_ReturnsNoun`, `Guess_PluralSuffix_ReturnsNoun`,
`Guess_FollowedByOf_ReturnsNoun`, `Guess_ConflictingSignals_ReturnsNull`,
`Guess_NoSignals_ReturnsNull`, `Evaluate_SingleSenseTerm_AlwaysReportedRegardlessOfContext`,
`Evaluate_MultiSenseTerm_NounContext_ReportsNounSense`,
`Evaluate_MultiSenseTerm_VerbContext_ReportsVerbSense`,
`Evaluate_MultiSenseTerm_AmbiguousContext_ReportsAllSensesAmbiguous`, and
`Evaluate_AnyPosSingleSenseTerm_AlwaysReported`. Natural "or"/Oxford-comma phrasing of a
sense's alternatives within the diagnostic message, for both the confident and ambiguous
paths, is additionally verified by `Evaluate_ConfidentSenseSingleAlternative_NoOrInMessage`,
`Evaluate_ConfidentSenseTwoAlternatives_JoinsWithOrNoOxfordComma`,
`Evaluate_ConfidentSenseThreeOrMoreAlternatives_JoinsWithOxfordCommaBeforeOr`, and
`Evaluate_AmbiguousMultiSenseTerm_GroupsAlternativesPerSenseWithNaturalJoin`.

**Template-Linting-Configuration**: YAML configuration loading and override resolution are
verified for defaults, malformed files, missing files, complete schemas, and first-match-wins
mode overrides. This scenario is tested by `Load_NullPath_ReturnsDefaultConfiguration`,
`Load_NonExistentPath_ThrowsInvalidOperationException`,
`Load_FullConfigurationFile_ParsesAllSections`, `Load_MalformedYaml_ThrowsInvalidOperationException`,
`ResolveMode_NoMatchingOverride_ReturnsDefaultMode`,
`ResolveMode_MatchingOverrideGlob_ReturnsOverriddenMode`,
`ResolveMode_MultipleOverrides_UsesFirstMatch`, and
`Run_ProcedureModeOverride_AppliesStricterWordLimit`.

**Template-Linting-CliIntegration**: The lint-specific CLI surface is verified for positional
globs, `--config`, `--format`, `--strict`, and default dispatch through `Program.Run`. This
scenario is tested by `Context_Create_NoArguments_ReturnsLintingDefaults`,
`Context_Create_PositionalArgument_CollectedAsGlob`,
`Context_Create_MultiplePositionalArguments_CollectedInOrder`,
`Context_Create_ConfigFlag_SetsConfigFile`,
`Context_Create_ConfigFlag_WithoutValue_ThrowsArgumentException`,
`Context_Create_FormatFlagJson_SetsJsonFormat`, `Context_Create_FormatFlagText_SetsTextFormat`,
`Context_Create_FormatFlag_UnsupportedValue_ThrowsArgumentException`,
`Context_Create_FormatFlag_WithoutValue_ThrowsArgumentException`,
`Context_Create_StrictFlag_SetsStrictTrue`, `Program_Run_NoArguments_DisplaysDefaultBehavior`,
and `Run_PositionalGlobs_OverrideConfigInclude`.

**Template-Linting-OutputFormats**: Text and JSON reporting are verified at the formatter level
and through the published CLI JSON path. This scenario is tested by
`Report_TextFormat_WritesDiagnosticLinesAndSummary`,
`Report_JsonFormat_WritesSingleJsonDocumentWithExpectedSchema`,
`Report_NoDiagnostics_WritesZeroCountSummary`, and
`Ste100Mark_LintWithJsonFormat_ProducesSingleValidJsonDocument`.

**Template-Linting-ExitCode**: Exit-code behavior is verified for clean files, build-breaking
errors, strict-mode warning promotion, configuration failures, and JSON-mode failure signaling.
This scenario is tested by `Run_CleanMarkdownFile_ProducesSuccessExitCode`,
`Run_FileWithSemicolon_ProducesFailureExitCode`,
`Run_WarnOnlyFinding_WithoutStrict_ProducesSuccessExitCode`,
`Run_WarnOnlyFinding_WithStrict_ProducesFailureExitCode`,
`Run_MissingExplicitConfigFile_ReportsErrorWithoutThrowing`,
`Context_MarkFailure_SetsExitCodeWithoutConsoleOutput`,
`Ste100Mark_LintCleanFile_ReturnsZeroExitCode`,
`Ste100Mark_LintWithStrictFlag_PromotesWarningsToFailure`, and
`Ste100Mark_LintWithMissingConfigFile_ReturnsNonZeroWithErrorMessage`.
