## CanvasNet Verification

This document provides the verification evidence for the CanvasNet OTS software item. Requirements for this OTS item
are defined in the CanvasNet OTS Software Requirements document.

### Required Functionality

CanvasNet (`DemaConsulting.CanvasNet.Viewer`, with the `DemaConsulting.CanvasNet` core library it depends on) draws
the image source files of a project to pixels, lists the file types it can draw, confines the files a drawn file
may read, reports problems with files as descriptive exceptions, honors cancellation, encodes PNG, reports where it
drew the elements of diagrams, charts, and models, and draws an SVG document held in memory. The application relies
on each of these behaviors, so each has a requirement and a test.

### Verification Approach

CanvasNet has no self-validation mode. It is verified by integration tests in `test/OtsSoftwareTests/CanvasNetTests.cs`
that call the library directly, on temporary folders, with the same calls and options the application uses
(`CanvasNetViewer.Render` with `FileAccessPolicy.UnderRoots` and `PositionSelection.All`, `CanvasNetViewer.Extensions`,
`CanvasNetViewer.Render` of a stream, and `PngCodec.Save`).
The tests are the evidence that the library behaves as the application relies on, and they are run again before a
version upgrade is accepted. The unit tests of the units that use the library repeat part of this evidence through
the application's own calls.

### Test Scenarios

#### CanvasNet_Render_SvgFile_ProducesStraightAlphaPixels

**Scenario**: A 40 by 20 SVG with a half-transparent red rectangle is rendered with file access confined to the
project folder.

**Expected**: The surface is 40 by 20, the format is `svg`, each row has 160 bytes, the first pixel is 255, 0, 0, 128
(straight alpha), and there are no notes.

**Requirement coverage**: `CanvasPilot-OTS-CanvasNet-RenderImageSources`.

#### CanvasNet_Extensions_ListsSupportedImageSourceTypes

**Scenario**: The extension list of the viewer is read.

**Expected**: It contains `.svg`, `.png`, `.jpg`, `.pdf`, `.mmd`, `.scad`, `.puml`, and the compound `.vl.json`, and
every entry is lower case and starts with a period.

**Requirement coverage**: `CanvasPilot-OTS-CanvasNet-ListSupportedTypes`.

#### CanvasNet_Render_UnderRootsPolicy_DeniesEmbeddedFileOutsideRoots

**Scenario**: An SVG in the project embeds a file that lies beside the project folder, and the render is confined to
the project folder.

**Expected**: The render succeeds, and its one note is a warning with the code `image-access-denied`.

**Requirement coverage**: `CanvasPilot-OTS-CanvasNet-ConfineFileAccess`.

#### CanvasNet_Render_UnderRootsPolicy_AllowsEmbeddedFileInsideRoots

**Scenario**: An SVG embeds another SVG from a sub-folder of the project, under the same confinement.

**Expected**: There are no notes and the embedded picture is drawn.

**Requirement coverage**: `CanvasPilot-OTS-CanvasNet-ConfineFileAccess`.

#### CanvasNet_Render_PageBeyondLastPage_ThrowsViewerExceptionWithCodeAndHint

**Scenario**: Page 2 of a single-page SVG is requested.

**Expected**: A viewer exception with the code `page-out-of-range`, a message, and a hint is thrown.

**Requirement coverage**: `CanvasPilot-OTS-CanvasNet-ReportFailures`.

#### CanvasNet_Render_UnusableFiles_ThrowDescriptiveExceptions

**Scenario**: A malformed SVG, a missing file, and a text file are rendered.

**Expected**: An image source exception whose message names the malformed XML, a file-not-found exception, and a
not-supported exception whose message lists the supported extensions.

**Requirement coverage**: `CanvasPilot-OTS-CanvasNet-ReportFailures`.

#### CanvasNet_Render_CancelledToken_ThrowsOperationCanceled

**Scenario**: A valid SVG is rendered with a cancellation token that is already cancelled.

**Expected**: An operation-cancelled exception is thrown.

**Requirement coverage**: `CanvasPilot-OTS-CanvasNet-HonorCancellation`.

#### CanvasNet_PngCodec_SaveSurface_WritesValidPngThatRoundTrips

**Scenario**: A 3 by 2 surface filled with a half-transparent color is saved as PNG to memory and loaded again.

**Expected**: The bytes start with the PNG signature, and the decoded surface has the same size and the same pixel.

**Requirement coverage**: `CanvasPilot-OTS-CanvasNet-EncodePng`.

#### CanvasNet_Render_MermaidWithPositionsSelection_ReportsNodesWithBoxes

**Scenario**: A flowchart with three nodes and two edges is rendered once with every position requested and once with
none.

**Expected**: Five positions: the nodes `A`, `B`, and `C` with the text `Start`, `Middle`, and `End`, the first on
source line 2, each with a box of positive size inside the picture and a pixel, and two edges joining `A` to `B` and `B`
to `C`; no truncation; and no positions when none were requested.

**Requirement coverage**: `CanvasPilot-OTS-CanvasNet-ReportElementPositions`.

#### CanvasNet_Render_VegaLiteWithPositionsSelection_ReportsMarksAndAxes

**Scenario**: A bar chart with two bars is rendered with every position requested.

**Expected**: Two marks and two axes are reported and every position has a box.

**Requirement coverage**: `CanvasPilot-OTS-CanvasNet-ReportElementPositions`.

#### CanvasNet_Render_ScadAnchors_ReportsPixelPositions

**Scenario**: A model that declares the anchor `top` at 5, 5, 10 is rendered with every position requested.

**Expected**: One anchor named `top` with the world position 5, 5, 10 and a pixel position inside the picture, and no
element positions.

**Requirement coverage**: `CanvasPilot-OTS-CanvasNet-ReportElementPositions`.

#### CanvasNet_Render_SvgStream_WithDataUriImageAndShapes_DrawsAll

**Scenario**: A 100 by 50 red PNG is embedded as a data link in an SVG document in a stream that also draws a blue
rectangle outline, a green ellipse outline, a yellow polyline, and white text over it; the stream is rendered.

**Expected**: The picture is 100 by 50 with format `svg`; a pixel away from the shapes is red; a pixel on each of the
rectangle, ellipse, and polyline strokes has that stroke's color; some pixel in the text's region is not red; and
there are no notes.

**Requirement coverage**: `CanvasPilot-OTS-CanvasNet-ComposeSvgFromStream`.
