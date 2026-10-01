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
- The rule catalog contains every emitted rule code with correct official/mechanical/advisory
  and suggestion-kind classification.

### Test Scenarios

**Ste100Mark-Linting-WordLimits**: Sentence splitting and Rule 4.1 counting are verified for
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

**Ste100Mark-Linting-Semicolons**: Rule 8.1 enforcement is verified both in-process and through
the published CLI, including the configuration path that disables the rule. This scenario is
tested by `Evaluate_Semicolon_FlagsSemicolonDiagnostic`,
`Evaluate_SemicolonWithAllowSemicolons_NoDiagnostic`,
`Run_FileWithSemicolon_ProducesFailureExitCode`, and
`Ste100Mark_LintFileWithSemicolon_ReportsErrorAndReturnsNonZero`.

**Ste100Mark-Linting-Contractions**: Rule 4.2 contraction detection is verified with the rule
enabled and disabled. This scenario is tested by `Evaluate_Contraction_FlagsContractionDiagnostic`
and `Evaluate_ContractionWithAllowContractions_NoDiagnostic`.

**Ste100Mark-Linting-ParagraphAdvisory**: The advisory paragraph-length heuristic is verified for
warning output, disablement through `0`, and the exemption of heading segments. This scenario is
tested by `Evaluate_ParagraphExceedingSentenceCap_FlagsAdvisoryWarning`,
`Evaluate_ParagraphLengthDisabled_NoAdvisoryDiagnostic`, and
`Evaluate_HeadingSegment_ExemptFromParagraphLengthCheck`.

**Ste100Mark-Linting-PassiveVoiceAdvisory**: The passive-voice heuristic is verified for warning,
off, and error-severity configurations, plus a case proving a simple (non-perfect-tense)
passive construction is still flagged after the complex-verb precedence amendment. This
scenario is tested by `Evaluate_PassiveVoicePattern_FlagsAdvisoryAtConfiguredSeverity`,
`Evaluate_PassiveVoiceOff_NoDiagnostic`, `Evaluate_PassiveVoiceError_FlagsAtErrorSeverity`, and
`Evaluate_WasOpened_StillFlagsPassiveVoice`. The imperative-subordinate-clause refinement -
exempting an "is"/"are" + common stative/adjectival participle match from passive-voice
reporting when the participle is one of a curated closed-class set (for example "energized",
"closed", "seated", "unobstructed") or the sentence is led by a common imperative verb (for
example "Keep", "Confirm", "Stop"), unless an explicit "by \<agent\>" phrase immediately
follows the participle, which always restores the genuine-passive finding regardless of either
exemption - is verified by `Evaluate_PassiveVoiceStativeParticiple_NotFlagged`,
`Evaluate_PassiveVoiceImperativeMainClauseComplement_NotFlagged`,
`Evaluate_PassiveVoiceIsUnobstructed_NeverFlagged` (confirming a pure adjective with no
past-participle form is never matched at all),
`Evaluate_PassiveVoiceImperativeWithExplicitAgent_StillFlagged`, and
`Evaluate_PassiveVoiceDeclarativeSentence_StillFlagged` (a plain declarative "is/are +
participle" sentence with no imperative lead and no stative participle is still flagged, as a
recall-regression guard).

**Ste100Mark-Linting-ComplexVerbAdvisory**: The complex-verb (perfect/modal-perfect tense)
heuristic is verified for perfect-tense and modal-perfect-tense matches, off-configuration,
inline-code exclusion, and the precedence case proving "has been X" is reported only as a
complex-verb finding and not also as a passive-voice finding. This scenario is tested by
`Evaluate_PerfectTensePattern_FlagsComplexVerbAdvisory`,
`Evaluate_ModalPerfectTensePattern_FlagsComplexVerbAdvisory`,
`Evaluate_ComplexVerbOff_NoDiagnostic`, `Evaluate_ComplexVerbOnlyInsideInlineCode_NoDiagnostic`,
and `Evaluate_HasBeenOpened_FlagsComplexVerbOnlyNotPassiveVoice`.

**Ste100Mark-Linting-IngFormAdvisory**: The "-ing" form heuristic is verified for a mid-sentence
genuine verb match, off-configuration, and inline-code exclusion. This scenario is tested by
`Evaluate_IngWordMidSentence_FlagsIngFormAdvisory`, `Evaluate_IngFormOff_NoDiagnostic`, and
`Evaluate_IngWordOnlyInsideInlineCode_NoDiagnostic`. The heuristic's delegation to
`PartOfSpeechGuesser.GuessIngFormRole` - so a gerund/process noun (sentence-initial subject,
object of a preposition, a gerund complement directly after a catenative verb such as
"continue"), a material noun ending in "-ing", a participial adjective directly before a noun,
and an "-ing" word inside a `Heading`/`TableHeader` segment, an admonition label, or a
quoted/cited title are never flagged, while a genuine present-participle verb use remains
flagged - is verified by `Evaluate_IngWordFollowedByPeriod_NotFlagged` (gerund complement after
a catenative verb), `Evaluate_IngWordPrecededByPeriod_NotFlagged` (sentence-initial subject
gerund immediately after a preceding sentence's period),
`Evaluate_IngWordAsSentenceInitialSubject_NotFlagged`,
`Evaluate_IngWordAsObjectOfPreposition_NotFlagged`, `Evaluate_IngWordAsMaterialNoun_NotFlagged`,
`Evaluate_IngWordAsParticipialAdjectiveBeforeNoun_NotFlagged`,
`Evaluate_IngWordInHeading_NotFlagged`, `Evaluate_IngWordInTableHeader_NotFlagged`,
`Evaluate_IngWordInQuotedCitedTitle_NotFlagged`, and
`Evaluate_IngWordInsideAdmonitionLabel_NotFlagged`.

**Ste100Mark-Linting-Dictionary**: Dictionary merge and lookup behavior is verified for the
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

**Ste100Mark-Linting-DictionaryDisable**: The `dictionary.enabled: false` switch is verified
end-to-end to suppress every `STE100-DICT` finding while leaving structural/mechanical checks
(the semicolon rule) unaffected, and the default (`enabled` omitted) is verified to still run
the dictionary check. This scenario is tested by
`Run_DictionaryDisabled_SuppressesDictionaryFindingsButKeepsStructuralChecks` and
`Run_NoDictionaryConfigSection_DictionaryCheckRunsByDefault`.

**Ste100Mark-Linting-InlineCodeSpans**: Inline code span handling is verified for verbatim
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

**Ste100Mark-Linting-FrontMatter**: A leading YAML front matter block, delimited by `---` as
the document's literal first line and closed by a later `---` or `...`, is verified to be
excluded entirely from prose extraction (so metadata such as names, titles, and keywords is
never checked as prose), while a document line after the front matter reports its true source
line number. A `---` first line with no closing delimiter, and a `---` line appearing anywhere
other than the document's first line, are both verified to fall back to ordinary paragraph
content rather than being misidentified as front matter or silently discarded. This scenario
is tested by `Extract_LeadingFrontMatter_ExcludedFromProse`,
`Extract_LeadingFrontMatterClosedWithEllipsis_ExcludedFromProse`,
`Extract_LeadingFrontMatter_SubsequentSegmentReportsTrueLineNumber`,
`Extract_LeadingHyphenLineWithoutClosingDelimiter_NotTreatedAsFrontMatter`,
`Extract_HyphenLineNotOnFirstLine_NotTreatedAsFrontMatterStart`, and
`Run_FrontMatter_ExcludedFromDictionaryCheck`.

**Ste100Mark-Linting-DictionaryPos**: Part-of-speech sense selection is verified for every
heuristic signal in isolation (infinitive marker, modal auxiliary, progressive auxiliary,
verb-inflection suffix, imperative sentence start in both Procedure and Descriptive mode,
article, possessive, quantifier/demonstrative, preposition, plural-noun suffix, noun-phrase
continuation, a determiner governing through a participle/adjective/proper-noun modifier
bounded by a maximum scan distance, and a match followed by a finite verb form), plus the
conflicting-signal and no-signal ambiguous fallbacks, and end-to-end through
`DictionaryChecker` for single-sense terms, confident noun/verb contexts, an ambiguous
context, and a single-sense `pos: any` connector phrase. This scenario is tested
by `Guess_PrecededByTo_ReturnsVerb`, `Guess_PrecededByModal_ReturnsVerb`,
`Guess_PrecededByBeAuxiliaryWithIngSuffix_ReturnsVerb`, `Guess_InflectionSuffixAlone_ReturnsVerb`,
`Guess_ImperativeSentenceStartInProcedureMode_ReturnsVerb`,
`Guess_SentenceStartInDescriptiveMode_ReturnsNull`, `Guess_PrecededByArticle_ReturnsNoun`,
`Guess_PrecededByPossessive_ReturnsNoun`, `Guess_PrecededByQuantifier_ReturnsNoun`,
`Guess_PrecededByPreposition_ReturnsNoun`, `Guess_PluralSuffix_ReturnsNoun`,
`Guess_FollowedByOf_ReturnsNoun`, `Guess_ConflictingSignals_ReturnsNull`,
`Guess_NoSignals_ReturnsNull`, `Guess_ArticleThenParticipleModifier_ReturnsNoun`,
`Guess_ArticleThenProperNounModifier_ReturnsNoun`,
`Guess_ArticleThenAdjectiveModifier_ReturnsNoun`,
`Guess_DeterminerBeyondMaxScanDistance_DoesNotGovern`,
`Guess_FollowedByFiniteVerb_ReturnsNoun`,
`Evaluate_SingleSenseTerm_InconclusiveContext_ReportedWithoutPosLabel`,
`Evaluate_SingleSenseVerbOnlyTerm_ConfidentNounContext_NotFlagged`,
`Evaluate_MultiSenseTerm_NounContext_ReportsNounSense`,
`Evaluate_MultiSenseTerm_VerbContext_ReportsVerbSense`,
`Evaluate_MultiSenseTerm_AmbiguousContext_ReportsAllSensesAmbiguous`,
`Evaluate_MultiSenseTerm_ConfidentGuessMatchesNoSense_NotFlagged`, and
`Evaluate_AnyPosSingleSenseTerm_AlwaysReported`. Natural "or"/Oxford-comma phrasing of a
sense's alternatives within the diagnostic message, for both the confident and ambiguous
paths, is additionally verified by `Evaluate_ConfidentSenseSingleAlternative_NoOrInMessage`,
`Evaluate_ConfidentSenseTwoAlternatives_JoinsWithOrNoOxfordComma`,
`Evaluate_ConfidentSenseThreeOrMoreAlternatives_JoinsWithOxfordCommaBeforeOr`, and
`Evaluate_AmbiguousMultiSenseTerm_GroupsAlternativesPerSenseWithNaturalJoin`. An ambiguous
candidate sense with no alternatives at all (a pure role restriction, disallowed with no
suggested replacement word) is excluded from the combined suggestion string rather than
contributing a stray leading-space/bare-part-of-speech fragment (for example " (adjective)"),
verified by `Evaluate_AmbiguousTerm_CandidateWithNoAlternatives_SuggestionHasNoEmptyFragment`.
Structured `Citations` - one `(Term, Pos)` entry per real alternative, additive alongside
`Suggestion` - are verified for the single-sense, confident multi-sense, and ambiguous
multi-sense paths, confirmed absent (`null`) when a purely self-referential sense is the sole
candidate or when a confidently-resolved sense has no alternatives at all, and confirmed to
contain only the surviving candidates' entries (omitting an alternatives-less candidate rather
than a stray/empty entry) when a diagnostic has a mix of citable and non-citable candidates, by
the `Citations` assertions within
`Evaluate_SingleSenseTerm_InconclusiveContext_ReportedWithoutPosLabel`,
`Evaluate_MultiSenseTerm_NounContext_ReportsNounSense`,
`Evaluate_MultiSenseTerm_VerbContext_ReportsVerbSense`,
`Evaluate_MultiSenseTerm_AmbiguousContext_ReportsAllSensesAmbiguous`,
`Evaluate_PureSelfReferentialEntry_ConfidentDisallowedUsage_UsesRoleRestrictionMessage` (`null`,
sole candidate is self-referential),
`Evaluate_ConfidentSingleSenseTermWithNoAlternatives_SuggestionAndCitationsAreNull` (`null`, sole
candidate is confidently resolved but has no alternatives to cite),
`Evaluate_PureSelfReferentialCandidate_WithinAmbiguousResult_UsesRoleRestrictionClause`
(non-`null`, containing only the genuinely actionable candidate's entries, omitting the
self-referential candidate), and
`Evaluate_AmbiguousTerm_CandidateWithNoAlternatives_SuggestionHasNoEmptyFragment` (non-`null`,
containing only the candidate with real alternatives, omitting the alternatives-less candidate).
Per-file dictionary allowances supplied via a matching `Profile`'s `dictionary.allow`/
`dictionary.ignore` are verified by `Evaluate_TermInExtraAllowedTerms_NotFlagged`,
`Evaluate_ExtraAllowedTermsDifferentCasing_StillSuppressesDiagnostic`, and
`Evaluate_ExtraAllowedTermsUnrelatedTerm_StillFlagsOtherDisallowedTerm`.

**Ste100Mark-Linting-DictionaryPosVerblessSegment**: The whole-segment "no finite verb anywhere"
noun signal is verified in isolation for a verbless table cell, a verbless list-item fragment, a
verbless comma-separated list fragment, and a verbless heading fragment, plus its correct
interaction with stronger and weaker verb signals: it does not out-vote a strong match-local verb
signal (preceded by the infinitive marker "to"), it correctly leaves the mode-dependent imperative
signal to resolve confidently rather than being silently overridden, and it does not fire when a
recognized finite verb form is present elsewhere in the segment. This scenario is tested by
`Guess_VerblessTableCellFragment_ReturnsNoun`, `Guess_VerblessListItemFragment_ReturnsNoun`,
`Guess_VerblessCommaSeparatedList_ReturnsNoun`, `Guess_VerblessHeadingFragment_ReturnsNoun`,
`Guess_VerblessSegmentButPrecededByTo_ReturnsVerb`,
`Guess_ImperativeInVerblessSegment_ReturnsVerb`, and
`Guess_SegmentHasFiniteVerbElsewhere_MatchStillAmbiguous`. End-to-end behavior through
`DictionaryChecker`, using a fixture dictionary matching the reported ASD-STE100 corpus shape
(verb-only entries for "wash"/"pump"/"probe"/"function", and noun-sense entries for
"arrangement"/"state"), is verified by `Evaluate_VerblessTableCellNounPhrase_NotFlagged`,
`Evaluate_VerblessTableHeaderCell_NotFlagged`, `Evaluate_VerblessListItemNounPhrase_NotFlagged`,
`Evaluate_VerblessCommaSeparatedListFragment_NotFlagged`,
`Evaluate_VerblessHeadingFragment_NotFlagged`,
`Evaluate_VerblessCellWithNounSenseTerm_StillFlagsNounSenseTerm` and
`Evaluate_VerblessCellWithStateNounSense_StillFlagsFinding` (confirming that a noun-sense entry
is still reported inside the same verbless cell that suppresses adjacent verb-only entries),
`Evaluate_ImperativeVerblessSentenceInProcedureMode_StillFlagsVerbUsage` (confirming an imperative
instruction with no other finite verb is still flagged as a verb usage), and
`Evaluate_SubjectNounWithDeterminerAndFollowingVerb_NotFlagged` (a determiner/finite-verb-follows
regression guard).

**Ste100Mark-Linting-AccurateLineNumbers**: Diagnostics on a multi-line paragraph are verified to
report the actual violating source line rather than the paragraph's first line, for a paragraph
extracted end-to-end (not the line-1-pinned test helper) with the violation placed on a later
line, across the word-limit, semicolon, contraction, -ing form, and dictionary checks, plus the
underlying `ProseSegment.ResolveLine` offset-to-line resolution for both a multi-line paragraph
and a single-line segment. This scenario is tested by
`Extract_MultiLineParagraph_ResolveLineReportsEachLinesOwnLineNumber`,
`Extract_SingleLineSegment_ResolveLineAlwaysReturnsSegmentLine`,
`Evaluate_LongSentenceOnLaterLineOfMultiLineParagraph_ReportsThatLine`,
`Evaluate_SemicolonOnLaterLineOfMultiLineParagraph_ReportsThatLine`,
`Evaluate_ContractionOnLaterLineOfMultiLineParagraph_ReportsThatLine`,
`Evaluate_IngFormOnLaterLineOfMultiLineParagraph_ReportsThatLine`, and
`Evaluate_DisallowedTermOnLaterLineOfMultiLineParagraph_ReportsThatLine`.

**Ste100Mark-Linting-WrappedListItemPhraseMatching**: A list item's wrapped continuation
lines (lazy-continuation lines with no list marker of their own) are verified to merge into
the same `ListItem` segment as the item they continue, with `ResolveLine` correctly
attributing each half to its true source line, while a genuinely separate list item, a
nested (deeper-indented) list item, and an empty marker line's role/line-offset handling are
all verified not to be affected by the merge. An end-to-end `DictionaryChecker` case confirms
an allow-in-phrase entry split across such a wrap is now suppressed. This scenario is tested
by `Extract_ListItemWrappedAcrossLines_MergesIntoSingleListItemSegment`,
`Extract_TwoListItemsFirstWrapped_RemainSeparateSegments`,
`Extract_ListItemWithEmptyMarkerLineThenContinuation_KeepsListItemRole`,
`Extract_EmptyListMarkerThenSeparateItem_DoesNotLeakStaleLineOffset`,
`Extract_NestedListItem_DoesNotMergeIntoParentListItem`, and
`Evaluate_AllowedPhraseSplitAcrossListItemLineWrap_SuppressesDiagnostic`.

**Extended POS Signal Coverage (supplementary regression scenario, no linked requirement)**:
Additional part-of-speech heuristic signals are
verified in isolation: a match immediately followed by a bare numeral/identifier (for example
"Block 0") resolves as a noun, while a match followed by a decimal-qualified number (a measured
value) still resolves as a verb; a match immediately followed by "not"/"never" resolves as a
noun, except when the matched word is itself a modal, "to be", or "do"/"have"-family auxiliary
verb (for example "shall not"/"does not"), which always remains verbal regardless of the
following negation; and a noun modifier directly before an allow-listed plural compound-noun
head (for example "cycles") resolves as a noun. This scenario is tested by
`Guess_FollowedByBareIdentifierNumber_ReturnsNoun`, `Guess_FollowedByDecimalNumber_StillReturnsVerb`,
`Guess_FollowedByNegationNot_ReturnsNoun`,
`Guess_ModalAuxiliaryFollowedByNegation_DoesNotReturnNoun`, and
`Guess_DoAuxiliaryFollowedByNegation_DoesNotReturnNoun`,
`Guess_NounModifierBeforeAllowedPluralCompoundHead_ReturnsNoun`. Context-sensitive word-sense
disambiguation for ambiguous technical terms - a noun-modifier position before another noun
resolving the more plausible technical/compound sense rather than a verb reading - is verified
by `Guess_AmbiguousWordMixBeforeNoun_ReturnsNoun` and
`Guess_AmbiguousWordPortBeforeNoun_ReturnsNoun`. The idiom "in use" (the object of the
preposition "in") resolving as a noun, rather than a bare verb-only match, is verified by
`Guess_IdiomInUsePrecededByPreposition_ReturnsNoun`.

**Suggestion Self-Filtering (supplementary regression scenario, no linked requirement)**: A
diagnostic's suggestion/citations never
recommend the exact word that was just flagged as one of its own alternatives, for both a
confidently-resolved single candidate sense and an ambiguous multi-sense result with no
confidently-resolving context. This scenario is tested by
`Evaluate_SuggestionNeverIncludesFlaggedWordItself` and
`Evaluate_AmbiguousSuggestionNeverIncludesFlaggedWordItself`.

**Compound-Phrase and Quoted-Mention Exclusion (supplementary regression scenario, no linked
requirement)**: A disallowed word that is part of a
project-approved multi-word compound/technical term (via the existing `allow-in-phrase`
mechanism) is not flagged within that phrase, while the same word used alone elsewhere in the
same document is still flagged; and a disallowed word appearing only as a mention inside a
quotation (not a use in the document's own prose) is not flagged. This scenario is tested by
`Evaluate_WordInsideApprovedCompoundPhrase_NotFlaggedButAloneElsewhereStillFlagged` and
`Evaluate_DisallowedTermInsideQuotedMention_NotFlagged`.

**Admonition-Label and Table-Header Exclusion (supplementary regression scenario, no linked
requirement)**: A bold admonition label at the start of a block
(for example "**Caution.**") and a table column-header cell (for example "Hazard") are
recognized as labels, not prose, and are excluded from the dictionary check entirely, while a
disallowed term in an adjacent ordinary sentence or table data cell is still flagged. This
scenario is tested by `Evaluate_DisallowedTermInsideAdmonitionLabel_NotFlagged` and
`Evaluate_DisallowedTermInTableHeaderCell_NotFlaggedButSameTermInDataCellStillFlagged`.

**Table-Header and Span-Detection Extraction (supplementary regression scenario, no linked
requirement)**: `MarkdownProseExtractor` tags a table's
header row (the row immediately followed by its `---` separator row) with the distinct
`SegmentRole.TableHeader`, while its data rows remain `SegmentRole.TableRow`, and a table row
with no following separator row (so its header-or-data status cannot be determined) remains
`SegmentRole.TableRow` rather than being misclassified. The extractor's span finders -
`FindAdmonitionLabelSpans` (a bold label at the very start of a block, not bold text appearing
mid-sentence) and `FindQuotedOrEmphasisSpans` (a double-quoted span, and a single-asterisk
emphasis span distinct from a double-asterisk bold span) - are verified directly. This scenario
is tested by `Extract_TableWithSeparatorRow_FirstRowIsTableHeader`,
`Extract_TableRowWithoutFollowingSeparator_RemainsTableRow`,
`FindAdmonitionLabelSpans_LabelAtBlockStart_ReturnsOneSpan`,
`FindAdmonitionLabelSpans_BoldTextMidSentence_ReturnsNoSpans`,
`FindQuotedOrEmphasisSpans_QuotedTitle_ReturnsSpan`, and
`FindQuotedOrEmphasisSpans_SingleAsteriskEmphasis_ReturnsSpanNotBold`.

**Ste100Mark-Linting-Configuration**: YAML configuration loading and resolution are verified for
defaults, malformed files, missing files, complete schemas, first-match-wins mode profiles,
and layered rules/dictionary profile deltas. This scenario is tested by
`Load_NullPath_ReturnsDefaultConfiguration`,
`Load_NonExistentPath_ThrowsInvalidOperationException`,
`Load_FullConfigurationFile_ParsesAllSections`, `Load_MalformedYaml_ThrowsInvalidOperationException`,
`ResolveMode_NoMatchingProfile_ReturnsDefaultMode`,
`ResolveMode_MatchingProfileGlob_ReturnsOverriddenMode`,
`ResolveMode_MultipleProfiles_UsesFirstMatch`,
`ResolveMode_MatchingProfileWithNullMode_FallsThroughToDefault`,
`ResolveRules_NoMatchingProfile_ReturnsGlobalRules`,
`ResolveRules_MatchingProfile_LayersDeltaOverGlobalRules`,
`ResolveRules_MultipleMatchingProfiles_LayersAllDeltasInOrder`,
`ResolveAllowedTerms_NoMatchingProfile_ReturnsGlobalTermsOnly`,
`ResolveAllowedTerms_MatchingProfile_UnionsWithGlobalAllowList`,
`ResolveAllowedPhrases_NoMatchingProfile_ReturnsGlobalPhrasesOnly`,
`ResolveAllowedPhrases_MatchingProfile_UnionsWithGlobalPhraseList`,
`Run_ProcedureModeOverride_AppliesStricterWordLimit`, and
`Run_ProfileDictionaryAllowList_PermitsTermOnlyWithinProfileGlob`.

**Ste100Mark-Linting-DictionaryPhraseScopedAllowance**: The `dictionary.allow-in-phrase`
phrase-scoped allowance is verified for suppressing a disallowed term only when the match falls
entirely inside a configured phrase while the same term elsewhere in the same segment is still
flagged, case-insensitive matching, matching across whitespace variation consistent with a
multi-word `Disallow` term, an unrelated configured phrase not suppressing an unmatched
occurrence, and omitting the `allowedPhrases` argument entirely (the default) not suppressing
any match. This scenario is tested by
`Evaluate_TermInsideAllowedPhrase_NotFlaggedButSameTermElsewhereStillFlagged`,
`Evaluate_TermInsideAllowedPhraseDifferentCasing_StillSuppressesDiagnostic`,
`Evaluate_AllowedPhraseMatchesAcrossWhitespace_SuppressesDiagnostic`,
`Evaluate_AllowedPhrasesUnrelatedPhrase_StillFlagsDisallowedTerm`, and
`Evaluate_NoAllowedPhrasesSupplied_StillFlagsTermInsideWouldBePhrase`. Configuration resolution
of the global `dictionary.allow-in-phrase` list unioned with a matching profile's
`dictionary.allow-in-phrase` delta is verified by
`ResolveAllowedPhrases_NoMatchingProfile_ReturnsGlobalPhrasesOnly` and
`ResolveAllowedPhrases_MatchingProfile_UnionsWithGlobalPhraseList` (listed above).

**Ste100Mark-Linting-CliIntegration**: The lint-specific CLI surface is verified for positional
globs, `--config`, `--format`, `--strict`, and default dispatch through `Program.Run`. This
scenario is tested by `Context_Create_NoArguments_ReturnsLintingDefaults`,
`Context_Create_PositionalArgument_CollectedAsGlob`,
`Context_Create_MultiplePositionalArguments_CollectedInOrder`,
`Context_Create_ConfigFlag_SetsConfigFile`,
`Context_Create_ConfigFlag_WithoutValue_ThrowsArgumentException`,
`Context_Create_FormatFlagJson_SetsJsonFormat`, `Context_Create_FormatFlagText_SetsTextFormat`,
`Context_Create_FormatFlag_UnsupportedValue_ThrowsArgumentException`,
`Context_Create_FormatFlag_WithoutValue_ThrowsArgumentException`,
`Context_Create_StrictFlag_SetsStrictTrue`, `Context_Create_AllowEmptyFlag_SetsAllowEmptyTrue`,
`Program_Run_NoArguments_DisplaysDefaultBehavior`,
and `Run_PositionalGlobs_OverrideConfigInclude`. Support for glob patterns and literal file
paths that are absolute (a Windows drive letter, a UNC path, or a POSIX-style leading `/`), in
addition to patterns relative to the current directory, is verified by
`Run_RelativeGlobPattern_MatchesConfiguredFiles`,
`Run_AbsoluteGlobPattern_MatchesConfiguredFiles`,
`Run_RelativeLiteralFilePath_MatchesSingleFile`,
`Run_AbsoluteLiteralFilePath_MatchesSingleFile`,
`Run_AbsoluteIncludeWithRelativeExclude_ExcludesMatchedFile`,
`Run_AbsoluteIncludeWithAbsoluteExclude_ExcludesMatchedFile`, and
`Run_AbsoluteIncludeWithMismatchedCasingAndRelativeExclude_ExcludesMatchedFile`.

**Ste100Mark-Linting-OutputFormats**: Text and JSON reporting are verified at the formatter level
and through the published CLI JSON path. This scenario is tested by
`Report_TextFormat_WritesDiagnosticLinesAndSummary`,
`Report_JsonFormat_WritesSingleJsonDocumentWithExpectedSchema`,
`Report_NoDiagnostics_WritesZeroCountSummary`,
`Report_NoFilesMatchedTrue_WritesFailureSummaryLine`,
`Report_NoFilesMatchedFalseWithZeroFilesChecked_WritesZeroCountSummary`,
`Report_JsonFormat_NoFilesMatchedTrue_IncludesNoFilesMatchedField`,
`Report_JsonFormat_WritesCitationsForDictionaryDiagnostic`, and
`Ste100Mark_LintWithJsonFormat_ProducesSingleValidJsonDocument`.

**Ste100Mark-Linting-ReportedFiles**: The JSON report's `files` array is verified to list every
file the effective selection resolved to, with its relative path and `"checked"` status, and to
omit files removed by a configured exclude pattern. This scenario is tested by
`Report_JsonFormat_WritesSingleJsonDocumentWithExpectedSchema`,
`Report_JsonFormat_NoFilesMatchedTrue_IncludesNoFilesMatchedField`,
`Run_JsonFormat_FilesArrayListsCheckedFilesAndOmitsExcludedFiles`, and
`Ste100Mark_LintWithJsonFormat_ReportsCheckedFilesArray`.

**Ste100Mark-Linting-ExitCode**: Exit-code behavior is verified for clean files, build-breaking
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

**Ste100Mark-Linting-EmptyFileSet**: The distinct zero-files-matched exit code (2), the JSON
`noFilesMatched` field, the distinct text wording, and the `--allow-empty` opt-out are verified
for both the CLI-glob and configured-include/exclude routes to zero matched files, in-process and
through the published CLI. This scenario is tested by
`Run_NoFilesMatchViaGlobs_ProducesNoFilesMatchedExitCode`,
`Run_NoFilesMatchViaConfigInclude_ProducesNoFilesMatchedExitCode`,
`Run_NoFilesMatchViaConfigIncludeExcludeSubtraction_ProducesNoFilesMatchedExitCode`,
`Run_NoFilesMatchWithAllowEmpty_ProducesSuccessExitCode`,
`Report_NoFilesMatchedTrue_WritesFailureSummaryLine`,
`Report_NoFilesMatchedFalseWithZeroFilesChecked_WritesZeroCountSummary`,
`Report_JsonFormat_NoFilesMatchedTrue_IncludesNoFilesMatchedField`,
`Ste100Mark_LintWithNoMatchingFiles_ReturnsNoFilesMatchedExitCode`,
`Ste100Mark_LintWithNoMatchingFilesAndAllowEmpty_ReturnsZeroExitCode`, and
`Ste100Mark_LintWithNoMatchingFilesJsonFormat_ReportsNoFilesMatchedTrue`.

**Ste100Mark-Linting-RuleCatalog**: The rule catalog is verified to contain exactly every rule
code the linter can emit, to classify official/mechanical/advisory status and suggestion kind
correctly, to report both writing modes for every rule, and to serialize as a valid camelCase
JSON array. This scenario is tested by `RuleCatalog_Entries_ContainsEveryEmittedRuleCode`,
`RuleCatalog_Entries_ClassifiesOfficialMechanicalAndAdvisoryRulesCorrectly`,
`RuleCatalog_Entries_ClassifiesSuggestionKindCorrectly`,
`RuleCatalog_Entries_ModesIncludeBothWritingModes`, and
`RuleCatalog_ToJson_ProducesValidCamelCaseJsonArray`.
