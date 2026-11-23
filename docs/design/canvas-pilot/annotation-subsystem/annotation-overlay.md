### AnnotationOverlay

![Annotation Structure](AnnotationSubsystemView.svg)

#### Purpose

`AnnotationOverlay` is the layer on which the user marks the shown picture: a floating bar with a select, a rectangle,
an ellipse, a freehand, and an arrow tool as icon buttons and a button that clears all marks, the drawing of the chosen
shape with the pointer or the keyboard in the pixels of the picture, an optional short note for each finished shape, and
the numbered marks that are drawn over the picture. It makes each mark in the `MarkRegistry`, pinned to the picture that
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
  `DraftPoints` in picture pixels, `PendingNumber`, `NoteText`); `VisibleMarks`, `HiddenMarkCount`, `HiddenMarksText`,
  `HasHiddenMarks`; `HasMarks` (the message holds a mark, shown or hidden); `KeyboardCursor`; `StatusText`; and the
  toggle flags of the tool buttons (`IsPointerSelected`, `IsRectangleSelected`, and so on). The event `MarkAdded`
  carries the new `Mark`. `ClearMarksCommand` raises the event `ClearMarksRequested`; it has no `CanExecute`, because
  `RefreshMarks` can run on a worker thread and a change of `CanExecuteChanged` would then reach the button on the wrong
  thread, so the button binds its `IsEnabled` to `HasMarks` instead.
- `AnnotationToolbarViewModel`: one property, `Annotation`, the view model above. It exists only so that the view
  locator finds the tool bar view; the tool bar is placed in the slot that floats over the stage of the preview pane,
  outside the scaled picture, so its buttons keep their size at every zoom.
- `AnnotationOverlayView` (a `UserControl`): a transparent surface as large as the picture, in the picture's own pixels,
  with a canvas on which it draws, for each shown mark, a white line under a colored line and a numbered badge, then the
  shape under construction as a dashed line with the number it will get, and the cursor of the keyboard. The shapes
  carry automation identifiers and names such as `Mark 2, rectangle, note: ...`.
- `AnnotationToolbarView` (a `UserControl`): a column that is as wide as its buttons (about 230, so it fits a stage 360
  wide). It holds a wrapping row with the bar and the *N hidden* chip, the note card, and two live-region texts. The bar
  (`ToolBar`) is a rounded, raised strip 40 high with `CpShadowFloating` whose tooltip is the status sentence; it holds
  the five `ToolToggle` buttons (`AnnotationToolSelect`, `AnnotationToolRectangle`, `AnnotationToolEllipse`,
  `AnnotationToolFreehand`, `AnnotationToolArrow`; each an icon only, named Select, Rectangle, Ellipse, Freehand, and
  Arrow, with the tooltip `Name  key` where the key is V, R, E, F, or A), which are disabled together while
  `ToolsEnabled` is false; a divider; and the icon button *Clear marks* (`AnnotationClearMarks`, enabled by `HasMarks`,
  tooltip `Clear marks  removes every mark from the message`). The chip (`HiddenMarksChip`, a warning icon and the text
  `N hidden`, with the full sentence as its tooltip) is visible while `HasHiddenMarks` holds. The note card (`NoteCard`,
  with the panel `AnnotationNotePanel`: a text box limited to 200 characters, *Add mark*, *Skip note*) lies under the
  bar and is visible only while a note is asked for. `AnnotationStatus` and `AnnotationHiddenMarks` are text blocks one
  pixel in size and fully transparent, with a polite live setting, so that a screen reader reads the status sentence and
  the hidden-marks sentence that a sighted user gets from the tooltip and the chip.
- `AnnotationOverlayStyle`: the colors, line width, badge size and place, and arrow head size of a mark on the screen.
  They are the same as in `MarkImageComposer`, which cannot be used here because `Payload` depends on `Annotation`; a
  test keeps the two equal.
- **Automation identifiers**: new `AnnotationClearMarks`; `AnnotationToolSelect` and the four other tool identifiers,
  `AnnotationNotePanel` (now inside `NoteCard`), `AnnotationNoteBox`, `AnnotationNoteAdd`, and `AnnotationNoteSkip` are
  unchanged, but the accessible names of the tools changed from `Rectangle mark` and so on to `Rectangle`.

**States.** The shape under construction is in one of three states: none, *drawing* (the button or the Enter key started
it), or *waiting for its note* (it was finished and is valid). A shape is finished by `EndStroke`; a click, a shape
smaller than a pixel, goes back to none and says so in the status sentence. A mark is made in the registry only when the
note state ends, because a mark never changes after it is made: `CommitNote` makes it with the note typed, `SkipNote`
without one. Starting the next shape or choosing another tool while a note waits commits the shape with the note typed
so far, so that a user who draws several marks in a row does not have to answer a prompt after each. `Cancel` and Escape
throw the shape away in either state.

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
key alone when it had none.

**SelectTool(tool)**: finishes a waiting note with the note typed so far, throws a shape being drawn away, sets the
tool, and calls the drag-out switch of the host with `false` for a tool and `true` for none, only when the tool changed.
A tool that draws is refused while `ToolsEnabled` is `false`; the toggle buttons of the tool bar are told to go back.

**SetActiveMarkIds(markIds)**: stores the identities of the mark chips that are in the message and works out the shown
marks with `MarkRegistry.VisibleMarks` for the key of the shown picture, and the number of hidden marks as the active
marks that are not shown. It is also run when the picture changes. Marks that are hidden stay in the registry and in the
message.

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
nothing, and the press reaches the picture. A loss of the pointer capture without a release throws the shape away. A
handler on the window, in the tunnel phase, turns Escape into `Cancel` while a shape is under construction, wherever the
focus is. The layer takes the keyboard focus, and is a tab stop, only while a tool is chosen. The layer handles
`RequestBringIntoView`, so that focusing it for its keys does not scroll the picture to show the whole of the layer (the
picture now has a margin inside the scroller of the pane). When a note is asked for,
the tool bar view puts the focus in the note box (after the layout), and Enter and Escape in the box make the mark or
throw the shape away. When the tool bar view is attached it looks for the `PreviewPaneView` above it and listens to the
key presses in the whole pane; with no modifier, with `ToolsEnabled` true, and with a source that is not a `TextBox`, it
chooses the tool of the key: V for Select, R for rectangle, E for ellipse, F for freehand, and A for arrow, and handles
the key. It stops listening when it is detached.

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
- **Avalonia and CommunityToolkit.Mvvm**: the views, and observable properties and commands of the view model.

#### Callers

- `ApplicationComposition` creates the view model over the mark registry and the preview pane, gives it the drag-out
  switch (`PreviewPaneViewModel.IsDragOutEnabled`), and puts it in the overlay slot and its tool bar in the slot that
  floats over the stage of the pane. It joins the layer to the message: it sets `IsDrawingAllowed` from
  `ComposerViewModel.CanEdit` and calls `SetActiveMarkIds` with `MarkChipIds` whenever the composer reports a change,
  and adds the chip of each mark that `MarkAdded` reports with `ComposerViewModel.AddMark`, which makes it one undo
  step.
- `ChatPanelViewModel` handles `ClearMarksRequested` by calling `ComposerViewModel.DeleteMarkChips`, which removes the
  chips of all marks, hidden ones included, with one undo step for each chip.
- The view locator finds the two views from the two view models by their names.
- `AnnotationToolbarView` depends on `PreviewPaneView` only to take the tool keys from it.
