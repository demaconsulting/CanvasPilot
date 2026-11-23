package CanvasPilot_SettingsService {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_PathHelpers::*;
    private import CanvasPilot_RollingFileLogger::*;

    part def SettingsService {
        doc /* Loads, validates, and atomically saves the versioned per-user settings file, including the reasoning effort of the AI provider as the text of the provider's own id and the read of the reasoning level of earlier files as that effort (without a new version of the file), the layout of the main window (size, place, state, and the widths of the explorer and the chat) kept within bounds, the theme choice and the options of the explorer, and the time at which each recent project was opened. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/AppSettings.cs",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/ISettingsService.cs",
                "src/DemaConsulting.CanvasPilot/SettingsSubsystem/SettingsService.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/SettingsSubsystem/SettingsServiceTests.cs");
            designFiles = ("docs/design/canvas-pilot/settings-subsystem/settings-service.md");
            verificationFiles = ("docs/verification/canvas-pilot/settings-subsystem/settings-service.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/settings-subsystem/settings-service.yaml");
        }

        // Resolves its file paths with the safe path-combination utility.
        dependency SettingsService to PathHelpers;

        // Reports bad or unreadable settings files to the application log.
        dependency SettingsService to RollingFileLogger;
    }
}
