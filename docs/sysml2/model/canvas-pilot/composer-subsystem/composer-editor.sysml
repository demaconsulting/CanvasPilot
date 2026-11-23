package CanvasPilot_ComposerEditor {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_AttachmentIntake::*;
    private import CanvasPilot_ComposerViewModel::*;
    private import OtsDependencies::*;

    part def ComposerEditor {
        doc /* The view of the message being composed: one editing surface in which typed text, dictated text, and chips for attached files, folders, pictures, and marks stand in order. It shows what the view model holds and reports every change the user makes as one action of the view model, writes every other change of the message into the surface and keeps the surface equal to the message, takes undo, redo, paste, copy, cut, and drops over from the surface and handles dropped and pasted content as data, draws chips inline as pills with their name, icon or picture, and a named delete button (a mark chip is tinted in the mark color), shows dictated words with a background, shows the live words of a dictation and the sentence about the last action, and shows a placeholder over the empty message. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/ChipElementGenerator.cs",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/ChipView.axaml",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/ChipView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/ComposerEditorView.axaml",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/ComposerEditorView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/DataTransferReader.cs",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/DictatedTextBackgroundRenderer.cs",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/IChipThumbnailProvider.cs",
                "src/DemaConsulting.CanvasPilot/ComposerSubsystem/ImageChipThumbnailProvider.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.UiTests/ComposerEditorSupportTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ComposerEditorViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ComposerUiTestSupport.cs"
            );
            designFiles = ("docs/design/canvas-pilot/composer-subsystem/composer-editor.md");
            verificationFiles = ("docs/verification/canvas-pilot/composer-subsystem/composer-editor.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/composer-subsystem/composer-editor.yaml");
        }

        // Reports every action of the user to it, shows what it holds, and uses it as the target of undo and redo.
        dependency ComposerEditor to ComposerViewModel;

        // Hands it what a drop or the clipboard brought, in the form of its dropped content.
        dependency ComposerEditor to AttachmentIntake;

        // Draws the message in the editing surface with chips inline, and takes over its undo, paste, copy, cut, and drops.
        dependency ComposerEditor to AvaloniaEdit;
    }
}
