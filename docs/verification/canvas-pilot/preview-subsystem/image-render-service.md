### ImageRenderService

#### Verification Approach

`ImageRenderService` is verified by unit tests in `ImageRenderServiceTests.cs` and `RenderResultTests.cs`. Most tests
call the real service, and so the real CanvasNet viewer and PNG codec, on SVG and text files in temporary project
folders; the real library is cheap and gives true evidence of how the unit uses it. Failures that cannot be caused on
demand with real files (a viewer error with a code and hint, an I/O failure, an unexpected exception, and a render
that waits for cancellation) use the unit's internal constructor, which takes a substitute drawing function. The
tests await the returned tasks, so background execution is exercised without sleeping.

#### Test Environment

Standard test environment with temporary local folders and real file I/O; no network, external services, or display.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A drawn SVG yields its size, format, and straight-alpha RGBA pixels in rows with no padding.
- A drawn file carries a SHA-256 fingerprint of its bytes and one of its size and pixels: the same file gives the same
  fingerprints, a changed file different ones, and a file that vanished after it was drawn gives a picture without a
  file fingerprint.
- A Mermaid diagram reports its nodes and edges, a Vega-Lite chart its marks and axes, with identifier, text, source
  line, and a box inside the picture; an OpenSCAD model reports its anchors with pixel positions; an SVG reports
  nothing and no note is added; the viewer is asked for every position.
- An embedded picture outside the project folder is denied with a warning note while the render succeeds, and an
  embedded picture in a project sub-folder is drawn without notes.
- A malformed file, a missing file, and an unsupported type each give an error result with the right code and no
  exception; a viewer error keeps its code, message, and hint; I/O failures and unexpected failures give error
  results; failures are logged.
- A cancelled token, before or during the render, gives a cancelled result with no picture or error.
- An encoded PNG has the signature and decodes to the same size and pixels, including transparency.
- Invalid arguments are refused.

#### Test Scenarios

**ImageRenderService_RenderAsync_SvgFile_ReturnsPixelsSizeAndFormat**: A half-transparent red 40 by 20 SVG is
rendered. The result succeeded, with the file's path, format `svg`, 40 by 20 pixels, 3200 bytes, the first pixel
255, 0, 0, 128, and no notes. This scenario is tested by
`ImageRenderService_RenderAsync_SvgFile_ReturnsPixelsSizeAndFormat`.

**ImageRenderService_RenderAsync_EmbeddedPictureOutsideProject_ReportsAccessDeniedNote**: An SVG embeds a file that
lies beside the project folder. The render succeeds and the one note is a warning with the code
`image-access-denied` naming the file. This scenario is tested by
`ImageRenderService_RenderAsync_EmbeddedPictureOutsideProject_ReportsAccessDeniedNote`.

**ImageRenderService_RenderAsync_EmbeddedPictureInProjectSubFolder_IsDrawnWithoutNotes**: An SVG embeds another SVG
from a sub-folder of the project. The embedded picture is drawn (the first pixel is red) and there are no notes. This
scenario is tested by `ImageRenderService_RenderAsync_EmbeddedPictureInProjectSubFolder_IsDrawnWithoutNotes`.

**ImageRenderService_RenderAsync_BrokenSvg_ReturnsCannotDrawErrorWithoutThrowing**: A malformed SVG gives the
error code `cannot-draw`, a message that mentions the XML problem, and a hint, without an exception. This scenario is
tested by `ImageRenderService_RenderAsync_BrokenSvg_ReturnsCannotDrawErrorWithoutThrowing`.

**ImageRenderService_RenderAsync_MissingFile_ReturnsInputNotFoundError**: A missing file gives `input-not-found`.
This scenario is tested by `ImageRenderService_RenderAsync_MissingFile_ReturnsInputNotFoundError`.

**ImageRenderService_RenderAsync_UnsupportedExtension_ReturnsUnsupportedFormatError**: A text file gives
`unsupported-format`. This scenario is tested by
`ImageRenderService_RenderAsync_UnsupportedExtension_ReturnsUnsupportedFormatError`.

**ImageRenderService_RenderAsync_ViewerException_ReturnsItsCodeAndHint**: A drawing function that throws a viewer
error with the code `page-out-of-range`, a message, and a hint gives an error with exactly that code, message, and
hint. This scenario is tested by `ImageRenderService_RenderAsync_ViewerException_ReturnsItsCodeAndHint`.

**ImageRenderService_RenderAsync_OtherFailures_AreReturnedAsErrors**: I/O errors, access errors, an unsupported
operation, a file-not-found error, and an unexpected exception type give `input-unreadable`, `input-unreadable`,
`unsupported-format`, `input-not-found`, and `operation-failed`, with the exception's message. This scenario is
tested by `ImageRenderService_RenderAsync_OtherFailures_AreReturnedAsErrors`.

**ImageRenderService_RenderAsync_AlreadyCancelledToken_ReturnsCancelledWithoutDrawing**: With a token that is
already cancelled the result is cancelled, has no picture or error, and the drawing function was not called. This
scenario is tested by `ImageRenderService_RenderAsync_AlreadyCancelledToken_ReturnsCancelledWithoutDrawing`.

**ImageRenderService_RenderAsync_CancelledWhileRendering_ReturnsCancelled**: A drawing function waits for its token
(the one the service passes in the viewer options); the test cancels after the function started, and the result is
cancelled. This scenario is tested by `ImageRenderService_RenderAsync_CancelledWhileRendering_ReturnsCancelled`.

**ImageRenderService_RenderAsync_Failure_IsLogged**: A failed render of a malformed file writes an entry that names
the file and the error code. This scenario is tested by `ImageRenderService_RenderAsync_Failure_IsLogged`.

**ImageRenderService_EncodePng_Image_ProducesValidPngWithSamePixels**: A 3 by 2 picture with an opaque and a
half-transparent pixel is encoded and decoded with the PNG codec; the signature, size, and every pixel match. This
scenario is tested by `ImageRenderService_EncodePng_Image_ProducesValidPngWithSamePixels`.

**ImageRenderService_InvalidArguments_Throw**: A missing drawing function or picture, and a blank file or folder,
are refused. This scenario is tested by `ImageRenderService_InvalidArguments_Throw`.

**RenderedImage_New_ValidArguments_KeepsValues** and **RenderedImage_New_InvalidArguments_Throw**: A picture keeps
its path, format, size, and pixels and defaults to no notes; blank paths, empty sizes, and pixel data of the wrong
length are refused. These scenarios are tested by `RenderedImage_New_ValidArguments_KeepsValues` and
`RenderedImage_New_InvalidArguments_Throw`.

**RenderResult_Factories_ReportExactlyOneOutcome**: A success, a failure, and a cancelled result each report only
their own outcome, and missing arguments are refused. This scenario is tested by
`RenderResult_Factories_ReportExactlyOneOutcome`.

**RenderDiagnostic_DisplayText_ShowsSeverityCodeAndMessage**: A note displays as `Warning: code: message`. This
scenario is tested by `RenderDiagnostic_DisplayText_ShowsSeverityCodeAndMessage`.

**ImageRenderService_RenderAsync_MermaidFile_ReportsNodePositionsInPixels**: A three-node, two-edge flowchart is
rendered. The format is `mermaid`, there are no notes and no truncation, the nodes `A`, `B`, and `C` have the text
`Start`, `Middle`, and `End`, the first is declared on line 2, each has a box with a positive size inside the
picture and a pixel, the nodes run left to right, and the edges join `A` to `B` and `B` to `C`. This scenario is
tested by `ImageRenderService_RenderAsync_MermaidFile_ReportsNodePositionsInPixels`.

**ImageRenderService_RenderAsync_VegaLiteChart_ReportsMarkPositions**: A bar chart with two bars is rendered. It
reports two marks and two axes and every position has a box. This scenario is tested by
`ImageRenderService_RenderAsync_VegaLiteChart_ReportsMarkPositions`.

**ImageRenderService_RenderAsync_ScadWithAnchor_ReportsAnchor**: A model that declares the anchor `top` is rendered.
One anchor is reported, named `top`, with a pixel position inside the picture, and there are no element positions.
This scenario is tested by `ImageRenderService_RenderAsync_ScadWithAnchor_ReportsAnchor`.

**ImageRenderService_RenderAsync_SvgFile_ReportsNoPositions**: An SVG is rendered. It has no positions, no anchors, no
truncation, and no notes. This scenario is tested by `ImageRenderService_RenderAsync_SvgFile_ReportsNoPositions`.

**ImageRenderService_RenderAsync_Request_AsksForAllPositions**: A drawing function records the options it receives.
They ask for every position. This scenario is tested by `ImageRenderService_RenderAsync_Request_AsksForAllPositions`.

**ImageRenderService_RenderAsync_SameFileTwice_SameContentHash**: The same unchanged file is rendered twice. Both
pictures have the same pixel fingerprint, file fingerprint, and snapshot key, and the file fingerprint is the SHA-256
hash of the file's bytes. This scenario is tested by `ImageRenderService_RenderAsync_SameFileTwice_SameContentHash`.

**ImageRenderService_RenderAsync_ChangedFile_DifferentSourceAndContentHash**: A file is rendered, its color is
changed, and it is rendered again. The file fingerprint, the pixel fingerprint, and the snapshot key all differ. This
scenario is tested by `ImageRenderService_RenderAsync_ChangedFile_DifferentSourceAndContentHash`.

**ImageRenderService_RenderAsync_FileGoneAfterDrawing_HasNoSourceFileHash**: A drawing function draws the real file
and then deletes it. The result is a picture with a pixel fingerprint and no file fingerprint. This scenario is
tested by `ImageRenderService_RenderAsync_FileGoneAfterDrawing_HasNoSourceFileHash`.

**RenderedImage_SnapshotKey_CombinesPathAndContentHash**: Pictures that are equal, that differ in path, and that
differ in pixels are compared. The key holds the path and the pixel fingerprint, equal pictures share it, and the
others differ. This scenario is tested by `RenderedImage_SnapshotKey_CombinesPathAndContentHash`.

**RenderedImage_ContentHash_DependsOnPixelsAndSizeOnly**: The same bytes as a 2 by 1 and a 1 by 2 picture, and a 2 by 1
picture with a note and another path, are compared. The fingerprint is 64 lower case hexadecimal characters, differs
with the size, and does not differ with the notes or the path. This scenario is tested by
`RenderedImage_ContentHash_DependsOnPixelsAndSizeOnly`.

**RenderedImage_New_PositionsAndAnchors_AreCopiedAndDefaultToEmpty**: A picture without element data has empty lists
and no file fingerprint; a picture built from lists that are cleared afterwards still holds its positions, anchors,
truncation flag, and file fingerprint. This scenario is tested by
`RenderedImage_New_PositionsAndAnchors_AreCopiedAndDefaultToEmpty`.
