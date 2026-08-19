## YamlDotNet

This document describes the integration and usage design for the `YamlDotNet` OTS software item.

### Purpose

YamlDotNet is chosen as the YAML deserialization library for the project. It parses
`.ste100mark.yaml` lint configuration files and project dictionary files into strongly typed
local models used by the `Linting` subsystem.

### Features Used

- `DeserializerBuilder` for constructing YAML deserializers
- `HyphenatedNamingConvention` for mapping kebab-case YAML keys to C# properties
- `IgnoreUnmatchedProperties` so forward-compatible extra YAML keys do not break parsing
- `Deserialize<T>` for binding configuration and dictionary files into local types

### Integration Pattern

YamlDotNet is consumed as a NuGet package reference in the main application project.
`LintConfig.Load` creates a deserializer for the root `.ste100mark.yaml` schema, and
`LintDictionary.ParseDictionaryYaml` creates a deserializer for the term-to-entry dictionary
schema. The library is used only for one-shot file parsing; no global initialization, shared
serializer cache, or explicit disposal is required. Parse failures are caught locally and
wrapped in `InvalidOperationException` so the tool reports a consistent expected-error shape.
