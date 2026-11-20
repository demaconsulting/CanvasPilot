package CanvasPilot {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * CanvasPilot is an AI-assisted drawing application built on CanvasNet.
     * The current build contains the application shell, logging, preview, project, settings, speech, and utility
     * subsystems; further subsystems are added to this system as they are introduced.
     */

    part def CanvasPilotSystem {
        doc /* The CanvasPilot system as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/CanvasPilotTests.cs");
            designFiles = ("docs/design/canvas-pilot.md");
            verificationFiles = ("docs/verification/canvas-pilot.md");
            requirementFiles = ("docs/reqstream/canvas-pilot.yaml");
        }

        part appShell : AppShellSubsystem;
        part logging : LoggingSubsystem;
        part preview : PreviewSubsystem;
        part project : ProjectSubsystem;
        part settings : SettingsSubsystem;
        part speech : SpeechSubsystem;
        part utilities : UtilitiesSubsystem;

        // CanvasNet is consumed at run time by the Preview and Project subsystems.
        part canvasNet : Ots::CanvasNet;

        // The speech library is consumed at run time by the Speech subsystem.
        part speechLibrary : Ots::Speech;

        // The Windows installer package is built by the WiX Toolset from the published application.
        part installer : Ots::WiX;
    }
}
