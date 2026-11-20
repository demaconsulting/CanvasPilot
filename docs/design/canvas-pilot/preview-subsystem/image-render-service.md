### ImageRenderService

![Preview Structure](PreviewSubsystemView.svg)

#### Purpose

`ImageRenderService` draws an image source file with `CanvasNetViewer.Render` on a thread-pool thread, copies the
pixels of the result into a `RenderedImage`, together with the fingerprints that identify the picture and the
positions of its elements, and encodes pictures as PNG with CanvasNet's PNG codec. It is the only
unit that uses the CanvasNet viewer to draw. Every ordinary failure to draw a file becomes a `RenderError`, so no
other unit handles viewer exceptions.

#### Data Model

**RenderedImage**: `SourcePath`, `Format` (for example `svg`), `Width`, `Height`, `RgbaPixels`
(`ReadOnlyMemory<byte>`, exactly `Width * Height * 4` bytes: red, green, blue, alpha with straight alpha, rows from
the top with no padding), and `Diagnostics`. The pixels are a copy, so the object holds nothing that needs
disposal. The picture is also an immutable *render snapshot* that says exactly which picture it is:

- `ContentHash`: the SHA-256 hash, as lower case hexadecimal, of the width and the height (each as four bytes,
  little endian) followed by the pixels, so it fingerprints what the user saw. The constructor computes it, so it
  cannot disagree with the pixels, and it does not depend on the path, the format, or the notes.
- `SourceFileHash`: the SHA-256 hash of the bytes of the source file, or `null` when the file could not be read for
  it (see *Key Methods*).
- `SnapshotKey`: `SourcePath`, a `#`, and `ContentHash`. Pictures with equal keys are the same picture of the same
  file; a later step uses the key to keep marks with the picture they were drawn on.
- `Positions`: `RenderPosition` records for the elements of a Mermaid diagram or Vega-Lite chart, empty for other
  kinds of file, and `PositionsTruncated`, which tells that the library left elements out because it reports at most
  a fixed number of them (1000).
- `Anchors`: `RenderAnchor` records for the anchors an OpenSCAD model declares with `echo("anchor", name, [x, y, z])`,
  empty for other kinds of file.

The lists are copied when the picture is created, so a caller cannot change them afterwards.

**RenderPosition**: `Kind` (`node`, `edge`, `mark`, `axis`, `view`), `Id` (the source's identifier or one derived by
the library, possibly empty), `Text`, `Line` (1-based line of the source file), `From` and `To` (the identifiers an
edge joins), `Box` (`RenderBox`: `X`, `Y`, `Width`, `Height`), and `Pixel` (`RenderPoint`: `X`, `Y`, a point a pointer
can aim at). All coordinates are continuous pixel coordinates of the picture with the origin at the top-left corner,
x to the right and y downward. `Text`, `From`, and `To` are `null` rather than empty when the library gives none.

**RenderAnchor**: `Name`, `PixelX`, and `PixelY` (`null` when the library cannot project the point). The anchors are
valid because the application draws at the natural size; a resized render would move them.

**RenderDiagnostic**: `Severity` (`Info`, `Warning`, `Unsupported`), `Code`, `Message`, and `DisplayText`
(`Severity: Code: Message`). It mirrors CanvasNet's `ImageSourceDiagnostic`.

**RenderError**: `Code`, `Message`, and `Hint` (or `null`). The code is CanvasNet's own for a `ViewerException`
(for example `page-out-of-range`); otherwise one of `RenderErrorCodes`: `input-not-found`, `input-unreadable`,
`unsupported-format`, `cannot-draw`, `size-limit`, and `operation-failed`.

**RenderResult**: exactly one of `Image`, `Error`, or `IsCancelled`, created with `Success`, `Failure`, or
`Cancelled`; `Succeeded` tells whether there is a picture.

**IImageRenderService**: `RenderAsync(filePath, projectFolder, cancellationToken)` and `EncodePng(image)`.

#### Key Methods

**RenderAsync**: Validates the arguments, then runs on the thread pool: throws if the token is already cancelled
(which becomes a cancelled result), calls `CanvasNetViewer.Render(fullPath, options)` with
`FileAccess = FileAccessPolicy.UnderRoots(projectFolder)`, the token as `CancellationToken`, and
`Positions = PositionSelection.All`, hashes the source file, copies the `Surface` rows with `GetRowSpanBytes`, the
notes of `ViewerImage.Diagnostics`, the `Positions` and `Anchors`, and disposes the viewer image.
CanvasNet's `Surface` stores straight (not premultiplied) red, green, blue, alpha bytes, so the pixels are copied
as they are. The viewer reads the drawn file wherever it lies, and judges only the files it references against the
policy: a denied embedded picture is not drawn and appears as the warning note `image-access-denied`.

**Positions request**: Requesting every position costs little and is ignored by every kind of file except Mermaid
and Vega-Lite, so the service always asks; the other kinds produce no positions and no notes (the tests prove this
for SVG and OpenSCAD). OpenSCAD anchors need no request. Embedded pictures never report positions, so a mark on a
picture that a diagram embeds finds no elements.

**Source file hash**: After the viewer has drawn the file, the service hashes the file's bytes with SHA-256 by
streaming it, opened for reading with sharing so that an editor that holds the file does not stop it. The viewer reads
the file itself, so a change between the drawing and the hashing could make the two disagree for one render; the
file watcher of the preview then triggers another render, so the picture is replaced by a consistent one. A file that
has disappeared or become unreadable gives `null` and does not fail the render.

**Error mapping**: A cancellation of the caller's token gives a cancelled result. `ViewerException` keeps its code,
message, and hint. `FileNotFoundException` and `DirectoryNotFoundException` give `input-not-found`;
`NotSupportedException` (no source draws the extension) gives `unsupported-format`; `ImageSourceException` (found
but cannot be drawn, for example malformed XML or a syntax error) gives `cannot-draw`; `ImageSourceBudgetException`
gives `size-limit`; `FileReferenceException`, `IOException`, and `UnauthorizedAccessException` give
`input-unreadable`; anything else gives `operation-failed`. Each failure is logged as a warning.

**EncodePng**: Copies the pixels into a new CanvasNet `Surface` and writes it with `PngCodec.Save` to a memory
stream, so the PNG keeps the straight-alpha transparency.

**Substitute drawing function**: An internal constructor takes the drawing function so tests can produce viewer
failures that cannot be caused on demand with real files.

#### Error Handling

Constructors and methods throw `ArgumentNullException` or `ArgumentException` for missing or blank arguments. The
catch-all in `RenderAsync` is deliberate: the drawing libraries parse arbitrary user files, and whatever fails must
become a message rather than an unhandled exception on a background thread. Failures to write a log entry are
ignored.

#### Dependencies

- **DemaConsulting.CanvasNet.Viewer** (OTS) — `CanvasNetViewer.Render`, `ViewerRenderOptions` (including `Positions`),
  `ViewerImage.Positions` and `Anchors`, `ViewerException`, `FileAccessPolicy.UnderRoots`, `Surface`, and `PngCodec`;
  see the CanvasNet OTS design.
- **RollingFileLogger** — optional; records files that could not be drawn.

#### Callers

- `PreviewPane` — renders the selected file.
- `ImageExporter` — encodes the shown picture as PNG.
- `ApplicationComposition` — creates the service.
