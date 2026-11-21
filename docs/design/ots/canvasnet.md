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
  in another assembly as new trees; `Filter(include, exclude)` gives a view without the cards that carry given tags (the
  application does not use it, because the tag `hosting` is also on cards that a drawing needs, and leaves cards out by
  identifier instead); `Find`, `GetChildren`, `GetSeeAlso`, `Roots`, and `Cards` read the tree;
  `FormatMarkdown(card, formatLink)` writes the body of a card with the links in the caller's words. `HelpCard` and
  `HelpExample` carry the parts of a card.
- `GenericFontFamilies` (namespace `DemaConsulting.CanvasNet.Fonts`) — `TryClassify(name, includeCommonAliases: true, ...)`
  says whether a font family name is a generic CSS family, a common alias of one (such as `Arial` or `Segoe UI`), or a
  bundled Liberation family, and which bundled design stands in for it; `SplitFamilyList` splits a CSS `font-family` list.
  `StyleKitDocument` classifies the first family of each font list of a style kit with it, so that the warning about a font
  that is not bundled agrees with what the library draws. CanvasNet draws SVG and OpenSCAD text with an installed font when
  the machine has it, and Mermaid, PlantUML, and Vega-Lite text with the bundled fonts only.

### Integration Pattern

CanvasNet is consumed as a NuGet package reference of the application library. `ImageRenderService` calls
`CanvasNetViewer.Render` for files and `PngCodec`; `MarkImageComposer` calls `CanvasNetViewer.Render` only for the SVG
document it builds in memory, and `Surface.Crop` and `PngCodec`; `KnowledgeCatalog` extends `HelpCatalog.Default` with
the cards embedded in the application, leaves out the cards that are only about the host program (by identifier), and
copies the cards into values of its own, once, on first use; `StyleKitDocument` asks `GenericFontFamilies` whether the first
family of a font list is one that CanvasNet draws the same on every machine; `CanvasNetImageSourceFilter` of the project
explorer reads
`CanvasNetViewer.Extensions`. The viewer is a static, stateless, thread-safe class, so each render is an independent
call on a thread-pool thread, and the `ViewerImage` is disposed as soon as its pixels and notes are copied. The
application never lets CanvasNet read outside the project folder and never draws on the user interface thread. CanvasNet
has no self-validation mode; the OTS tests in `CanvasNetTests.cs` exercise the calls above directly and are the evidence
that the library behaves as the application relies on, and a version upgrade re-runs them before it is accepted. The
drawing libraries bundled with the viewer are not used separately.
