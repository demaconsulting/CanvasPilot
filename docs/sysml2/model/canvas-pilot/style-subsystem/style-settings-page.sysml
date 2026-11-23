package CanvasPilot_StyleSettingsPage {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_ProjectService::*;
    private import CanvasPilot_SettingsDialog::*;
    private import CanvasPilot_StyleKitCatalog::*;
    private import CanvasPilot_StylePicker::*;

    part def StyleSettingsPage {
        doc /* The Style Kits page of the settings dialog: the shared folders in which a team keeps style kits, in the order in which they win, each with whether the kits in it could be read, added by a typed full path or by the folder picker, removed, and moved, and the kit that a new project starts with, merging only the style settings into the settings when applied. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleFolderItem.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKitChoice.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleSettingsPageViewModel.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleSettingsPageView.axaml",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleSettingsPageView.axaml.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/StyleSettingsPageTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/StyleSettingsPageViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/AccessibilityViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/style-subsystem/style-settings-page.md");
            verificationFiles = ("docs/verification/canvas-pilot/style-subsystem/style-settings-page.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/style-subsystem/style-settings-page.yaml");
        }

        // Shows what could be read of the shared folders and which kits can be the default.
        dependency StyleSettingsPage to StyleKitCatalog;

        // Uses the words for the places of kits.
        dependency StyleSettingsPage to StylePicker;

        // Is a page of the settings dialog and merges into the settings through the page contract.
        dependency StyleSettingsPage to SettingsDialog;

        // Asks the platform folder picker, which the main window supplies, for a folder.
        dependency StyleSettingsPage to ProjectService;
    }
}
