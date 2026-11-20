## CanvasNet

This document describes the integration and usage design for the `CanvasNet` OTS software item.

### Purpose

`DemaConsulting.CanvasNet.Viewer` (with the `DemaConsulting.CanvasNet` core library it depends on) is the DEMA
Consulting library that draws image source files: SVG, Mermaid, PlantUML, Vega-Lite, OpenSCAD, PDF, PPTX, VSDX, and
raster files, with images nested in images. It is the library CanvasPilot exists to drive, so the application uses
it for what it shows and saves instead of drawing files itself. The packages are pinned to one version in the
application project.

### Features Used

- `CanvasNetViewer.Extensions` — the extension list that decides which files the project explorer lists.
- `CanvasNetViewer.Render(path, ViewerRenderOptions)` — draws a file to a `ViewerImage`: a `Surface` of straight-alpha
  red, green, blue, alpha bytes, the format drawn, and the notes (`Diagnostics`).
- `ViewerRenderOptions.FileAccess = FileAccessPolicy.UnderRoots(projectFolder)` — confines the files the drawn file
  reads to the project folder; a denied picture becomes the warning note `image-access-denied`.
- `ViewerRenderOptions.CancellationToken` — stops a render; an `OperationCanceledException` results.
- `ViewerRenderOptions.Positions = PositionSelection.All` — asks a Mermaid diagram or a Vega-Lite chart for the
  position of every element. `ViewerImage.Positions` (`ImagePosition`: `Kind` such as `node`, `edge`, `mark`, `axis`,
  `Id`, `Text`, `Line`, `From`, `To`, `Box`, and `Pixel`, in continuous picture pixels with the origin at the top-left
  corner) and `ViewerImage.PositionsTruncated` (the library reports at most 1000 positions) carry the answer. Every
  other kind of file ignores the request, and a picture that a file embeds never reports positions.
- `ViewerImage.Anchors` (`SourceAnchor`: `Name`, `X`, `Y`, `Z`, `PixelX`, `PixelY`) — the anchors that an OpenSCAD model
  declares with `echo("anchor", name, [x, y, z])`. Their pixel positions are valid only while the render is not
  resized with `Width` or `Height`; the application renders at the natural size.
- `CanvasNetViewer.Render(Stream, fileName, options)` — draws an SVG document held in memory. A later unit uses it to
  draw marks over the picture the user saw: the SVG embeds the picture as a `data:image/png;base64` link and draws
  rectangle, ellipse, polyline, and text elements over it, so no file is written and the picture is exactly the
  pixels that were shown. A render of a stream resolves no file the document names, because it has no folder.
- `ViewerException` (`Code`, `Message`, `Hint`) and the other exceptions of `Render` (`ImageSourceException`,
  `ImageSourceBudgetException`, `FileNotFoundException`, `NotSupportedException`, `FileReferenceException`) — the
  failures the application turns into messages.
- `Surface.GetRowSpanBytes` and `PngCodec.Save` — reading pixels and encoding a PNG.

### Integration Pattern

CanvasNet is consumed as a NuGet package reference of the application library. Only `ImageRenderService` calls
`CanvasNetViewer.Render` and `PngCodec`; `CanvasNetImageSourceFilter` of the project explorer reads
`CanvasNetViewer.Extensions`. The viewer is a static, stateless, thread-safe class, so each render is an
independent call on a thread-pool thread, and the `ViewerImage` is disposed as soon as its pixels and notes are
copied. The application never lets CanvasNet read outside the project folder and never draws on the user interface
thread. CanvasNet has no self-validation mode; the OTS tests in `CanvasNetTests.cs` exercise the calls above
directly and are the evidence that the library behaves as the application relies on, and a version upgrade
re-runs them before it is accepted. The drawing libraries bundled with the viewer are not used separately.
