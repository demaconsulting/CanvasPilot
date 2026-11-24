## Annotation

![Annotation Structure](AnnotationSubsystemView.svg)

### Overview

The `Annotation` subsystem holds the marks that the user draws on the shown picture to say what they mean in a message:
which picture each mark belongs to, its number, where it is in that picture, and an optional short note, and it finds
which parts of the picture's source file lie under a mark, and it offers the layer on which the user draws them. A mark
is a rectangle, an ellipse, an arrow, or a freehand stroke. The registry and the resolver have no dependency on the user
interface, so their rules are tested without a window; the layer, `AnnotationOverlay`, keeps its own rules in a view
model that needs no window either, and has two thin views, the layer over the picture and a floating tool bar.

The subsystem contains three units: `MarkRegistry`, which makes and keeps the marks, `MarkSourceResolver`, which maps a
mark to the parts of the source file under it, and `AnnotationOverlay`, which is the layer on which the marks are drawn
and shown. It depends on `Preview` for the picture type `RenderedImage`, the point and box types of its coordinates, and
`IRenderedImageSource`, and on `Logging`, and on nothing else of the application. The `Composer` subsystem knows a mark
only as a chip with an identity and a number, so the overlay does not know the composer either: the chat panel
joins the two, by telling the overlay which mark chips the message holds and by adding the chip of each mark that the
overlay reports.

### Interfaces

**MarkRegistry**: The marks of the message being composed.

- *Type*: In-process .NET class.
- *Role*: Provider.
- *Contract*: `Add` takes the picture the user drew on, the shape, the points the pointer visited in the picture's
  pixels, and an optional note, and returns the `Mark` with the next number. `ActiveMarks` takes the identities of the
  mark chips that are in the message and returns their marks in number order. `VisibleMarks` returns those that were
  drawn on exactly the picture whose key is given. `ChangeNote` changes the note of a mark it holds, keeping everything
  else about the mark, and raises `NoteChanged` with the changed mark. `Clear` forgets all marks.
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

**AnnotationOverlay**: The layer on which the user marks the shown picture.

- *Type*: In-process .NET view models (`AnnotationOverlayViewModel`, `AnnotationToolbarViewModel`) and Avalonia views
  that the view locator finds from them.
- *Role*: Provider to the host, and consumer of the registry and the shown picture.
- *Contract*: `SelectTool`, `BeginStroke`, `ContinueStroke`, `EndStroke`, `CommitNote`, `SkipNote`, `Cancel`, `Escape`,
  and the keyboard methods `MoveCursor` and `ConfirmCursor` drive it; the points are in the pixels of the shown picture.
  `SetActiveMarkIds` tells which mark chips the message holds. `IsDrawingAllowed` is set by the host. The event
  `MarkAdded` carries each finished mark, which is already in the registry. `ClearMarksCommand` raises
  `ClearMarksRequested`, which the host answers by taking the marks out of the message; `HasMarks` tells whether the
  message holds a mark. `BeginEditNote` opens the note card for a mark of the message, as a double-click on the mark
  with no tool chosen does; `IsEditingMarkNote` tells that the card is open for an existing mark. The constructor takes
  a switch that is called with `false` while a tool is chosen and `true` otherwise.
- *Constraints*: Owned by the user interface thread; not safe for concurrent use. The views must be laid out in the
  pixels of the picture, as the overlay slot of `PreviewPane` does.

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

**The layer.** `AnnotationOverlay` lives in the overlay slot of the preview pane, which is laid out in the pixels of the
picture and scaled with it, so the position of a pointer event on the layer is already a point of the picture and no
conversion exists to get wrong. Its tools are in a second slot, floating over the stage of the pane, which is not
scaled. The overlay makes a mark only when its note is finished or skipped, and reports it through an
event; the overlay and the composer do not know each other, because `Composer` already depends on `Annotation`. The chat
panel joins them: it adds the chip of each reported mark with `ComposerViewModel.AddMark`, which is one undo step, and
tells the overlay which chips the message holds each time the composer changes, so undo and redo of the chip hide and
show the mark with no undo of its own. The tool bar has a button that clears the marks: the overlay only raises
`ClearMarksRequested`, and the chat panel takes the chips of all marks, those hidden by a changed picture included, out
of the composer. Because the pointer is also used to drag the picture out of the application, the overlay switches that
off while a tool is chosen and handles the press only then, so with no tool the press goes on to the picture. A shape is
cancelled by Escape from anywhere in the window, and the whole layer can be used with the keyboard. What a mark looks
like on the screen is what `MarkImageComposer` draws in the annotated picture, so the user sees what the assistant gets.

**Notes.** The layer shows the note of each shown mark on one line beside its number badge, in the colors of the theme,
cut short with an ellipsis and kept inside the picture, and gives the badge, the line, and the label the whole note as
their tooltip. The label is drawn only on the screen; the pictures sent to the assistant carry the note as data. The
note of a mark of the message can be changed until the message is sent. A `Mark` never changes;
`MarkRegistry.ChangeNote` replaces it at its place with a new instance of the same identity, number, geometry, picture,
and time, so the marks of a message that was sent, which hold the instances of that moment, keep their notes, and raises
`NoteChanged`, which the overlay and the composer follow. The overlay opens the same note card as for a new shape,
filled with the note, on a double-click on the mark with no tool chosen, or when the chat panel passes on a request from
the mark's chip. Confirming changes only the note; canceling leaves the note and the mark as they were. Starting a
shape, choosing a tool, or opening another note saves what was typed; the change is given up when the mark's chip leaves
the message or marks can no longer be made. A change of a note is not a step of the composer's undo history, because the
note belongs to the registry and not to the text of the message.
