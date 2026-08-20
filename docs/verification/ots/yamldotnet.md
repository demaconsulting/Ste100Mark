## YamlDotNet Verification

This document provides the verification evidence for the YamlDotNet OTS software item.
Requirements for this OTS item are defined in the YamlDotNet OTS Software Requirements document.

### Required Functionality

YamlDotNet is the YAML deserialization library used by the Linting subsystem. It parses
`.ste100mark.yaml` lint configuration files and project dictionary files into strongly typed
local models via `DeserializerBuilder`, `HyphenatedNamingConvention`, and `Deserialize<T>`.
Correct parsing, error reporting, and default-handling behavior confirm the library is
functioning correctly for the project's usage.

### Verification Approach

YamlDotNet has no dedicated self-validation CLI of its own, so it is verified by transitive
evidence from the Linting subsystem's own passing tests. Each scenario names a specific test
method that exercises `LintConfig.Load` or `LintDictionary.ParseDictionaryYaml`, both of which
depend directly on YamlDotNet to deserialize YAML content. A passing test run for all scenarios
constitutes evidence that YamlDotNet correctly deserializes valid YAML, rejects malformed YAML,
and supports the default/override merge behavior the subsystem relies on.

### Test Scenarios

#### Load_FullConfigurationFile_ParsesAllSections

**Scenario**: A full `.ste100mark.yaml` configuration file with every supported section is
deserialized by YamlDotNet.

**Expected**: YamlDotNet parses every section into the strongly typed `LintConfig` model without
error.

**Requirement coverage**: `Ste100Mark-OTS-YamlDotNet`.

#### Load_MalformedYaml_ThrowsInvalidOperationException

**Scenario**: A configuration file containing malformed YAML is deserialized by YamlDotNet.

**Expected**: YamlDotNet raises a parse error that the caller wraps in
`InvalidOperationException`.

**Requirement coverage**: `Ste100Mark-OTS-YamlDotNet`.

#### Load_NullPath_ReturnsDefaultConfiguration

**Scenario**: No configuration path is supplied, so no YamlDotNet deserialization is attempted.

**Expected**: The default configuration model is returned without invoking the deserializer.

**Requirement coverage**: `Ste100Mark-OTS-YamlDotNet`.

#### Load_NonExistentPath_ThrowsInvalidOperationException

**Scenario**: A configuration path that does not exist on disk is supplied.

**Expected**: The caller reports a consistent `InvalidOperationException` before any
YamlDotNet deserialization is attempted.

**Requirement coverage**: `Ste100Mark-OTS-YamlDotNet`.

#### Load_DefaultConfig_IncludesEmbeddedEntries

**Scenario**: The embedded illustrative dictionary is deserialized by YamlDotNet into the
in-memory dictionary model.

**Expected**: YamlDotNet parses the embedded YAML into entries that are present in the merged
dictionary.

**Requirement coverage**: `Ste100Mark-OTS-YamlDotNet`.

#### Load_ProjectDictionaryFile_MergedOverEmbedded

**Scenario**: A project-supplied dictionary file is deserialized by YamlDotNet and merged over
the embedded dictionary.

**Expected**: YamlDotNet parses the project file's entries, and they take precedence over
embedded entries with the same term.

**Requirement coverage**: `Ste100Mark-OTS-YamlDotNet`.

#### Load_MissingProjectDictionaryFile_ThrowsInvalidOperationException

**Scenario**: A configured project dictionary file path does not exist on disk.

**Expected**: The caller reports a consistent `InvalidOperationException` before any
YamlDotNet deserialization is attempted.

**Requirement coverage**: `Ste100Mark-OTS-YamlDotNet`.

#### Load_InlineDisallowEntry_AddedToMergedDictionary

**Scenario**: An inline disallow entry defined directly in the YAML configuration is
deserialized by YamlDotNet.

**Expected**: YamlDotNet parses the inline entry into the configuration model, and it is added
to the merged dictionary.

**Requirement coverage**: `Ste100Mark-OTS-YamlDotNet`.
