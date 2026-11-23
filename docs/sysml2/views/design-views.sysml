// Views rendered into docs/design/generated/ for the Design document.
// Per SysML2Tools syntax, 'expose' is only valid inside a named 'view' usage
// (not a 'view def' definition), and it - not 'render' - is what scopes the
// rendered diagram content.
package Ste100MarkDesignViews {
    private import StandardViewDefinitions::*;
    private import Ste100Mark::*;

    view SoftwareStructureView : InterconnectionView {
        expose Ste100Mark::Ste100MarkSystem;
    }

    view Ste100MarkView {
        expose Ste100MarkSystem;
    }

    view CliView {
        expose CliSubsystem;
    }

    view LintingView {
        expose LintingSubsystem;
    }

    view SelfTestView {
        expose SelfTestSubsystem;
    }

    view UtilitiesView {
        expose UtilitiesSubsystem;
    }
}
