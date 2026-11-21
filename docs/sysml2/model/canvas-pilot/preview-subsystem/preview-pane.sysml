package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def PreviewPane {
        doc /* Follows the project explorer's selected file, renders it in the background, discards stale results, renders again when files change, shows the picture, busy state, errors, and notes, hosts a layer over the picture in the picture's own pixel coordinates and a place in its top bar for the controls of that layer, and starts the drag of the picture out of the application. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/IRenderedImageSource.cs",
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/IRenderNoteSink.cs",
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/PreviewPaneViewModel.cs",
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/PreviewPaneView.axaml",
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/PreviewPaneView.axaml.cs",
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/RenderedImageBitmap.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/PreviewSubsystem/PreviewPaneTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/PreviewSubsystem/RenderedImageBitmapTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/PreviewSubsystem/PreviewSubsystemFakes.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/PreviewPaneViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/preview-subsystem/preview-pane.md");
            verificationFiles = ("docs/verification/canvas-pilot/preview-subsystem/preview-pane.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/preview-subsystem/preview-pane.yaml");
        }

        // Renders the selected file through the render service.
        dependency PreviewPane to ImageRenderService;

        // Follows the explorer's selected file and project folder, and shares its refresh scheduler.
        dependency PreviewPane to ProjectExplorer;

        // Ignores the project file and the hidden data folder when files change.
        dependency PreviewPane to ProjectService;

        // Asks for the shown picture as a PNG file when the user drags it out.
        dependency PreviewPane to ImageDragSource;

        // Reports unexpected render failures and watcher problems to the application log.
        dependency PreviewPane to RollingFileLogger;
    }
}
