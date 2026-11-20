## CanvasNet

This document describes the integration and usage design for the `CanvasNet` OTS software item.

### Purpose

`DemaConsulting.CanvasNet.Viewer` (with the `DemaConsulting.CanvasNet` core library it depends on) is the DEMA
Consulting library that draws image source files: SVG, Mermaid, PlantUML, Vega-Lite, OpenSCAD, PDF, PPTX, VSDX, and
raster files, with images nested in images. It is the library CanvasPilot exists to drive, so the application uses it
for what it shows and saves instead of drawing files itself. The package reference in the application project pins the
viewer library; the library takes its drawing from several packages that are listed as its dependencies and are restored
with it (core, SVG, PDF, PPTX, VSDX, 3D, OpenSCAD, Vega-Lite, Mermaid, PlantUML, and a shared layout package), so the
whole set always moves to one version together.

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
- `CanvasNetViewer.Render(Stream, fileName, options)` — draws an SVG document held in memory. `MarkImageComposer` uses
  it to draw marks over the picture the user saw: the SVG embeds the picture as a `data:image/png;base64` link and draws
  rectangle, ellipse, polyline, circle, and text elements over it, so no file is written and the picture is exactly the
  pixels that were shown. A render of a stream resolves no file the document names, because it has no folder.
- `ViewerException` (`Code`, `Message`, `Hint`) and the other exceptions of `Render` (`ImageSourceException`,
  `ImageSourceBudgetException`, `FileNotFoundException`, `NotSupportedException`, `FileReferenceException`) — the
  failures the application turns into messages.
- `Surface.GetRowSpanBytes` and `PngCodec.Save` — reading pixels and encoding a PNG.
- `Surface.Crop` and `PngCodec.Save` — `MarkImageComposer` cuts the area around a mark from the picture it drew and
  encodes it.
- `HelpCatalog` (namespace `DemaConsulting.CanvasNet.Viewer.Help`) — the help of the library as a tree of cards.
  `HelpCatalog.Default` is the library's own tree; `Extend(assembly, resourcePrefix)` adds the cards that are embedded
  in another assembly as new trees; `Filter(include, exclude)` gives a view without the cards that carry given tags,
  where the visible cards below a hidden card move up (`HelpTags.Hosting` is the tag for what the host program
  controls); `Find`, `GetChildren`, `GetSeeAlso`, `Roots`, and `Cards` read the tree; `FormatMarkdown(card, formatLink)`
  writes the body of a card with the links in the caller's words. `HelpCard` and `HelpExample` carry the parts of a
  card. A filtered view cannot be extended, so the application extends first and filters second.

### Integration Pattern

CanvasNet is consumed as a NuGet package reference of the application library. `ImageRenderService` calls
`CanvasNetViewer.Render` for files and `PngCodec`; `MarkImageComposer` calls `CanvasNetViewer.Render` only for the SVG
document it builds in memory, and `Surface.Crop` and `PngCodec`; `KnowledgeCatalog` extends `HelpCatalog.Default` with
the cards embedded in the application, filters out the cards of the tag `hosting`, and copies the cards into values of
its own, once, on first use; `CanvasNetImageSourceFilter` of the project explorer reads `CanvasNetViewer.Extensions`.
The viewer is a static, stateless, thread-safe class, so each render is an independent call on a thread-pool thread, and
the `ViewerImage` is disposed as soon as its pixels and notes are copied. The application never lets CanvasNet read
outside the project folder and never draws on the user interface thread. CanvasNet has no self-validation mode; the OTS
tests in `CanvasNetTests.cs` exercise the calls above directly and are the evidence that the library behaves as the
application relies on, and a version upgrade re-runs them before it is accepted. The drawing libraries bundled with the
viewer are not used separately.
