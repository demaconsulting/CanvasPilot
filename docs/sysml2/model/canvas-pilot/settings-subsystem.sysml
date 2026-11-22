package CanvasPilot_SettingsSubsystem {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_AiProviderSettingsPage::*;
    private import CanvasPilot_AppearanceSettingsPage::*;
    private import CanvasPilot_SettingsDialog::*;
    private import CanvasPilot_SettingsService::*;

    part def SettingsSubsystem {
        doc /* Persisted user settings and the modal settings dialog with its pages. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/SettingsSubsystem/SettingsSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/settings-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/settings-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/settings-subsystem.yaml");
        }

        part settingsService : SettingsService;
        part settingsDialog : SettingsDialog;
        part aiProviderSettingsPage : AiProviderSettingsPage;
        part appearanceSettingsPage : AppearanceSettingsPage;
    }
}
