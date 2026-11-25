### AnnotationOverlay

![Annotation Structure](AnnotationSubsystemView.svg)

#### Purpose

`AnnotationOverlay` is the layer on which the user marks the shown picture: a floating bar with a select, a rectangle,
an ellipse, a freehand, and an arrow tool as icon buttons and a button that clears all marks, the drawing of the chosen
shape with the pointer or the keyboard in the pixels of the picture, an optional short note for each finished shape, and
the numbered marks that are drawn over the picture with their notes beside them, and the change of the note of a mark
of the message. It makes each mark in the `MarkRegistry`, pinned to the picture that
was shown when the shape was started, and reports it so that the host can add the mark's chip to the message. It never
decides what is part of the message: the host tells it which mark chips the message holds, and the layer draws only
those marks of the shown picture; when the user clears the marks, it only asks the host to take them out. All of its
rules are in a view model that needs no window, and the views only forward the pointer and the keys and draw what the
view model holds.

#### Data Model

- `AnnotationTool`: `None`, `Rectangle`, `Ellipse`, `Freehand`, `Arrow`. `None` is the choice that the tool bar calls
  *Select*: the pointer keeps its own use on the picture.
- `AnnotationOverlayViewModel` (the logic): `CurrentTool`; `IsDrawingAllowed` (set by the host, true while the message
  can be edited); `HasPicture`, `PictureWidth`, `PictureHeight` (from `IRenderedImageSource`); `ToolsEnabled` (a picture
  is shown and drawing is allowed); the shape under construction (`IsDrawing`, `IsNoteEditing`, `HasDraft`, `DraftKind`,
  `DraftPoints` in picture pixels, `PendingNumber`, `NoteText`); the change of the note of a mark (`IsEditingMarkNote`,
  `EditingMarkId`, and the texts `NoteBoxName` and `CommitNoteText` of the note card); `VisibleMarks`,
  `HiddenMarkCount`, `HiddenMarksText`, `HasHiddenMarks`; `HasMarks` (the message holds a mark, shown or hidden);
  `KeyboardCursor`; `StatusText`; and the toggle flags of the tool buttons (`IsPointerSelected`, `IsRectangleSelected`,
  and so on). The event `MarkAdded` carries the new `Mark`. `ClearMarksCommand` raises the event `ClearMarksRequested`;
  it has no `CanExecute`, because `RefreshMarks` can run on a worker thread and a change of `CanExecuteChanged` would
  then reach the button on the wrong thread, so the button binds its `IsEnabled` to `HasMarks` instead.
- `AnnotationToolbarViewModel`: one property, `Annotation`, the view model above. It exists only so that the view
  locator finds the tool bar view; the tool bar is placed in the slot that floats over the stage of the preview pane,
  outside the scaled picture, so its buttons keep their size at every zoom.
- `AnnotationOverlayView` (a `UserControl`): a transparent surface as large as the picture, in the picture's own pixels,
  with a canvas on which it draws, for each shown mark, a white line under a colored line, then the note labels, then
  the numbered badges, so that nothing covers a number; then the shape under construction as a dashed line with the
  number it will get, and the cursor of the keyboard. The shapes carry automation identifiers and names such as
  `Mark 2, rectangle, note: ...`. The note label (`AnnotationMarkNote`, named `Note of mark 2: ...`) is a `Border` of
  the style class `markNote`, whose background is `CpSurfaceOverlay`, whose text is `CpTextPrimary`, whose radius is
  `CpRadiusPill`, and whose edge has the color of the mark; its text is one line cut short with an ellipsis. The colored
  line, the badge, and the label of a shown mark answer the pointer, carry the mark in their `Tag`, and have the tooltip
  `Mark 2: <the whole note>` when the mark has a note; the white line, the dashed shape, and its badge do not answer the
  pointer. The canvas has no background, so a press beside them reaches the surface.
- `AnnotationToolbarView` (a `UserControl`): a column that is as wide as its buttons (about 230, so it fits a stage 360
  wide). It holds a wrapping row with the bar and the *N hidden* chip, the note card, and two live-region texts. The bar
  (`ToolBar`) is a rounded, raised strip 40 high with `CpShadowFloating` whose tooltip is the status sentence; it holds
  the five `ToolToggle` buttons (`AnnotationToolSelect`, `AnnotationToolRectangle`, `AnnotationToolEllipse`,
  `AnnotationToolFreehand`, `AnnotationToolArrow`; each an icon only, named Select, Rectangle, Ellipse, Freehand, and
  Arrow, with the tooltip `Name  key` where the key is V, R, E, F, or A), which are disabled together while
  `ToolsEnabled` is false; a divider; and the icon button *Clear marks* (`AnnotationClearMarks`, enabled by `HasMarks`,
  tooltip `Clear marks  removes every mark from the message`). The chip (`HiddenMarksChip`, a warning icon and the text
  `N hidden`, with the full sentence as its tooltip) is visible while `HasHiddenMarks` holds. The note card (`NoteCard`,
  with the id `AnnotationNoteCard`, a radius of 14 like the other cards, and the panel `AnnotationNotePanel`: the label
  *Note:*, a text box limited to 200 characters and named by `NoteBoxName`, *Skip note* in the secondary button look,
  which is hidden while the note of an existing mark is changed, *Cancel* (`AnnotationNoteCancel`) in the secondary
  button look, which is shown only then, and the primary button (`AnnotationNoteAdd`) with the text `CommitNoteText`,
  *Add mark* or *Save note*, which fills with the accent color) lies under the
  bar and is visible only while a note is asked for. `AnnotationStatus` and `AnnotationHiddenMarks` are text blocks one
  pixel in size and fully transparent, with a polite live setting, so that a screen reader reads the status sentence and
  the hidden-marks sentence that a sighted user gets from the tooltip and the chip.
- `AnnotationOverlayStyle`: the colors, line width, badge size and place, and arrow head size of a mark on the screen.
  They are the same as in `MarkImageComposer`, which cannot be used here because `Payload` depends on `Annotation`; a
  test keeps the two equal. It also lays out the note label (`NoteFontSize`, `NoteMaxWidth`, and `NoteLabel`, which
  returns a `NoteLabelLayout`): the label is as tall as the badge and centered on it, to its right when there is room
  and to its left otherwise, at most 40 % of the picture wide (between six and twenty-four badge radii), and never
  wider than the room inside the picture. The label is drawn only on the screen, so `MarkImageComposer` has no copy of
  these.
- **Automation identifiers**: new `AnnotationNoteCancel` and `AnnotationMarkNote`; `AnnotationClearMarks`,
  `AnnotationToolSelect` and the four other tool identifiers, `AnnotationNotePanel`, `AnnotationNoteBox`,
  `AnnotationNoteAdd`, and `AnnotationNoteSkip` are unchanged.

**States.** The shape under construction is in one of three states: none, *drawing* (the button or the Enter key started
it), or *waiting for its note* (it was finished and is valid). A shape is finished by `EndStroke`; a click, a shape
smaller than a pixel, goes back to none and says so in the status sentence. A mark is made in the registry only when the
note state ends: `CommitNote` makes it with the note typed, `SkipNote`
without one. Starting the next shape or choosing another tool while a note waits commits the shape with the note typed
so far, so that a user who draws several marks in a row does not have to answer a prompt after each. `Cancel` and Escape
throw the shape away in either state.

A fourth state, *editing a note*, is the note card open for a mark that already exists (`IsEditingMarkNote`). It has no
shape to draw, so `HasDraft` is false. `CommitNote` saves the note with `MarkRegistry.ChangeNote` and raises no
`MarkAdded`; `SkipNote` does nothing; `Cancel` and Escape close the card and keep the note and the mark as they were.
Starting a shape, choosing a tool, or opening another note saves the note typed so far. A new picture does not end the
state, because the card is not tied to the shown picture. When no picture is shown any more, for example because the
file could not be drawn or was closed, marks can no longer be made (`ToolsEnabled` is false), so the change is given up
with the note unchanged, as in the last row of the table.

| Aspect | New shape | Editing a note |
| --- | --- | --- |
| Opened by | `EndStroke` of a valid shape | `BeginEditNote` (double-click on the mark, chip request) |
| `NoteText` at open | empty | the current note, or empty |
| Enter or the primary button | `Add` with the note; `MarkAdded` | `ChangeNote`; no `MarkAdded` |
| Second button | *Skip note*: the mark without a note | *Cancel*: note unchanged, mark kept |
| Escape | throws the shape away | closes the card; note and mark unchanged |
| Next shape, other tool, other note | keeps the mark with the typed note | saves the typed note |
| Mark leaves message, marks not allowed | throws the shape away | gives the change up, note unchanged |

#### Key Methods

**BeginStroke / ContinueStroke / EndStroke(point)**: the press, the move, and the release of the pointer, with points in
picture pixels. `BeginStroke` does nothing and returns `false` unless a tool is chosen and `ToolsEnabled`; it remembers
the picture that is shown, so the mark belongs to that picture even if the same pixels are drawn again as a new object
while the note is typed. Points are cut to the picture; a coordinate that is not a number is ignored. A rectangle, an
ellipse, and an arrow keep the first and the current point; a freehand shape keeps each point that is at least half a
pixel from the last, at most 20,000 of them, after which the last point is replaced. `EndStroke` asks
`MarkGeometry.Create` whether the shape is a mark, which refuses a shape smaller than one pixel.

**CommitNote / SkipNote / Cancel / Escape**: `CommitNote` and `SkipNote` call `MarkRegistry.Add` with the remembered
picture, the kind, and the raw points, empty the shape under construction, and only then raise `MarkAdded`, so that a
host that adds the chip finds the mark in the registry and the layer in a clean state. `Cancel` throws the shape away.
`Escape` cancels a shape and otherwise chooses no tool; it returns whether it had an effect, so the view can leave the
key alone when it had none. While the note of an existing mark is changed, `CommitNote` calls `MarkRegistry.ChangeNote`
instead of `Add`, `SkipNote` does nothing, and `Cancel` closes the card with the note unchanged.

**BeginEditNote(markId)**: opens the note card for a mark of the message. It returns `false` and changes nothing when
`ToolsEnabled` is false, a shape is being drawn, or the mark is not in the message or not in the registry. A note card
that is open is finished first with what was typed. Otherwise it remembers the mark and its number, fills `NoteText`
with the mark's note, hides the cursor of the keyboard, and returns `true`. The mark need not be on the shown picture,
so the request of a chip can change the note of a hidden mark. The status sentence is
`Mark N: change the note and press Enter, or press Escape to keep it.` The view model follows `MarkRegistry.NoteChanged`
and works out the shown marks again, so a changed note is drawn at once.

**SelectTool(tool)**: finishes a waiting note with the note typed so far, throws a shape being drawn away, sets the
tool, and calls the drag-out switch of the host with `false` for a tool and `true` for none, only when the tool changed.
A tool that draws is refused while `ToolsEnabled` is `false`; the toggle buttons of the tool bar are told to go back.

**SetActiveMarkIds(markIds)**: stores the identities of the mark chips that are in the message and works out the shown
marks with `MarkRegistry.VisibleMarks` for the key of the shown picture, and the number of hidden marks as the active
marks that are not shown. It is also run when the picture changes. Marks that are hidden stay in the registry and in the
message. When the mark whose note is being changed is no longer in the message, the change is given up first.

**MoveCursor(dx, dy) / ConfirmCursor / HideCursor**: the keyboard way to draw. The cursor starts in the middle of the
picture, is moved by the arrow keys (8 pixels, 1 with Shift, 40 with Control), `ConfirmCursor` starts the shape at the
cursor or, when one is being drawn, finishes it there, and moving the cursor reshapes the shape that is being drawn. The
same `BeginStroke`, `ContinueStroke`, and `EndStroke` rules apply, so the keyboard makes the same marks as the pointer.
While a note waits the keys belong to the note.

**HasMarks / ClearMarksCommand**: `HasMarks` is true while the visible marks or the hidden mark count is above zero; it
is raised whenever the marks are worked out again (`RefreshMarks`). The command only raises `ClearMarksRequested`; it
takes no mark out itself. The host (the chat panel) takes the chips of all marks out of the composer, with one undo step
for each chip, and then the overlay is told the new, shorter list of chips through `SetActiveMarkIds`, which empties
`HasMarks`.

**Views.** `AnnotationOverlayView` converts nothing: its position for a pointer event is already in picture pixels,
because the preview pane scales the picture and the layer together (see the *PreviewPane* design). On a press with the
left button while a tool is chosen it starts the shape, takes the pointer so that the shape goes on outside the picture,
takes the keyboard focus, and marks the event handled so that the picture is not dragged out; with no tool it handles
only a double-click on a mark: it walks up from the element under the pointer to the badge, line, or label that carries
the mark and calls `BeginEditNote`; every other press reaches the picture, so a single press can still drag the picture
out. A loss of the pointer capture without a release throws the shape away. A handler on the window, in the tunnel
phase, turns Escape into `Cancel` while a shape is under construction or the note card is open, wherever the focus is.
The layer takes the keyboard focus, and is a tab stop, only while a tool is chosen. The layer handles
`RequestBringIntoView`, so that focusing it for its keys does not scroll the picture to show the whole of the layer (the
picture now has a margin inside the scroller of the pane). When a note is asked for, the tool bar view puts the focus in
the note box (after the layout), and Enter and Escape in the box make the mark or throw the shape away. When the note of
an existing mark is opened, the tool bar view remembers the element that had the focus, puts the caret at the end of the
note, and gives the focus back to that element when the card closes, unless another note was opened at once or the card
closed because the user started a shape (`IsDrawing`), in which case the layer keeps the focus while the shape is drawn
and the remembered element is forgotten. When the
tool bar view is attached it looks for the `PreviewPaneView` above it and listens to the key presses in the whole pane;
with no modifier, with `ToolsEnabled` true, and with a source that is not a `TextBox`, it chooses the tool of the key: V
for Select, R for rectangle, E for ellipse, F for freehand, and A for arrow, and handles the key. It stops listening
when it is detached.

#### Error Handling

The view model does not throw for what the user does: a click, a shape that leaves the picture, a key without a tool, a
tool when no picture is shown, and a note that is too long are all answered with a state and a sentence in `StatusText`.
Arguments that a caller must not give throw: `ArgumentNullException` for missing parts or identities,
`ArgumentOutOfRangeException` for a tool that is not defined. When `MarkRegistry.Add` throws for a shape that was
accepted (it should not happen, because the shape was checked), the shape is lost, a warning is logged, and the user
sees no mark. Nothing in the unit performs I/O.

#### Dependencies

- **Annotation subsystem** (`MarkRegistry` unit): makes the marks; `MarkGeometry` checks the shapes and gives the
  distance and the maximum note length.
- **Preview subsystem**: `IRenderedImageSource` (the shown picture and the event that it changed), `RenderedImage`, and
  the point and box types; the view of the tool bar also finds `PreviewPaneView` above it, to take the tool keys. The
  view model does not reference `PreviewPane` or the composer.
- **Logging subsystem**: `RollingFileLogger` for the warning above.
- **ThemeResources** (of the AppShell subsystem, by resource keys only): the surface, text, border, and warning colors,
  the radii, the floating shadow, the small type size, the tool icons, and the `IconButton`, `PrimaryButton`,
  `SecondaryButton`, and `ToolToggle` themes.
- **Avalonia and CommunityToolkit.Mvvm**: the views, and observable properties and commands of the view model.

#### Callers

- `ApplicationComposition` creates the view model over the mark registry and the preview pane, gives it the drag-out
  switch (`PreviewPaneViewModel.IsDragOutEnabled`), puts it in the overlay slot of the pane and its tool bar view model
  in the slot that floats over the stage of the pane, and hands it to the chat panel.
- `ChatPanelViewModel` joins the layer to the message. Whenever the composer reports a change (and once when it is
  made), it sets `IsDrawingAllowed` from `ComposerViewModel.CanEdit` and calls `SetActiveMarkIds` with `MarkChipIds`. It
  adds the chip of each mark that `MarkAdded` reports with `ComposerViewModel.AddMark`, which makes it one undo step.
  It handles `ClearMarksRequested` by calling `ComposerViewModel.DeleteMarkChips`, which removes the chips of all marks,
  hidden ones included, with one undo step for each chip. It handles `ComposerViewModel.MarkNoteEditRequested` by
  calling `BeginEditNote` with the mark of the chip.
- The view locator finds the two views from the two view models by their names.
- `AnnotationToolbarView` depends on `PreviewPaneView` only to take the tool keys from it.
