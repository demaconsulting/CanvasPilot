package CanvasPilot_ThumbnailProvider {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_ImageRenderService::*;
    private import CanvasPilot_PreviewPane::*;
    private import CanvasPilot_ProjectExplorer::*;

    part def ThumbnailProvider {
        doc /* Makes the small pictures that the project explorer shows beside the rows of drawable files, and the larger ones of the welcome screen: draws the file through the render service at the size of the thumbnail (56 by 36 pixels by default) on a white background, off the user interface thread, at most two files at once; keeps the pixels of the latest thumbnails (256 by default, the least recently used goes first, keyed by file, time, size, and picture size); and gives every caller a bitmap of its own. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/PreviewSubsystem/ThumbnailProvider.cs",
                "src/DemaConsulting.CanvasPilot/ProjectSubsystem/IThumbnailSource.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.UiTests/ThumbnailViewTests.cs");
            designFiles = ("docs/design/canvas-pilot/preview-subsystem/thumbnail-provider.md");
            verificationFiles = ("docs/verification/canvas-pilot/preview-subsystem/thumbnail-provider.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/preview-subsystem/thumbnail-provider.yaml");
        }

        // The number of thumbnails the cache holds.
        attribute capacity : ScalarValues::Integer;

        // The width of a thumbnail of the explorer in pixels.
        attribute thumbnailWidth : ScalarValues::Integer;

        // The height of a thumbnail of the explorer in pixels.
        attribute thumbnailHeight : ScalarValues::Integer;

        // How many files are drawn at the same time.
        attribute maximumConcurrentRenders : ScalarValues::Integer;

        // Draws the files at the size of a thumbnail.
        dependency ThumbnailProvider to ImageRenderService;

        // Turns the cached pixels into a bitmap for the caller.
        dependency ThumbnailProvider to PreviewPane;

        // Implements the thumbnail source that the explorer declares and asks for.
        dependency ThumbnailProvider to ProjectExplorer;
    }
}
