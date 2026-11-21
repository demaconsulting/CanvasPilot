package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def StylePicker {
        doc /* The list in the status bar of the main window with which the user chooses the style kit of the open project: no style and every kit that is not hidden by a kit of a more local place, the kit that the project uses as the selected choice, the kit that the project names and that cannot be found as a choice that says so, the choice written into the project file, and the reason when a choice cannot be kept. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StylePickerItem.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StylePickerViewModel.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StylePickerView.axaml",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StylePickerView.axaml.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/StylePickerTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/StylePickerViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/style-subsystem/style-picker.md");
            verificationFiles = ("docs/verification/canvas-pilot/style-subsystem/style-picker.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/style-subsystem/style-picker.yaml");
        }

        // Shows and changes the kit that the open project uses.
        dependency StylePicker to StyleSelection;

        // Lists the kits that can be chosen.
        dependency StylePicker to StyleKitCatalog;

        // Says whether a project is open.
        dependency StylePicker to ProjectService;
    }
}
