### MarkRegistry

![Annotation Structure](AnnotationSubsystemView.svg)

#### Purpose

`MarkRegistry` makes and keeps the marks of the message being composed: it pins each mark to the exact picture it was
drawn on, numbers it, puts its geometry in one normal form in the pixels of that picture, keeps an optional short note,
and tells which marks are part of the message and which are shown on a picture. It is what makes "a mark never moves to
another picture" true, in a unit that needs no window to test.

#### Data Model

- `MarkKind`: `Rectangle`, `Ellipse`, `Arrow`, `Freehand`.
- `MarkGeometry`: immutable; `Kind`, `Points` (an immutable array of `RenderPoint`, in picture pixels) and `BoundingBox`
  (a `RenderBox`). Made only by `MarkGeometry.Create`. Rectangle and ellipse: two points, top-left then bottom-right,
  each side at least one pixel. Arrow: tail then head, at least one pixel apart. Freehand: two to 500 points, first and
  last as drawn, at least one pixel long. Every point is inside the picture.
- `Mark`: immutable; `Id` (a `Guid`, carried by the mark's chip), `Number` (one or more), `Geometry`, `Kind`, `Note`
  (`null` or one line of at most 200 characters), `Snapshot` (the `RenderedImage` it was drawn on) and `CreatedUtc`.
- `MarkRegistry`: the list of marks in the order they were made, and a `TimeProvider` for the time of creation.
- `MarkNoteChangedEventArgs`: the data of `NoteChanged`, the `Mark` as it is after the change.
- Invariants: numbers in the registry are unique; a mark instance never changes, and `ChangeNote` replaces it with a new
  instance of the same identity, number, geometry, picture, and time; the registry holds no mark after `Clear`.

#### Key Methods

**Add(snapshot, kind, points, note)**: makes the geometry with `MarkGeometry.Create` (which throws for a shape that is
not a mark, before anything is added), takes the number one more than the largest number in the registry (one for the
first mark), cleans the note, adds the mark, and returns it.

**ChangeNote(markId, note)**: finds the mark, cleans the note as `Add` does, and, when the cleaned note differs from the
mark's note, puts a new `Mark` with the same identity, number, geometry, picture, and time of creation and the new note
at the same place in the list, raises `NoteChanged` once on the caller's thread, and returns the new mark. It returns
the mark unchanged, without the event, for the same note, and `null`, without a change, for a mark it does not hold.
Marks and lists that were handed out before keep the note they had, which is what a message that was sent relies on.

**MarkGeometry.Create(kind, points, width, height)**: moves every point into the picture, builds the normal form of the
shape (corners sorted for rectangle and ellipse, tail and head for an arrow, drops repeated points and simplifies for a
stroke), and refuses a shape smaller than `MinimumExtent` (one pixel). The simplification starts with a distance of one
pixel and doubles it until at most 500 points remain; it works with an explicit stack, so a stroke of any length is
safe.

**CleanNote(note)**: replaces every control character, line breaks and tabs included, with a space, trims, cuts to 200
characters without splitting a surrogate pair, and returns `null` when nothing is left.

**ActiveMarks(markIds)**: the marks whose identity is among the given identities, in the order of their numbers; unknown
and repeated identities are ignored. **VisibleMarks(markIds, snapshotKey)**: the active marks whose picture has the
given key. **Find(markId)**, **Marks** (an immutable copy) and **Clear()**.

#### Error Handling

Invalid arguments throw: `ArgumentNullException` for a missing picture, points, or identities;
`ArgumentOutOfRangeException` for a picture smaller than one pixel or an unknown shape; `ArgumentException` for fewer
than two points, a coordinate that is not a finite number, or a shape smaller than one pixel. None of them changes the
registry. The unit performs no I/O and logs nothing; a caller that draws with a pointer decides what to tell the user
about a click.

#### Dependencies

- **Preview subsystem** (`ImageRenderService` unit): the `RenderedImage` picture, its width, height, and `SnapshotKey`,
  and the `RenderPoint` and `RenderBox` types of picture pixels.
- **.NET BCL**: collections, immutable arrays, and `TimeProvider`.

#### Callers

- `AnnotationOverlay` calls `Add` when the user finishes a shape and the note step ends, `ChangeNote` when the user
  confirms a changed note, `VisibleMarks` to decide what to show, and follows `NoteChanged`; the registry is cleared by
  the composer when a message is sent.
- `ComposerViewModel` reads the note of a mark with `Find` for the tooltip of its chip and passes `NoteChanged` on to
  the editing control.
- The payload subsystem reads the marks of the message through `Marks` and `MarkSourceResolver`.
