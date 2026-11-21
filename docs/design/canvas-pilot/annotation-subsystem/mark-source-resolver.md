### MarkSourceResolver

![Annotation Structure](AnnotationSubsystemView.svg)

#### Purpose

`MarkSourceResolver` finds which parts of a picture's source file lie under a mark, so that the assistant is told which
nodes, bars, or anchors the user pointed at and not only which pixels. It uses only what the renderer reported with the
picture, and says plainly when the picture reported nothing.

#### Data Model

- `SourceElementReference`: immutable; `Kind` (`node`, `edge`, `mark`, `axis`, or `anchor`), `Id`, `Text`, `Line`, `Box`
  and `Pixel`, copied from the renderer's position or anchor.
- `SourceElementSearchResult`: immutable; `Elements` (most covered first), `IsAvailable`, `UnavailableReason`, and
  `IsTruncated`. `IsAvailable` with no elements means that nothing lies under the mark; without `IsAvailable` the
  picture cannot say.
- Constants: at most 25 elements; a quarter of an element's box for an area; 24 pixels from the head of an arrow to an
  anchor; 8 pixels from a stroke to an anchor.

#### Key Methods

**Resolve(mark)**: takes the picture of the mark. When the picture has element positions it resolves against their
boxes; otherwise, when it has anchors with a pixel, against the anchors; otherwise it returns not available with the
reason.

**Rules for element boxes.** An element without a box is skipped, and a box thinner than one pixel is widened to one
pixel around its center so that edges drawn as straight lines can be covered. Each remaining element gets a score above
zero when it is under the mark, and is left out when its score is zero:

| Mark | The element is under the mark when | Score |
| --- | --- | --- |
| Rectangle | a quarter of its box is covered, or its center is inside | the covered part |
| Ellipse | the same, measured with the ellipse on a 16 by 16 grid | the covered part |
| Arrow | its box holds the head, or the tail | 1 for the head, one half for the tail |
| Freehand | the stroke touches its box | the part of the stroke inside |

The elements are ordered by score, the highest first, then by area of the box, the smallest first (the most specific
element), then in the order the renderer reported them. At most 25 are returned; `IsTruncated` is set when more were
found or when the renderer itself says that it left positions out.

**Rules for anchors.** An anchor without a pixel is skipped. A rectangle covers the anchors inside it, an ellipse those
inside the ellipse, an arrow only the one anchor nearest its head and at most 24 pixels from it, and a freehand stroke
the anchors within 8 pixels of the stroke. They are ordered by distance from the center of the area, the head of the
arrow, or the middle point of the stroke, the nearest first. An anchor is reported as an element of kind `anchor` with
its name as identifier and its pixel.

**Not available.** The reason is `{format} provides no element positions` for formats that never report them (SVG, PDF,
presentations, raster pictures), `the renderer reported no element positions for this {format} picture` for Mermaid and
Vega-Lite pictures without positions, and `the model declares no anchors that appear in the picture` for an OpenSCAD
model without usable anchors.

A box is the bounding box of the element, so the box of a diagonal edge also covers empty corners; this limit comes from
the information the renderer gives and is the reason for the quarter rule: an edge is reported only when the mark covers
a good part of its box.

#### Error Handling

A missing mark throws `ArgumentNullException`. A picture without positions is not an error: it is the not available
result. The unit performs no I/O, keeps no state, and logs nothing.

#### Dependencies

- **MarkRegistry** unit: the `Mark` and its `MarkGeometry`.
- **Preview subsystem** (`ImageRenderService` unit): the positions, anchors, and the truncation flag that the picture
  carries, and the point and box types.
- **.NET BCL**: collections and math.

#### Callers

- The payload subsystem asks for the elements under each mark of the message.
