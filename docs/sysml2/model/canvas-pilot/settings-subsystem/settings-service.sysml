package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def SettingsService {
        doc /* Loads, validates, and atomically saves the versioned per-user settings file. */

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
