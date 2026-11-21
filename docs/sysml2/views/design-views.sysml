// Views rendered into docs/design/generated/ for the Design document.
// Declares one repository-level views package that imports the system and subsystem
// packages so each 'expose' statement can reference its target part def directly. Per
// SysML2Tools syntax, 'expose' is only valid inside a named 'view' usage (not a
// 'view def' definition), and it - not 'render' - is what scopes the rendered diagram
// content.
//
// This file is a sibling of docs/sysml2/model/ (not nested inside it), so a single
// 'docs/sysml2/model/**/*.sysml' glob never needs to exclude view files.
//
// Each container view exposes the container part def and its direct children
// ('expose X; expose X::*;') and renders 'asInterconnectionDiagram' so nested
// composition (and, where declared, 'dependency' relationships) render correctly.
package CanvasPilotDesignViews {
    private import StandardViewDefinitions::*;
    private import Views::*;
    private import OtsDependencies::*;
    private import CanvasPilot::*;
    private import CanvasPilot_AnnotationSubsystem::*;
    private import CanvasPilot_AppShellSubsystem::*;
    private import CanvasPilot_ChatSubsystem::*;
    private import CanvasPilot_ComposerSubsystem::*;
    private import CanvasPilot_KnowledgeSubsystem::*;
    private import CanvasPilot_LoggingSubsystem::*;
    private import CanvasPilot_PayloadSubsystem::*;
    private import CanvasPilot_PreviewSubsystem::*;
    private import CanvasPilot_ProjectSubsystem::*;
    private import CanvasPilot_SettingsSubsystem::*;
    private import CanvasPilot_SpeechSubsystem::*;
    private import CanvasPilot_StyleSubsystem::*;
    private import CanvasPilot_UtilitiesSubsystem::*;

    view SoftwareStructureView : InterconnectionView {
        expose CanvasPilot::CanvasPilotSystem::**;  // system part def, recursively: subsystems + units
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

    view KnowledgeSubsystemView {
        expose KnowledgeSubsystem;
        expose KnowledgeSubsystem::*;
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

    view StyleSubsystemView {
        expose StyleSubsystem;
        expose StyleSubsystem::*;
        render asInterconnectionDiagram;
    }

    view UtilitiesSubsystemView {
        expose UtilitiesSubsystem;
        expose UtilitiesSubsystem::*;
        render asInterconnectionDiagram;
    }
}
