package Ste100Mark_Program {
    private import SoftwareItemMetadata::*;
    private import Ste100Mark_Context::*;
    private import Ste100Mark_Linter::*;
    private import Ste100Mark_RuleCatalog::*;
    private import Ste100Mark_Validation::*;

    part def Program {
        doc /* Entry point and execution orchestrator. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Ste100Mark/Program.cs");
            testFiles = ("test/DemaConsulting.Ste100Mark.Tests/ProgramTests.cs");
            designFiles = ("docs/design/ste100-mark/program.md");
            verificationFiles = ("docs/verification/ste100-mark/program.md");
            requirementFiles = ("docs/reqstream/ste100-mark/program.yaml");
        }
    }

    // Program reads parsed flags and writes output through Context, prints the rule catalog,
    // runs self-validation, and dispatches the default path to the Linter.
    dependency Program to Context;
    dependency Program to Linter;
    dependency Program to RuleCatalog;
    dependency Program to Validation;
}
