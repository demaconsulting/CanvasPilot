## Annotation

![Annotation Structure](AnnotationSubsystemView.svg)

### Overview

The `Annotation` subsystem holds the marks that the user draws on the shown picture to say what they mean in a message:
which picture each mark belongs to, its number, where it is in that picture, and an optional short note, and it finds
which parts of the picture's source file lie under a mark. A mark is a rectangle, an ellipse, an arrow, or a freehand
stroke. The subsystem has no dependency on the user interface, so all of its rules are tested without a window; the
layer over the picture on which the user draws is added by a later unit and builds on it.

The subsystem contains two units: `MarkRegistry`, which makes and keeps the marks, and `MarkSourceResolver`, which maps
a mark to the parts of the source file under it. It depends on `Preview` for the picture type `RenderedImage` and the
point and box types of its coordinates, and on nothing else of the application. The `Composer` subsystem knows a mark
only as a chip with an identity and a number, so it does not depend on this subsystem; the unit that joins the two is
added later.

### Interfaces

**MarkRegistry**: The marks of the message being composed.

- *Type*: In-process .NET class.
- *Role*: Provider.
- *Contract*: `Add` takes the picture the user drew on, the shape, the points the pointer visited in the picture's
  pixels, and an optional note, and returns the `Mark` with the next number. `ActiveMarks` takes the identities of the
  mark chips that are in the message and returns their marks in number order. `VisibleMarks` returns those that were
  drawn on exactly the picture whose key is given. `Clear` forgets all marks.
- *Constraints*: Not safe for concurrent use; the user interface thread owns it. `Marks` and the lists it returns are
  copies that other threads may read. `Add` throws `ArgumentException` for a shape smaller than one pixel and does not
  change the registry.

**MarkSourceResolver**: The parts of the source under a mark.

- *Type*: In-process .NET static class.
- *Role*: Provider.
- *Contract*: `Resolve` takes a `Mark` and returns a `SourceElementSearchResult`: the elements under the mark, with
  kind, identifier, text, source line, and box or pixel, the most covered first, or the statement that the picture
  cannot tell, with the reason.
- *Constraints*: Stateless, safe for concurrent use. At most 25 elements are returned.

### Design

**A mark belongs to a picture.** A `Mark` holds the `RenderedImage` it was drawn on. That object carries the file, the
fingerprints of the file and of the pixels, and the pixels the user saw, so a mark that is described to the assistant
long after the file has changed still says which picture it meant. The key of a picture is its path and the fingerprint
of its pixels; a picture that is drawn again with the same pixels has the same key, so its marks stay visible, and a
picture with other pixels has another key, so the marks of the earlier picture are hidden there, because their geometry
means the pixels of the picture they were drawn on.

**A mark is part of the message while its chip is.** The registry never decides which marks are in the message. The
caller passes the identities of the mark chips that are in the composer's text, and the registry returns those marks.
This way the one undo system of the composer is enough: undoing the addition of a mark removes its chip, which hides the
mark, and redoing brings both back. A mark is removed from the registry only when the registry is cleared, which is done
when the message is sent.

**Numbers.** A mark gets one more than the largest number in the registry and keeps it for the draft. A mark that was
undone keeps its number, so the next mark does not take it over and a sentence about "mark 2" never changes its meaning.

**Geometry in picture pixels.** The points of a mark are in the coordinates of the picture it was drawn on, with the
origin at the top-left corner, whatever the zoom of the window was; the layer over the picture is laid out in those
pixels, so the unit does not convert anything. `MarkGeometry.Create` puts the points in one normal form: every point is
moved into the picture, a rectangle or an ellipse becomes its top-left and bottom-right corners, an arrow its tail and
head, and a freehand stroke a polyline without repeated points that is simplified with the Ramer-Douglas-Peucker method
to at most 500 points, always keeping the first and the last point. A shape smaller than one pixel is a click and is
refused.

**Source elements.** Only some pictures know where their parts are: the renderer reports the box of every node, edge,
chart mark, and axis for Mermaid diagrams and Vega-Lite charts, and the pixel of every anchor that an OpenSCAD model
declares. `MarkSourceResolver` uses these facts and invents nothing; the rules of each shape are in the design of that
unit. For every other kind of picture it says so, with a reason that names the format, so that the assistant is not
told that nothing lies under a mark when the picture simply cannot say.
