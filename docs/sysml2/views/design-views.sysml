// Views rendered into docs/design/generated/ for the Design document.
// Reopens the CanvasPilot package so each 'expose' statement can reference
// its target part def directly. Per SysML2Tools syntax, 'expose' is only valid
// inside a named 'view' usage (not a 'view def' definition), and it - not
// 'render' - is what scopes the rendered diagram content.
//
// This file is a sibling of docs/sysml2/model/ (not nested inside it), so a single
// 'docs/sysml2/model/**/*.sysml' glob never needs to exclude view files.
//
// Each container view exposes the container part def and its direct children
// ('expose X; expose X::*;') and renders 'asInterconnectionDiagram' so nested
// composition (and, where declared, 'dependency' relationships) render correctly.
package CanvasPilot {
    private import StandardViewDefinitions::*;

    view SoftwareStructureView : InterconnectionView {
        expose CanvasPilot::**;
    }

    view CanvasPilotView {
        expose CanvasPilotSystem;
        expose CanvasPilotSystem::*;
        render asInterconnectionDiagram;
    }

    view AnnotationSubsystemView {
        expose AnnotationSubsystem;
        expose AnnotationSubsystem::*;
        render asInterconnectionDiagram;
    }

    view AppShellSubsystemView {
        expose AppShellSubsystem;
        expose AppShellSubsystem::*;
        render asInterconnectionDiagram;
    }

    view ChatSubsystemView {
        expose ChatSubsystem;
        expose ChatSubsystem::*;
        render asInterconnectionDiagram;
    }

    view ComposerSubsystemView {
        expose ComposerSubsystem;
        expose ComposerSubsystem::*;
        render asInterconnectionDiagram;
    }

    view LoggingSubsystemView {
        expose LoggingSubsystem;
        expose LoggingSubsystem::*;
        render asInterconnectionDiagram;
    }

    view PayloadSubsystemView {
        expose PayloadSubsystem;
        expose PayloadSubsystem::*;
        render asInterconnectionDiagram;
    }

    view PreviewSubsystemView {
        expose PreviewSubsystem;
        expose PreviewSubsystem::*;
        render asInterconnectionDiagram;
    }

    view ProjectSubsystemView {
        expose ProjectSubsystem;
        expose ProjectSubsystem::*;
        render asInterconnectionDiagram;
    }

    view SettingsSubsystemView {
        expose SettingsSubsystem;
        expose SettingsSubsystem::*;
        render asInterconnectionDiagram;
    }

    view SpeechSubsystemView {
        expose SpeechSubsystem;
        expose SpeechSubsystem::*;
        render asInterconnectionDiagram;
    }

    view UtilitiesSubsystemView {
        expose UtilitiesSubsystem;
        expose UtilitiesSubsystem::*;
        render asInterconnectionDiagram;
    }
}
