package CanvasPilot {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * CanvasPilot is an AI-assisted drawing application built on CanvasNet.
     * The model contains the application shell, annotation, chat, composer, logging, payload, preview, project, settings,
     * speech, and utility subsystems. Messages are composed, dictated, marked, and sent into a payload without an agent;
     * the AI agent that would answer them is out of scope of this version.
     */

    part def CanvasPilotSystem {
        doc /* The CanvasPilot system as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/CanvasPilotTests.cs");
            designFiles = ("docs/design/canvas-pilot.md");
            verificationFiles = ("docs/verification/canvas-pilot.md");
            requirementFiles = ("docs/reqstream/canvas-pilot.yaml");
        }

        part annotation : AnnotationSubsystem;
        part appShell : AppShellSubsystem;
        part chat : ChatSubsystem;
        part composer : ComposerSubsystem;
        part logging : LoggingSubsystem;
        part payload : PayloadSubsystem;
        part preview : PreviewSubsystem;
        part project : ProjectSubsystem;
        part settings : SettingsSubsystem;
        part speech : SpeechSubsystem;
        part utilities : UtilitiesSubsystem;

        // The user interface framework, its text editing control, and the view model source generators are consumed at
        // run time by the subsystems that have views and view models.
        part userInterface : Ots::Avalonia;
        part textEditor : Ots::AvaloniaEdit;
        part viewModelToolkit : Ots::CommunityToolkitMvvm;

        // CanvasNet is consumed at run time by the Preview, Project, and Payload subsystems.
        part canvasNet : Ots::CanvasNet;

        // The speech library is consumed at run time by the Speech subsystem.
        part speechLibrary : Ots::Speech;

        // The Windows installer package is built by the WiX Toolset from the published application.
        part installer : Ots::WiX;
    }
}
