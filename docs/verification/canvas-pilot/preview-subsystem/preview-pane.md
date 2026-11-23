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

Unit tests do not need a display. UI tests run under the Avalonia headless platform with the Skia
renderer, serialized with test parallelization disabled; no display or windowing system is required. Tests use
temporary local folders and real file I/O; no network or external services are required.

**Checks by hand before a release** (the headless platform cannot deliver real wheel, key, and middle-button input; they
are not evidence for a requirement):

1. *Zoom with the wheel and the keys.* With a picture larger than the pane shown, `Ctrl` with the wheel zooms in and out
   in steps, the wheel alone scrolls, and `Ctrl+0`, `Ctrl+1`, `Ctrl+Plus`, and `Ctrl+Minus` fit, show the actual size,
   and zoom, after a click on the empty stage has given the pane the focus.
2. *Panning.* At a zoom where the picture is larger than the pane, dragging with the middle button moves the picture and
   the scroll bars follow.
3. *Looks.* In the light and the dark theme, the stage shows its dot grid, the picture has a margin and a soft shadow,
   the zoom pill sits at the bottom right, the busy line runs along the top of the stage while a large file renders, the
   error card is centered with a red outline, and an open drawer of notes at a stage narrower than about 700 pixels may
   cover the zoom pill (a known limit).
4. *Names.* With a screen reader, the zoom buttons are read as Fit to window, Actual size, Zoom out, Zoom in, and the
   zoom label, each with its tooltip.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- Selecting a file renders it in the project folder, shows busy, then the picture, its status sentence, and its notes;
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
  picture's size and pixels.
- The pane tells the file name, and for a shown picture its size, format in capital letters, and render time, tells none
  of the picture's facts before a render ends or after one fails, and sums its state up in one sentence.
- The placeholder says that no project is open, or that a file is to be selected, while there is nothing else to show,
  and is hidden while a picture, an error, or a running render is shown.
- The zoom steps are 25, 50, 75, 100, 150, 200, 300, and 400 percent; zooming in or out from the fitted picture goes to
  the next step above or below the percent at which it is shown; the zoom never leaves 25 to 400 and the commands for a
  larger or smaller zoom are off at its ends; the actual size command gives 100 percent; fitting again shows the label
  of the fitted zoom; the zoom last chosen stays while Fit is on.
- Fit scales the picture down to the pane without scroll bars and never enlarges it; with Fit off the picture is at its
  own size with automatic scroll bars. The zoom pill zooms the picture and the overlay together: the Fit toggle, the
  actual size button, the zoom out button, and the zoom label change the zoom and the label as they should, the label
  fits the picture again, and the picture on screen has the size of the picture times the zoom.
- The busy line is 2 pixels high at the top of the stage while a render runs, and hidden otherwise; the error card shows
  the code, message, and hint; the notes drawer lies inside the stage, shows the header `1 note` and the notes when
  open.
- The overlay slot is empty and hidden by default, shows the content assigned to it exactly over the picture, has the
  picture's pixel size in its own coordinates at actual size, when zoomed, and when the picture is scaled down to the
  pane, and is scaled on screen by the same factor as the picture.
- The slot that floats over the stage is empty and hidden by default, shows the content assigned to it at its own size
  although the picture is scaled down, and is not inside the scaled picture.
- A press of the left button on the shown picture followed by a move of at least four pixels starts one drag with the
  copy effect that offers a PNG file and a bitmap of the picture's size; a click, a move of three pixels, a drag while
  dragging out is off, and a drag from a pane without a drag source start nothing; a drag operation that fails is
  logged and the next drag starts normally.
- The pane gives a file for dragging only while a picture is shown, dragging out is on, and it has a drag source.
- Notes that a part gives for the shown picture replace, in place, the earlier notes of the same group, keep the notes
  of the renderer and of other groups in their order, open the list, are ignored while no picture is shown, and are gone
  when the picture is drawn again; a blank group and a missing list are refused.

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
**PreviewPaneViewModel_New_NullArguments_Throw**: A new pane is idle with the placeholder `No project is open.`, fits
the window, and has no notes; missing collaborators are refused. These scenarios are tested by
`PreviewPaneViewModel_New_NoProject_ShowsPlaceholderAndFitsWindow` and `PreviewPaneViewModel_New_NullArguments_Throw`.

**PreviewPaneViewModel_Render_ReportsFileSizeFormatAndRenderTimeOfTheShownPicture**: Before a file is selected the file
name, size, format, and render time are empty. Selecting `a.svg` gives the name and no other fact while the render runs;
after a 900 by 500 SVG is shown the facts are `a.svg`, `900 × 500 px`, `SVG`, and `Rendered in N ms`; after another file
fails to draw only its name is left and the render time is `null`. This scenario is tested by
`PreviewPaneViewModel_Render_ReportsFileSizeFormatAndRenderTimeOfTheShownPicture`.

**PreviewPaneViewModel_Zoom_StepsClampAndFit_FollowTheStepList**: A fitted pane that the view reports as shown at 90
percent has the label `90%`. Zooming in gives 100 percent and turns Fit off; ten more steps stop at 400 with zooming in
off and zooming out on; ten steps out stop at 25 with zooming out off and zooming in on; the actual size command gives
100 percent and `IsActualSize`; fitting again turns Fit on, is not the actual size, and the label is `90%` again. This
scenario is tested by `PreviewPaneViewModel_Zoom_StepsClampAndFit_FollowTheStepList`.

**PreviewPaneViewModel_ZoomOut_FromFit_GoesToTheStepBelowTheFittedZoom**: Zooming out of a fitted picture shown at 90
percent gives 75 percent and turns Fit off. This scenario is tested by
`PreviewPaneViewModel_ZoomOut_FromFit_GoesToTheStepBelowTheFittedZoom`.

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
alpha), the scroller, and the status sentence `red.svg: 40 x 20 px, svg`, and hides the placeholder, error panel, and
busy indicator. This scenario is tested by `PreviewPaneView_SvgSelected_ShowsBitmapWithConvertedPixels`.

**PreviewPaneView_BrokenFile_ShowsErrorCodeMessageAndHint**: A malformed SVG shows the error card with the code, the
XML message, and the hint, hides the picture, and shows `render failed` in the status sentence. This scenario is tested
by `PreviewPaneView_BrokenFile_ShowsErrorCodeMessageAndHint`.

**PreviewPaneView_RenderRunning_ShowsBusyIndicatorUntilDone**: With a render service that waits, the pane shows the
placeholder while idle, the busy line and `Rendering slow.svg...` while the render runs, and neither once it finishes.
While it runs the line is 2 pixels high and its top is the top of the stage. This scenario is tested by
`PreviewPaneView_RenderRunning_ShowsBusyIndicatorUntilDone`.

**PreviewPaneView_RenderWithNotes_ListsDiagnostics**: An SVG that embeds a file outside the project shows the drawer
hidden before the render; afterwards it is shown with the header `1 note`, and, when opened, lies entirely inside the
stage and holds the `image-access-denied` note. This scenario is tested by
`PreviewPaneView_RenderWithNotes_ListsDiagnostics`.

**PreviewPaneView_DiagnosticsDrawerOnNarrowStage_DoesNotOverlapTheZoomPill**: In a pane 400 pixels wide that shows a
picture with a note, both the drawer and the zoom pill are shown, and the drawer, closed and open, lies above the pill
and does not cover it. This scenario is tested by
`PreviewPaneView_DiagnosticsDrawerOnNarrowStage_DoesNotOverlapTheZoomPill`.

**PreviewPaneView_FitToggle_ShowsItsOnStateWithTintOutlineAndAccentIcon**: In the dark theme the Fit toggle, which is on
when a picture opens, has the accent tint as its fill, a one pixel outline and an icon in the accent text color; after
it is turned off it has no fill, no outline, and the icon of the other buttons. This scenario is tested by
`PreviewPaneView_FitToggle_ShowsItsOnStateWithTintOutlineAndAccentIcon`.

**PreviewPaneView_FitToWindowToggled_ChangesStretchAndScrollBars**: Fitting scales down without scroll bars (the
`Viewbox` stretches uniformly, down only); clearing the Fit toggle shows the actual size (no scaling) with automatic
scroll bars and `FitToWindow` off; checking it again restores fitting; the `Image` itself is never stretched. This
scenario is tested by `PreviewPaneView_FitToWindowToggled_ChangesStretchAndScrollBars`.

**PreviewPaneView_ZoomPill_ZoomsPictureAndOverlayTogether**: A pane shows an 800 by 400 picture fitted to the pane, with
an overlay; the label ends with `%`. The actual size button gives the label `100%`, a picture 800 wide on screen, and
Fit off. Two presses of the zoom out button give `50%`, a picture of 400 by 200 on screen, and an overlay that covers
exactly the same rectangle and is still 800 by 400 in its own coordinates. A press of the label fits the picture again
and gives the fitted label. This scenario is tested by `PreviewPaneView_ZoomPill_ZoomsPictureAndOverlayTogether`.

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

**PreviewPaneViewModel_Toolbar_AssignedContent_IsExposedAndAnnounced**: The slot that floats over the stage in a new
pane is empty; assigning an object exposes exactly that object and sets `HasToolbar`; clearing it resets both; each
change raises `PropertyChanged` for `Toolbar` and `HasToolbar`, and the overlay slot is not affected. This scenario is
tested by `PreviewPaneViewModel_Toolbar_AssignedContent_IsExposedAndAnnounced`.

**PreviewPaneView_ToolbarSlot_EmptyThenAssigned_IsHiddenThenShownOutsideThePicture**: With a large picture shown, scaled
down to the pane, the host of the floating slot is hidden while the slot is empty; after a control of 100 by 20 is
assigned the host is visible, holds exactly that control, the control measures 100 by 20 on screen, and it is not a
descendant of the scroll viewer that holds the picture; after the slot is cleared the host is hidden again. This
scenario is tested by `PreviewPaneView_ToolbarSlot_EmptyThenAssigned_IsHiddenThenShownOutsideThePicture`.

**PreviewPaneViewModel_PrepareDragFile_PictureShown_WritesPngOnlyWhenAllowed**: A pane with a drag source gives no file
before a picture is shown, gives the PNG file named after the picture once one is shown, gives none while dragging out
is off and the same file when it is on again, and a pane without a drag source gives none. This scenario is tested by
`PreviewPaneViewModel_PrepareDragFile_PictureShown_WritesPngOnlyWhenAllowed`.

**PreviewPaneViewModel_ReportDragProblem_Failure_IsLogged**: A failure reported by the view is written to the log with
the text of the failure; a missing failure is refused. This scenario is tested by
`PreviewPaneViewModel_ReportDragProblem_Failure_IsLogged`.

**PreviewPaneView_DragFromImage_PictureShown_OffersPngFileAndBitmap**: With a 40 by 20 picture shown, pressing the left
button on it and moving 10 pixels starts one drag operation, with the copy effect, whose data offers the file and the
bitmap formats, a file named `red-` and the fingerprint start with the extension `.png` that starts with the PNG
signature, and a bitmap of 40 by 20 pixels; releasing and moving again starts no further drag. The view's drag
operation is replaced by a recording one, because the system's never completes under the headless platform. This
scenario is tested by `PreviewPaneView_DragFromImage_PictureShown_OffersPngFileAndBitmap`.

**PreviewPaneView_DragFromImage_ClickOrNotAllowed_StartsNothing**: A click, a press followed by a move of three pixels
in each direction, and a drag while dragging out is turned off start nothing; neither does a drag from a pane that has
no drag source. This scenario is tested by `PreviewPaneView_DragFromImage_ClickOrNotAllowed_StartsNothing`.

**PreviewPaneView_DragFromImage_DragOperationFails_LogsAndKeepsWorking**: When the drag operation fails the first time,
the log holds the problem and its text, and after the release a second drag starts normally. This scenario is tested by
`PreviewPaneView_DragFromImage_DragOperationFails_LogsAndKeepsWorking`.

**PreviewPaneView_ProjectClosed_ClearsPicture**: Closing the project removes the bitmap and shows the placeholder
`No project is open.`. This scenario is tested by `PreviewPaneView_ProjectClosed_ClearsPicture`.

**RenderedImageBitmap_Create_Picture_HasSizeAndPremultipliedBgraPixels**: A 2 by 1 picture becomes a premultiplied
bitmap of that size with the converted pixels. This scenario is tested by
`RenderedImageBitmap_Create_Picture_HasSizeAndPremultipliedBgraPixels`.

**MainWindowView_SelectSvg_ShowsPreviewAndEnablesImageMenu**: In the main window, selecting an SVG in the explorer
shows a bitmap of its size in the workspace's preview pane and enables the Image menu items; closing the project
disables them. This scenario is tested by `MainWindowView_SelectSvg_ShowsPreviewAndEnablesImageMenu`.

**PreviewPaneViewModel_ProjectFolder_FollowsTheExplorer**: The pane, seen as the source of the shown picture, has no
project folder before a project is opened, the folder of the project while it is open, and none after it is closed. This
scenario is tested by `PreviewPaneViewModel_ProjectFolder_FollowsTheExplorer`.

**PreviewPaneViewModel_ShowNotes_ReplacesTheGroupKeepsOthersAndGoesWithTheNextRender**: A picture is shown with a note
of the renderer. Notes of the group `style` are given, notes of another group, and new notes of `style`: the list is the
note of the renderer, the other group's note, and the newest `style` note, in that order, with the header `3 notes`;
giving no notes removes the `style` notes; after another picture is drawn the list is empty; the list was closed at
first and open after the first notes. This scenario is tested by
`PreviewPaneViewModel_ShowNotes_ReplacesTheGroupKeepsOthersAndGoesWithTheNextRender`.

**PreviewPaneViewModel_ShowNotes_NoPictureOrBadArguments_IgnoresOrRefuses**: With no picture shown, notes are ignored
and the list stays closed; a blank group is refused with `ArgumentException` and a missing list with
`ArgumentNullException`. This scenario is tested by
`PreviewPaneViewModel_ShowNotes_NoPictureOrBadArguments_IgnoresOrRefuses`.
