### MarkImageComposer

![Payload Structure](PayloadSubsystemView.svg)

#### Purpose

`MarkImageComposer` makes the pictures that show where the user pointed: the rendered picture with the marks drawn and
numbered on it, and a cut-out of the area around each mark. It does this with CanvasNet, without a window and without
writing a file, so the pictures are made on a background thread and are exactly the pixels the user saw, plus the marks.

#### Data Model

- `IMarkImageComposer`: the seam, with one method `Compose`.
- `MarkedPictureImages(AnnotatedPng, Crops)` and `MarkCrop(Number, Png, Box)`: the result, with the bytes of PNG files
  and the area of each cut-out in picture pixels.
- Constants: a margin of 16 pixels (`CropPadding`) and a minimum of 64 pixels (`MinimumCropSize`) for the cut-outs.
- Derived sizes: the line width of a mark is the smaller side of the picture divided by 80, rounded, from 3 to 8 pixels;
  the white line under it is 3 pixels wider; the radius of the badge is 3.5 percent of the smaller side, rounded, from 9
  to 20 pixels. The six colors are `#e53935`, `#1e88e5`, `#43a047`, `#fb8c00`, `#8e24aa`, and `#00acc1`, used by
  `(number - 1) mod 6`.
- The unit holds one reference, to the render service that encodes the picture.

#### Key Methods

**Compose(picture, marks)**: refuses a mark whose picture has another key than the picture; returns the PNG of the
picture itself and no cut-outs when there are no marks; otherwise builds the SVG document, has CanvasNet draw it, checks
the result, encodes it, and cuts the areas.

1. *Document.* The document is as large as the picture in pixels. It holds an `image` element with the PNG of the
   picture as a `data:image/png;base64` link, then, for each mark in the order of the numbers, the white line and the
   colored line of its shape, and last the badges. A rectangle is a `rect`, an ellipse an `ellipse` inscribed in the
   bounding box, a freehand mark a `polyline`, and an arrow a polyline for the shaft and two polylines for the head, as
   long as five line widths (at least 12 pixels, at most the length of the arrow) at 28 degrees to the shaft. The size
   of the head comes from the width of the colored line, so the white line under it has exactly the same shape. A badge
   is a `circle` in the color of the mark with a white edge of 2 pixels and the number as `text` in white, scaled to the
   number of digits and centered with the average width of a digit, so that no font feature that CanvasNet may lack is
   needed. Its center is the top-left corner of the bounding box, moved so that the whole badge is inside the picture.
2. *Drawing.* `CanvasNetViewer.Render` draws the document from a stream. A problem that CanvasNet reports in its notes,
   or a surface of another size than the picture, stops the build with `InvalidOperationException`: a picture without
   the picture the user saw under the marks must never be sent.
3. *Cut-outs.* The area of a mark is its bounding box and its badge with a margin of 16 pixels, grown around its center
   to at least 64 pixels in each direction, limited to the size of the picture, and moved inside it. The area is cut
   from the annotated surface with `Surface.Crop` and encoded, so the other marks that fall in the area are drawn in it.

#### Error Handling

Missing arguments throw `ArgumentNullException`; a mark of another picture throws `ArgumentException`. A failure of
CanvasNet to draw the document, a note that it reports, and a result of another size are reported as
`InvalidOperationException` with the cause as the inner exception where there is one. Nothing is logged here; the caller
reports the failure to the user and keeps the draft.

#### Dependencies

- **CanvasNet** (OTS): draws the SVG document from a stream (`CanvasNetViewer.Render`), copies a region
  (`Surface.Crop`), and encodes PNG (`PngCodec.Save`); see the design of the CanvasNet OTS item.
- **ImageRenderService** unit of `Preview`: `RenderedImage` and the PNG of its pixels (`EncodePng`).
- **MarkRegistry** unit of `Annotation`: `Mark` and its geometry.
- **.NET BCL**: text building, streams, and math.

#### Callers

- `PayloadBuilder` asks for the pictures of each picture that marks were drawn on.
