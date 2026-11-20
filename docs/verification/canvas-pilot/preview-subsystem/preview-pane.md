### PreviewPane

#### Verification Approach

`PreviewPane` is verified at three levels. Unit tests in `PreviewPaneTests.cs` drive `PreviewPaneViewModel` over a
real `ProjectExplorerViewModel` on temporary folders, with a hand-written `FakeImageRenderService` whose renders wait
until the test completes them, so the order in which results arrive is controlled and stale renders and their
cancellation tokens can be inspected. A hand-written `FakeRefreshScheduler` holds the pane's delayed renders until a
test runs them. Two tests let the real `FileSystemWatcher` report a change and wait, with a timeout, for the pane to
request the render. `RenderedImageBitmapTests.cs` checks the pixel conversion without an Avalonia platform. Headless
UI tests in `PreviewPaneViewTests.cs` (the `UiTests` project) show the real `PreviewPaneView` with the real render
service, and `MainWindowViewTests.cs` shows the pane inside the main window.

#### Test Environment

Unit tests use the standard test environment. UI tests run under the Avalonia headless platform with the Skia
renderer, serialized with test parallelization disabled; no display or windowing system is required. Tests use
temporary local folders and real file I/O; no network or external services are required.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- Selecting a file renders it in the project folder, shows busy, then the picture, its status line, and its notes;
  a pane created over an existing selection renders at once.
- A newer selection cancels the running render's token, and results of the cancelled render, success or error,
  never replace the newer picture whichever result arrives first; clearing the selection, closing the project, and
  disposing cancel and clear.
- A failed render shows its code, message, and hint (or no hint) and no picture; an unexpected failure of the render
  service shows `operation-failed` and leaves nothing busy; a cancelled result leaves nothing shown.
- A burst of re-render requests leaves one pending render with the 400 millisecond delay, the old picture stays
  visible while the new one renders, a pending render is discarded when the selection changes, and the real watcher
  requests a render for changes in the project folder and its sub-folders but not while nothing is selected.
- Changes in the hidden data folder and to the project file are not relevant; all other paths are.
- Straight-alpha RGBA converts to premultiplied BGRA with rounding, honoring the row size; the created bitmap has the
  picture's size and pixels; fit and actual size change the stretch of the `Viewbox` around the picture and the scroll
  bars.
- The overlay slot is empty and hidden by default, shows the content assigned to it exactly over the picture, has the
  picture's pixel size in its own coordinates at actual size and when the picture is scaled down to the pane, and is
  scaled on screen by the same factor as the picture.

#### Test Scenarios

**PreviewPaneViewModel_SelectFile_RenderSucceeds_ShowsBusyThenImageStatusAndNotes**: Selecting a file starts one
render with the file and the project folder; the pane is busy with the status `Rendering a.svg...`; completing the
render with an 8 by 4 picture and one warning shows the picture, the note, the header `1 note`, the status
`a.svg: 8 x 4 px, svg, 1 note`, and no placeholder or error, and announces the picture. This scenario is tested by
`PreviewPaneViewModel_SelectFile_RenderSucceeds_ShowsBusyThenImageStatusAndNotes`.

**PreviewPaneViewModel_New_FileAlreadySelected_StartsRender**: A pane created while a file is selected starts a
render. This scenario is tested by `PreviewPaneViewModel_New_FileAlreadySelected_StartsRender`.

**PreviewPaneViewModel_SelectAnotherFile_CancelsRunningRenderAndIgnoresStaleResult**: With a render running,
another file is selected; the first token is cancelled and the second is not. Both renders are completed, the stale
one first and then last, and only the second picture is shown. This scenario is tested by
`PreviewPaneViewModel_SelectAnotherFile_CancelsRunningRenderAndIgnoresStaleResult`.

**PreviewPaneViewModel_SelectAnotherFile_StaleErrorIsIgnored**: The newer render succeeds and then the older one
reports an error; the picture stays and no error shows. This scenario is tested by
`PreviewPaneViewModel_SelectAnotherFile_StaleErrorIsIgnored`.

**PreviewPaneViewModel_RenderFails_ShowsCodeMessageAndHintAndClearsImage**: After a picture, a failing render with
code `scad-error`, a message, and a hint shows the title `Render failed (scad-error)`, the message, `Hint: ...`, no
picture, nothing to export, and the status `b.svg: render failed`; the next successful render clears the error. This
scenario is tested by `PreviewPaneViewModel_RenderFails_ShowsCodeMessageAndHintAndClearsImage`.

**PreviewPaneViewModel_RenderFailsWithoutHint_ShowsNoHint**: An error without a hint shows an empty hint. This
scenario is tested by `PreviewPaneViewModel_RenderFailsWithoutHint_ShowsNoHint`.

**PreviewPaneViewModel_RenderServiceThrows_ShowsOperationFailedAndIsNotBusy**: A render service that throws gives
the error `operation-failed` and leaves the pane idle. This scenario is tested by
`PreviewPaneViewModel_RenderServiceThrows_ShowsOperationFailedAndIsNotBusy`.

**PreviewPaneViewModel_CurrentRenderReportsCancelled_IsNotBusyAndShowsNothing**: A cancelled result for the current
render leaves the pane idle with the placeholder. This scenario is tested by
`PreviewPaneViewModel_CurrentRenderReportsCancelled_IsNotBusyAndShowsNothing`.

**PreviewPaneViewModel_SelectionCleared_CancelsRenderAndClearsPane**: Clearing the selection cancels the running
render, ignores its late result, and shows the placeholder with an empty status. This scenario is tested by
`PreviewPaneViewModel_SelectionCleared_CancelsRenderAndClearsPane`.

**PreviewPaneViewModel_ProjectClosed_CancelsRenderAndClearsPane**: Closing the project cancels and ignores the
running render and says no project is open. This scenario is tested by
`PreviewPaneViewModel_ProjectClosed_CancelsRenderAndClearsPane`.

**PreviewPaneViewModel_Dispose_CancelsRenderIgnoresResultAndIsRepeatable**: Disposing twice cancels the running
render, discards the pending render, ignores the late result, and starts no render for later requests or selections.
This scenario is tested by `PreviewPaneViewModel_Dispose_CancelsRenderIgnoresResultAndIsRepeatable`.

**PreviewPaneViewModel_RequestRerender_Burst_CoalescesIntoOneRenderAndKeepsPicture**: Three requests leave one
pending callback with the 400 millisecond default delay; running it starts one render while the old picture stays
visible and busy is set; the new picture then replaces it. This scenario is tested by
`PreviewPaneViewModel_RequestRerender_Burst_CoalescesIntoOneRenderAndKeepsPicture`.

**PreviewPaneViewModel_SelectionChangesWhileRerenderPending_DiscardsPendingRender**: Selecting another file discards
the waiting render, and the callback that was already queued does nothing. This scenario is tested by
`PreviewPaneViewModel_SelectionChangesWhileRerenderPending_DiscardsPendingRender`.

**PreviewPaneViewModel_FileChangesOnDisk_WatcherRequestsRerender**: Changing the selected file and then a file in a
sub-folder each make the real watcher request a render of the selected file within a timeout. This scenario is
tested by `PreviewPaneViewModel_FileChangesOnDisk_WatcherRequestsRerender`.

**PreviewPaneViewModel_FileChangesWithoutSelection_RequestsNoRender**: A change with no selected file schedules and
renders nothing. This scenario is tested by `PreviewPaneViewModel_FileChangesWithoutSelection_RequestsNoRender`.

**PreviewPaneViewModel_IsRelevantChange_ProjectPaths_IgnoresDataFolderAndProjectFile**: Root files, sub-folders,
assets, a data-folder-named folder in a sub-folder, and a project file in a sub-folder are relevant; the data folder
and everything inside it, and a project file at the root in any case, are not. This scenario is tested by
`PreviewPaneViewModel_IsRelevantChange_ProjectPaths_IgnoresDataFolderAndProjectFile`.

**PreviewPaneViewModel_IsRelevantChange_PathOutsideProject_IsRelevant**: A path outside the project is relevant and
blank arguments are refused. This scenario is tested by
`PreviewPaneViewModel_IsRelevantChange_PathOutsideProject_IsRelevant`.

**PreviewPaneViewModel_New_NoProject_ShowsPlaceholderAndFitsWindow** and
**PreviewPaneViewModel_New_NullArguments_Throw**: A new pane is idle with the placeholder, fits the window, and has
no notes; missing collaborators are refused. These scenarios are tested by
`PreviewPaneViewModel_New_NoProject_ShowsPlaceholderAndFitsWindow` and `PreviewPaneViewModel_New_NullArguments_Throw`.

**RenderedImageBitmap_Convert_OpaquePixels_SwapsRedAndBlueOnly**,
**RenderedImageBitmap_Convert_SemiTransparentPixel_MultipliesColorByAlpha**,
**RenderedImageBitmap_Convert_TransparentPixel_BecomesZero**,
**RenderedImageBitmap_Convert_WiderDestinationRows_UsesStrideAndKeepsPadding**, and
**RenderedImageBitmap_Convert_InvalidArguments_Throw**: Opaque pixels only change channel order; colors are
multiplied by alpha with rounding (255 at 128 gives 128, 100 at 1 gives 0); a transparent pixel becomes zero; rows are
written at a wider destination row size with the padding untouched; bad sizes and buffers are refused. These
scenarios are tested by the tests of the same names.

**PreviewPaneView_SvgSelected_ShowsBitmapWithConvertedPixels**: With the real render service, selecting a
half-transparent red SVG shows a 40 by 20 bitmap whose first pixel is 0, 0, 128, 128 (premultiplied blue, green, red,
alpha), the scroller, and the status line, and hides the placeholder, error panel, and busy indicator. This scenario
is tested by `PreviewPaneView_SvgSelected_ShowsBitmapWithConvertedPixels`.

**PreviewPaneView_BrokenFile_ShowsErrorCodeMessageAndHint**: A malformed SVG shows the error panel with the code, the
XML message, and the hint, hides the picture, and shows `render failed` in the status. This scenario is tested by
`PreviewPaneView_BrokenFile_ShowsErrorCodeMessageAndHint`.

**PreviewPaneView_RenderRunning_ShowsBusyIndicatorUntilDone**: With a render service that waits, the pane shows the
placeholder while idle, the busy indicator and `Rendering slow.svg...` while the render runs, and neither once it
finishes. This scenario is tested by `PreviewPaneView_RenderRunning_ShowsBusyIndicatorUntilDone`.

**PreviewPaneView_RenderWithNotes_ListsDiagnostics**: An SVG that embeds a file outside the project shows the
expander with the header `1 note` and a list that holds the `image-access-denied` note. This scenario is tested by
`PreviewPaneView_RenderWithNotes_ListsDiagnostics`.

**PreviewPaneView_FitToWindowToggled_ChangesStretchAndScrollBars**: Fitting scales down without scroll bars (the
`Viewbox` stretches uniformly, down only); clearing the check box shows the actual size (no scaling) with automatic
scroll bars; checking it again restores fitting; the `Image` itself is never stretched. This scenario is tested by
`PreviewPaneView_FitToWindowToggled_ChangesStretchAndScrollBars`.

**PreviewPaneViewModel_Overlay_AssignedContent_IsExposedAndAnnounced**: The overlay slot of a new pane is empty;
assigning an object exposes exactly that object and sets `HasOverlay`; clearing it again resets both; each change
raises `PropertyChanged` for `Overlay` and `HasOverlay`. This scenario is tested by
`PreviewPaneViewModel_Overlay_AssignedContent_IsExposedAndAnnounced`.

**PreviewPaneView_FitOff_ImageAtNaturalSize_OverlayCoversImage**: A pane shows an 800 by 400 picture in a window
smaller than the picture, with a transparent border assigned as the overlay, and Fit to window cleared. The overlay
and the picture are each 800 by 400 in their own coordinates, 800 by 400 in the window, and cover the same rectangle.
This scenario is tested by `PreviewPaneView_FitOff_ImageAtNaturalSize_OverlayCoversImage`.

**PreviewPaneView_FitOn_OverlaySizeEqualsImagePixelSize**: With the same picture and overlay fitted to the pane, the
overlay and the picture are still 800 by 400 in their own coordinates, but on screen are smaller than 800 pixels wide,
keep the aspect ratio of 2 to 1, and cover the same rectangle in the window. This scenario is tested by
`PreviewPaneView_FitOn_OverlaySizeEqualsImagePixelSize`.

**PreviewPaneView_OverlaySlot_EmptyThenAssigned_IsHiddenThenShown**: With a picture shown, the overlay host is hidden
while the slot is empty, visible and holding exactly the assigned control after it is assigned, and hidden again after
the slot is cleared. This scenario is tested by `PreviewPaneView_OverlaySlot_EmptyThenAssigned_IsHiddenThenShown`.

**PreviewPaneView_ProjectClosed_ClearsPicture**: Closing the project removes the bitmap and shows the placeholder
`No project is open.`. This scenario is tested by `PreviewPaneView_ProjectClosed_ClearsPicture`.

**RenderedImageBitmap_Create_Picture_HasSizeAndPremultipliedBgraPixels**: A 2 by 1 picture becomes a premultiplied
bitmap of that size with the converted pixels. This scenario is tested by
`RenderedImageBitmap_Create_Picture_HasSizeAndPremultipliedBgraPixels`.

**MainWindowView_SelectSvg_ShowsPreviewAndEnablesImageMenu**: In the main window, selecting an SVG in the explorer
shows a bitmap of its size in the workspace's preview pane and enables the Image menu items; closing the project
disables them. This scenario is tested by `MainWindowView_SelectSvg_ShowsPreviewAndEnablesImageMenu`.
