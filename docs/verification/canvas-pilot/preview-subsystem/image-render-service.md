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
