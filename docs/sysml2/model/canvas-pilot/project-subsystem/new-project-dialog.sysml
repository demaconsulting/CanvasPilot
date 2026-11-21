package CanvasPilot_NewProjectDialog {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_PathHelpers::*;
    private import CanvasPilot_ProjectService::*;

    part def NewProjectDialog {
        doc /* Modal New Project window that collects and validates a project name and location. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/IFolderPicker.cs",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/NewProjectDialogViewModel.cs",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/NewProjectDialogView.axaml",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/NewProjectDialogView.axaml.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/ProjectSubsystem/NewProjectDialogTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/NewProjectDialogViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/project-subsystem/new-project-dialog.md");
            verificationFiles = ("docs/verification/canvas-pilot/project-subsystem/new-project-dialog.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/project-subsystem/new-project-dialog.yaml");
        }

        // Validates the entries through the project service.
        dependency NewProjectDialog to ProjectService;

        // Computes the project folder path with the safe path-combination utility.
        dependency NewProjectDialog to PathHelpers;
    }
}
