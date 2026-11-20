### ImageRenderService

![Preview Structure](PreviewSubsystemView.svg)

#### Purpose

`ImageRenderService` draws an image source file with `CanvasNetViewer.Render` on a thread-pool thread, copies the
pixels of the result into a `RenderedImage`, and encodes pictures as PNG with CanvasNet's PNG codec. It is the only
unit that uses the CanvasNet viewer to draw. Every ordinary failure to draw a file becomes a `RenderError`, so no
other unit handles viewer exceptions.

#### Data Model

**RenderedImage**: `SourcePath`, `Format` (for example `svg`), `Width`, `Height`, `RgbaPixels`
(`ReadOnlyMemory<byte>`, exactly `Width * Height * 4` bytes: red, green, blue, alpha with straight alpha, rows from
the top with no padding), and `Diagnostics`. The pixels are a copy, so the object holds nothing that needs
disposal.

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
`FileAccess = FileAccessPolicy.UnderRoots(projectFolder)` and the token as `CancellationToken`, copies the
`Surface` rows with `GetRowSpanBytes` and the notes of `ViewerImage.Diagnostics`, and disposes the viewer image.
CanvasNet's `Surface` stores straight (not premultiplied) red, green, blue, alpha bytes, so the pixels are copied
as they are. The viewer reads the drawn file wherever it lies, and judges only the files it references against the
policy: a denied embedded picture is not drawn and appears as the warning note `image-access-denied`.

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

- **DemaConsulting.CanvasNet.Viewer** (OTS) — `CanvasNetViewer.Render`, `ViewerRenderOptions`, `ViewerException`,
  `FileAccessPolicy.UnderRoots`, `Surface`, and `PngCodec`; see the CanvasNet OTS design.
- **RollingFileLogger** — optional; records files that could not be drawn.

#### Callers

- `PreviewPane` — renders the selected file.
- `ImageExporter` — encodes the shown picture as PNG.
- `ApplicationComposition` — creates the service.
