package CanvasPilot_ExportPresetDialog {
    private import SoftwareItemMetadata::*;

    part def ExportPresetDialog {
        doc /* Modal Export PNG window and view model that list the ways to export the shown picture (as it is shown, the sizes of the style kit, the sizes of the application), say what the file will be, and report the choice. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/ExportPresetDialogViewModel.cs",
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/ExportPresetItemViewModel.cs",
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/ExportPresetDialogView.axaml",
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/ExportPresetDialogView.axaml.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/PreviewSubsystem/ExportPresetDialogTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ExportPresetDialogViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/preview-subsystem/export-preset-dialog.md");
            verificationFiles = ("docs/verification/canvas-pilot/preview-subsystem/export-preset-dialog.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/preview-subsystem/export-preset-dialog.yaml");
        }
    }
}
