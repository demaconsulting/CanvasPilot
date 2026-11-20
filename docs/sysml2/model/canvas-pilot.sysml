package CanvasPilot {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * CanvasPilot is an AI-assisted drawing application built on CanvasNet.
     * The current build contains the logging, settings, and utility subsystems; further subsystems
     * are added to this system as they are introduced.
     */

    part def CanvasPilotSystem {
        doc /* The CanvasPilot system as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/CanvasPilotTests.cs");
            designFiles = ("docs/design/canvas-pilot.md");
            verificationFiles = ("docs/verification/canvas-pilot.md");
            requirementFiles = ("docs/reqstream/canvas-pilot.yaml");
        }

        part logging : LoggingSubsystem;
        part settings : SettingsSubsystem;
        part utilities : UtilitiesSubsystem;
    }
}
