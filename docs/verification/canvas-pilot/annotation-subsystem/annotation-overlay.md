### AnnotationOverlay

#### Verification Approach

`AnnotationOverlay` is verified at two levels. The rules are verified by unit tests of the view model in
`test/DemaConsulting.CanvasPilot.Tests/AnnotationSubsystem/AnnotationOverlayTests.cs`, which use the real `MarkRegistry`
and a hand-written source of the shown picture (`FakeRenderedImageSource`) that the test changes the way the preview
pane does, and which drive the overlay with the same methods that the views call: the press, the move, and the release
of the pointer, the note, the tool, Escape, and the keyboard. The views are verified by headless Avalonia UI tests in
`test/DemaConsulting.CanvasPilot.UiTests/AnnotationOverlayViewTests.cs`, which show the real preview pane of the
composed application, with the real overlay and the floating tool bar in its slots, over a temporary project with real
SVG files, and send the pointer and the keys through the window as the platform delivers them: mouse press, move, and
release at the points of the window that show chosen points of the picture, key presses for the arrow keys, Enter, and
Escape, and text input for the note. The tests read the mapping between the picture and the window from the layer
itself, so they hold at any zoom. After each drawing they check what the user sees (the shape and its badge, the note
panel, the status sentence, the sentence about hidden marks) and what the application holds (the mark in the registry,
its geometry in picture pixels, the chip in the composer, the switch for dragging out). The look of the marks equals
that of the annotated picture of a message; a unit test compares the colors and sizes with those of `MarkImageComposer`.
The note label beside each badge, its tooltips, and the double-click that opens a note are checked in the same window:
the tests read the label, its place, its colors in both themes, and the tooltips of the badge, line, and label, and
double-click with two quick clicks through the window, which the headless platform counts as a double-click.

#### Test Environment

The standard test runner with the headless Avalonia platform and the Skia back end, and the application's own `App`,
which supplies the view locator. A temporary folder per test holds the application's settings, log, and project, and is
removed afterwards. No window is shown on a screen, no real input device, input method, or screen reader is used.

**Checks by hand before a release** (the headless platform cannot show them; they are not evidence for a requirement):

1. *Looks.* On a diagram, draw a rectangle, an ellipse, a freehand line, and an arrow in the light and the dark theme
   and at fit and at actual size: each mark has a white edge under its color, the badge shows its number and is inside
   the picture, the dashed shape under construction is visible on a dark and on a light picture, the bar floats at the
   top center of the stage with its shadow and the *N hidden* chip beside it, and the bar and the note card wrap in a
   narrow window without hiding the note. Give a mark a long note: the label beside its number is legible in both
   themes, ends with an ellipsis, stays inside the picture, and resting the pointer on the badge, line, or label shows
   the whole note.
2. *Pointer devices.* With a pen or a touch screen, press, move, and lift: the shape follows the contact and the mark is
   made on lift. With the mouse, pressing the right button does not start a shape.
3. *Keyboard and screen reader.* With only the keyboard, choose a tool with Tab and Space or with the keys V, R, E, F,
   and A, move the cursor with the
   arrow keys, start and finish a shape with Enter, type a note, and press Enter; with a screen reader the tools, the
   status sentence, the sentence about hidden marks, and each mark are read with their names, and the marks are read as,
   for example, `Mark 2, rectangle, note: ...`, and each note label as `Note of mark 2: ...`. With the caret beside a
   mark chip in the message, F2 opens the note, the box is read as `Note for mark 2`, Enter saves it, and the focus goes
   back to the message.
4. *Drag out.* Drag the picture into the file manager with no tool chosen: the file is dropped. Choose a tool and drag
   on the picture: a mark is drawn and nothing is dragged out.
5. *With the chat.* When the chat panel is shown, drawing a mark inserts `Mark n` in the row of chips above the words of
   the message, Ctrl+Z removes the chip and the mark, and Ctrl+Y brings both back. Using *Clear marks* with marks hidden
   by a changed picture removes their chips too.
6. *Scrolling.* At a zoom where the picture is larger than the pane, choosing a tool and pressing a key on the layer
   does not scroll the picture.

#### Acceptance Criteria

- All unit tests and UI tests of the unit pass with zero failures.
- Each of the four tools, pressed, moved, and released on the picture, shows its shape while the button is down, asks
  for a note when it is released, and then makes one more mark with the next number, the kind of the tool, the points in
  picture pixels, and the picture that was shown, whatever the zoom of the pane; the mark's chip is in the message, and
  the mark is drawn with its badge and the color of its number.
- A rectangle or ellipse dragged in any direction, a stroke that leaves the picture, a freehand line of any length, and
  an arrow give the same normal geometry as the registry makes; a click or a shape smaller than a pixel makes no mark
  and the status sentence says so; with no tool chosen nothing is drawn and the press reaches the picture.
- A note is limited to 200 characters, is one clean line in the mark, is made with Enter or *Add mark*, is dropped with
  *Skip note*, is kept when the next shape is started or another tool is chosen, and the focus goes to it when it is
  asked for.
- Escape throws away a shape while the button is down and while the note waits, wherever the focus is, makes no mark and
  no chip, and with no shape leaves the tool.
- A mark is drawn only while its chip is in the message and only on exactly the picture it was drawn on; after the file
  changes the earlier marks are not drawn, the sentence says how many are hidden, they stay in the message, and the same
  pixels show them again; undo and redo of the chip hide and show the mark.
- The switch for dragging the picture out is off while a tool is chosen and on otherwise, including after the overlay is
  released, and a drag with the pointer chosen starts the drag out of the picture.
- The tools are off with a reason while no picture is shown or the message cannot be edited, a shape under construction
  is thrown away when the picture changes to other pixels, goes away, or marks are no longer allowed, a chosen tool
  stays while only the picture is missing, and it is given up when marks are not allowed.
- The keyboard makes the same marks: the cursor starts in the middle, moves by 8, 1, or 40 pixels, stays inside the
  picture, is hidden by the pointer, Enter starts and finishes the shape, and the keys belong to the note while it
  waits.
- The tool buttons are icon buttons that have the names Select, Rectangle, Ellipse, Freehand, and Arrow and the tooltips
  `Name  key`, exactly the button of the chosen tool is checked, and a second click on a tool's button chooses the
  pointer.
- With the focus on the stage of the pane, the key R chooses the rectangle and checks its button.
- On a window 360 wide, the bar and the note card lie inside the window.
- *Clear marks* is off while the message holds no mark; with marks in the message it asks the host, which takes every
  mark chip out, and the layer then shows no mark and the button is off again.
- A mark with a note shows it on one line beside its badge, in the colors of the theme, cut short with an ellipsis and
  inside the picture; a mark without a note has no label; a changed note is drawn at once; the badge, line, and label
  have the whole note as their tooltip.
- A double-click on a mark with no tool, or a request of the host, opens the note card for a mark of the message with
  its note and the focus in it; Enter or *Save note* changes only the note and reports no mark; Escape or *Cancel* keeps
  the note and the mark; starting a shape, choosing a tool, or opening another note saves what was typed; the change
  goes on over a new picture and is given up when the mark leaves the message, marks are not allowed, or no picture is
  shown; the focus goes back when the card closes, except when it closed because a shape was started, which keeps the
  focus on the layer.

#### Test Scenarios

**AnnotationOverlayViewModel_Stroke_RectangleDragged_MakesNumberedMarkInPicturePixels**: A rectangle is pressed at (30,
20), moved, and released at (80, 60) on a picture of 200 by 100 pixels. While the button is down the draft follows the
pointer; at the release a note is waited for and no mark exists; skipping the note makes mark number one with the
corners (30, 20) and (80, 60), no note, and the shown picture. This scenario is tested by
`AnnotationOverlayViewModel_Stroke_RectangleDragged_MakesNumberedMarkInPicturePixels`.

**AnnotationOverlayViewModel_Stroke_EllipseAndArrow_MakeMarksOfTheirKind**: An ellipse and an arrow are drawn from the
lower right to the upper left. The ellipse has its corners in normal form and the arrow its tail and head as drawn. This
scenario is tested by `AnnotationOverlayViewModel_Stroke_EllipseAndArrow_MakeMarksOfTheirKind`.

**AnnotationOverlayViewModel_Stroke_Freehand_KeepsSimplifiedPathWithEnds**: A straight line is drawn through 91 points a
pixel apart plus one that is nearer than half a pixel; the draft keeps 91 points, and the mark is the line with its two
ends. This scenario is tested by `AnnotationOverlayViewModel_Stroke_Freehand_KeepsSimplifiedPathWithEnds`.

**AnnotationOverlayViewModel_Stroke_FreehandBeyondLimit_ReplacesLastPoint**: A stroke that already has 20,000 points
does not grow; its last point follows the pointer. This scenario is tested by
`AnnotationOverlayViewModel_Stroke_FreehandBeyondLimit_ReplacesLastPoint`.

**AnnotationOverlayViewModel_Stroke_ClickWithoutDrag_MakesNoMarkAndSaysSo**: A press and release at one point, and at a
point 0.4 pixel away, ask for no note and make no mark, and the text says that a click makes none. This scenario is
tested by `AnnotationOverlayViewModel_Stroke_ClickWithoutDrag_MakesNoMarkAndSaysSo`.

**AnnotationOverlayViewModel_Stroke_PointerOutsidePicture_IsClampedToPicture**: A rectangle is pressed beyond the lower
right corner and released beyond the upper left corner, and a point that is not a number is ignored; the draft and the
mark are inside the picture, from (0, 0) to (200, 100). This scenario is tested by
`AnnotationOverlayViewModel_Stroke_PointerOutsidePicture_IsClampedToPicture`.

**AnnotationOverlayViewModel_BeginStroke_NoToolChosen_StartsNothing**: With no tool, a press, a move, and a release draw
nothing and the text asks to choose a tool. This scenario is tested by
`AnnotationOverlayViewModel_BeginStroke_NoToolChosen_StartsNothing`.

**AnnotationOverlayViewModel_Note_Typed_IsKeptCleanAndCappedAtTwoHundredCharacters**: A note with a line break and 300
more characters is cut to 200 in the text box, and the mark holds one line of 200 characters. This scenario is tested by
`AnnotationOverlayViewModel_Note_Typed_IsKeptCleanAndCappedAtTwoHundredCharacters`.

**AnnotationOverlayViewModel_Note_Skipped_MakesMarkWithoutNote**: A typed note is skipped and the mark has none. This
scenario is tested by `AnnotationOverlayViewModel_Note_Skipped_MakesMarkWithoutNote`.

**AnnotationOverlayViewModel_Note_NextShapeOrOtherToolChosen_KeepsPendingMarkWithTypedNote**: A finished shape with a
partly typed note is kept, with the note, when the next shape is started; the second shape is kept when another tool is
chosen. The marks are numbered one and two with their notes, and the second was pending as number two. This scenario is
tested by `AnnotationOverlayViewModel_Note_NextShapeOrOtherToolChosen_KeepsPendingMarkWithTypedNote`.

**AnnotationOverlayViewModel_CancelCommand_OnlyWhileShapeUnderConstruction**: The cancel command can run only while a
shape is drawn or waits for its note, and the note commands only while it waits. This scenario is tested by
`AnnotationOverlayViewModel_CancelCommand_OnlyWhileShapeUnderConstruction`.

**AnnotationOverlayViewModel_MarkAdded_Raised_MarkIsAlreadyInRegistryAndStateIsClean**: When the event is raised, the
mark is found in the registry and the overlay has no shape and no note. This scenario is tested by
`AnnotationOverlayViewModel_MarkAdded_Raised_MarkIsAlreadyInRegistryAndStateIsClean`.

**AnnotationOverlayViewModel_Stroke_PictureReplacedWithSamePixelsBeforeNote_MarkKeepsPictureOfFirstPress**: The same
pixels are drawn again as a new picture object while the note waits; the mark holds the first picture. This scenario is
tested by `AnnotationOverlayViewModel_Stroke_PictureReplacedWithSamePixelsBeforeNote_MarkKeepsPictureOfFirstPress`.

**AnnotationOverlayViewModel_Escape_DuringStrokeNoteAndIdle_CancelsThenLeavesTheTool**: Escape cancels a shape being
drawn, cancels a shape waiting for its note, then leaves the tool, then has no effect; no mark is made and the switch
for dragging out was told `false` and then `true`. This scenario is tested by
`AnnotationOverlayViewModel_Escape_DuringStrokeNoteAndIdle_CancelsThenLeavesTheTool`.

**AnnotationOverlayViewModel_SetActiveMarkIds_NewPicture_HidesMarksOfEarlierPictureAndCountsThem**: Marks are made on
two pictures of one file and a third mark has no chip. With the chips of the first two, the first picture shows mark one
and counts one hidden, the second picture shows mark two and counts one hidden, and the first picture shows mark one
again; the registry keeps all three. This scenario is tested by
`AnnotationOverlayViewModel_SetActiveMarkIds_NewPicture_HidesMarksOfEarlierPictureAndCountsThem`.

**AnnotationOverlayViewModel_SetActiveMarkIds_SamePixelsAgainOrNoPicture_ShowsOrHidesAllMarks**: The same pixels drawn
again show both marks; with no picture none are shown and the text counts two hidden marks in the plural. This scenario
is tested by `AnnotationOverlayViewModel_SetActiveMarkIds_SamePixelsAgainOrNoPicture_ShowsOrHidesAllMarks`.

**AnnotationOverlayViewModel_SelectTool_ToolThenNone_TurnsDragOutOffThenOn**: Choosing the rectangle, the same tool
again, the arrow, and no tool tells the switch `false`, `false`, `true` only when the tool changed. This scenario is
tested by `AnnotationOverlayViewModel_SelectTool_ToolThenNone_TurnsDragOutOffThenOn`.

**AnnotationOverlayViewModel_Dispose_ToolChosen_AllowsDragOutAndStopsFollowing**: Disposing with a tool chosen tells the
switch `true` once, and later pictures change nothing. This scenario is tested by
`AnnotationOverlayViewModel_Dispose_ToolChosen_AllowsDragOutAndStopsFollowing`.

**AnnotationOverlayViewModel_ToolFlags_SetFromCheckBoxes_ChooseAndRevert**: Checking a tool's toggle flag chooses it and
unchecks the others; clearing the check of the chosen tool chooses the pointer; clearing the check of the pointer is
announced again so that the button goes back; an undefined tool throws. This scenario is tested by
`AnnotationOverlayViewModel_ToolFlags_SetFromCheckBoxes_ChooseAndRevert`.

**AnnotationOverlayViewModel_ClearMarksCommand_OnlyWithMarks_AsksTheHost**: With no mark in the message `HasMarks` is
false. After a mark is made and its chip is announced, `HasMarks` is true and the command can run; using it raises
`ClearMarksRequested` once; when the chip is gone `HasMarks` is false again. This scenario is tested by
`AnnotationOverlayViewModel_ClearMarksCommand_OnlyWithMarks_AsksTheHost`.

**AnnotationOverlayViewModel_SelectTool_NoPicture_StaysOnPointerAndSaysWhy** and
**AnnotationOverlayViewModel_SelectTool_DrawingNotAllowed_IsRefusedAndExplained**: A tool is refused while no picture is
shown and while the host does not allow marks, with the reason in the status text. These scenarios are tested by
`AnnotationOverlayViewModel_SelectTool_NoPicture_StaysOnPointerAndSaysWhy` and
`AnnotationOverlayViewModel_SelectTool_DrawingNotAllowed_IsRefusedAndExplained`.

**AnnotationOverlayViewModel_PictureChanged_WhileDrawing_CancelsOnlyForOtherPixels**,
**AnnotationOverlayViewModel_PictureCleared_ShapeIsThrownAwayAndToolWaitsForNextPicture**, and
**AnnotationOverlayViewModel_IsDrawingAllowed_TurnedOffWhileNoteWaits_ThrowsShapeAwayAndGivesUpTool**: A shape survives
the same pixels drawn again and is thrown away for other pixels; a missing picture throws the shape away and keeps the
tool, and drawing works on the next picture; a host that stops allowing marks throws a waiting shape away and gives up
the tool. These scenarios are tested by the three tests of these names.

**AnnotationOverlayViewModel_Keyboard_ArrowKeysAndEnter_DrawRectangle**,
**AnnotationOverlayViewModel_Keyboard_NoToolOrNoteWaiting_DoesNothingAndCursorStaysInPicture**, and
**AnnotationOverlayViewModel_Keyboard_FreehandMovesAddPointsAndHideCursorStops**: The cursor starts in the middle of the
picture, moves in steps, makes a rectangle from (92, 50) to (133, 90) with Enter, stays at the corner when pushed far
out, is hidden by the pointer, does nothing without a tool or while a note waits, and extends a freehand line with each
move. These scenarios are tested by the three tests of these names.

**AnnotationOverlayViewModel_New_NullArguments_Throw**,
**AnnotationOverlayViewModel_StatusText_EachState_TellsWhatToDo**, and
**AnnotationOverlayViewModel_ToolName_EachTool_IsAWord**: Missing parts are refused, each state has its own sentence,
and each tool has a name. These scenarios are tested by the three tests of these names.

**AnnotationOverlayStyle_Values_EqualThoseOfMarkImageComposer**: The colors of marks one to eight and the stroke width,
badge radius, and badge place for four picture sizes equal those of `MarkImageComposer`. This scenario is tested by
`AnnotationOverlayStyle_Values_EqualThoseOfMarkImageComposer`.

**AnnotationOverlayView_DragWithEachTool_DrawsDraftThenMakesMarkChipInPicturePixels**: A picture of 800 by 400 pixels is
scaled to the pane. For each tool the button is clicked, the pointer is pressed on the picture, moved, and released, and
the note is skipped. During the press there is one draft shape; at the release the note panel is shown and the draft is
still drawn; afterwards there is one more mark with the next number and the kind of the tool, its first and last points
are the pixels that were pointed at, its chip is in the composer, and it is drawn as a shape and a badge with its
number. This scenario is tested by `AnnotationOverlayView_DragWithEachTool_DrawsDraftThenMakesMarkChipInPicturePixels`.

**AnnotationOverlayView_DragRectangle_FitToWindowOff_CommitsSamePixelCoordinates**: With Fit to window cleared the layer
is at scale one and the same drag gives the same pixels. This scenario is tested by
`AnnotationOverlayView_DragRectangle_FitToWindowOff_CommitsSamePixelCoordinates`.

**AnnotationOverlayView_NoteTyped_EnterMakesMarkWithNoteAndFocusIsInNote**: After a shape the note box has the focus;
text input and Enter make the mark with the note, the note panel goes away, the box limits the length to 200, and the
mark's name for a screen reader holds the note. This scenario is tested by
`AnnotationOverlayView_NoteTyped_EnterMakesMarkWithNoteAndFocusIsInNote`.

**AnnotationOverlayView_NoteCard_UsesThePrimaryAndSecondaryButtonsAndTheCardRadius**: While a finished shape waits for
its note in the dark theme, *Add mark* is filled with the accent color with the text on the accent, *Skip note* has the
raised fill and the strong outline, both are at least 32 high, and the card has the corner radius of the other cards
(14). This scenario is tested by `AnnotationOverlayView_NoteCard_UsesThePrimaryAndSecondaryButtonsAndTheCardRadius`.

**AnnotationOverlayView_NoteButtons_AddMarkOrSkipNote**: *Add mark* keeps a typed note and *Skip note* drops one. This
scenario is tested by `AnnotationOverlayView_NoteButtons_AddMarkOrSkipNote`.

**AnnotationOverlayView_Escape_DuringDragAndWhileNote_MakesNoMark**: Escape with the button down and at the note throws
the shape away, with no draft, note, mark, or chip left, and the tool stays chosen. This scenario is tested by
`AnnotationOverlayView_Escape_DuringDragAndWhileNote_MakesNoMark`.

**AnnotationOverlayView_ClickWithoutDrag_MakesNoMark**: A click makes no mark and the status sentence says so. This
scenario is tested by `AnnotationOverlayView_ClickWithoutDrag_MakesNoMark`.

**AnnotationOverlayView_PointerLeavesPicture_StrokeContinuesAndIsClampedToPicture**: A rectangle released beyond the
lower right corner of the window ends at the corner of the picture, because the layer holds the pointer. This scenario
is tested by `AnnotationOverlayView_PointerLeavesPicture_StrokeContinuesAndIsClampedToPicture`.

**AnnotationOverlayView_ToolChosen_PictureDragOutIsOffAndPointerDragsPictureOut**: With a drag operation that counts its
starts, a drag with the rectangle tool draws a mark and starts no drag out, and after the pointer is chosen the same
drag starts exactly one drag out and makes no mark; dragging out was on, off, and on again. This scenario is tested by
`AnnotationOverlayView_ToolChosen_PictureDragOutIsOffAndPointerDragsPictureOut`.

**AnnotationOverlayView_FileChanged_HidesMarkOfEarlierPictureAndSaysSo**: A mark is drawn on a blue picture. After the
file draws green the mark is not drawn and the sentence says one mark is hidden; a second mark is number two; after the
file draws blue again only the first mark is shown; the composer holds both chips and the registry both marks. This
scenario is tested by `AnnotationOverlayView_FileChanged_HidesMarkOfEarlierPictureAndSaysSo`.

**AnnotationOverlayView_ChipUndoAndRedo_HidesAndShowsTheMark**: Undo of the composer removes the chip and the drawn mark
and badge, and redo brings them back. This scenario is tested by
`AnnotationOverlayView_ChipUndoAndRedo_HidesAndShowsTheMark`.

**AnnotationOverlayView_Keyboard_ArrowKeysAndEnter_DrawRectangleMark**: With the layer focused and the rectangle tool,
two presses of the right arrow show the cursor, Enter starts the shape at (416, 200), Control with the right arrow and
the down arrow move it, Enter finishes it, the note box takes the focus, and Enter makes the mark from (416, 200) to
(456, 208) with its chip. This scenario is tested by
`AnnotationOverlayView_Keyboard_ArrowKeysAndEnter_DrawRectangleMark`.

**AnnotationOverlayView_EscapeWithoutShape_GivesUpToolAndLayerTakesKeyboardOnlyWithTool**: The layer is not focusable
without a tool and is with one; Escape on the focused layer chooses the pointer, checks its button, and allows dragging
out. This scenario is tested by `AnnotationOverlayView_EscapeWithoutShape_GivesUpToolAndLayerTakesKeyboardOnlyWithTool`.

**AnnotationToolbarView_ToolButtons_AreNamedCheckedWithTheToolAndOffWithoutPicture**: Every tool button has a name; they
are off with the reason `Show a picture to mark it.` in the status sentence while no picture is shown; choosing the
ellipse checks only its button; clicking it again chooses the pointer. This scenario is tested by
`AnnotationToolbarView_ToolButtons_AreNamedCheckedWithTheToolAndOffWithoutPicture`.

**AnnotationToolbarView_FloatingBar_HasTooltipsKeysAndStaysInsideNarrowStage**: On a window 360 wide with a picture
shown, the five tool buttons are named Select, Rectangle, Ellipse, Freehand, and Arrow, with the tooltips `Select  V`,
`Rectangle  R`, `Ellipse  E`, `Freehand  F`, and `Arrow  A`. With the focus on the stage, the key R chooses the
rectangle and checks its button. After a rectangle is drawn, so that the note card shows, the bar and the note card each
lie between the left and the right edge of the window. This scenario is tested by
`AnnotationToolbarView_FloatingBar_HasTooltipsKeysAndStaysInsideNarrowStage`.

**AnnotationToolbarView_ClearMarks_TakesTheMarkChipsOutOfTheMessage**: The *Clear marks* button is off while the message
holds no mark. After two marks are drawn their two chips are in the composer; using the button leaves no chip, no
visible mark, and the button is off again. This scenario is tested by
`AnnotationToolbarView_ClearMarks_TakesTheMarkChipsOutOfTheMessage`.

**AnnotationToolbarView_MessageCannotBeEdited_ToolsAreOffAndToolIsGivenUp**: When the composer loses its project the
tool buttons are off, the pointer is chosen, the status sentence says to open a project, and dragging out is allowed;
with a project again the buttons are on. This scenario is tested by
`AnnotationToolbarView_MessageCannotBeEdited_ToolsAreOffAndToolIsGivenUp`.

**AnnotationOverlayView_ShownMarks_AreLaidOutInPicturePixelsWithTheColorOfTheirNumber**: The layer is 800 by 400 in its
own coordinates; marks one and two have the lines of red and blue, their badges are centered on the top-left corners of
their boxes, and the second shape is named `Mark 2, rectangle`. This scenario is tested by
`AnnotationOverlayView_ShownMarks_AreLaidOutInPicturePixelsWithTheColorOfTheirNumber`.

**AnnotationOverlayViewModel_BeginEditNote_MarkOfMessage_OpensCardWithItsNumberAndNote**: With two marks of the message,
the note of mark two is opened with no tool chosen: the card is open for that mark with its note, the box is named
`Note for mark 2`, the primary button says *Save note*, Cancel can run and Skip cannot, and no shape is drawn. This
scenario is tested by `AnnotationOverlayViewModel_BeginEditNote_MarkOfMessage_OpensCardWithItsNumberAndNote`.

**AnnotationOverlayViewModel_BeginEditNote_UnknownOutsideMessageWhileDrawingOrNotAllowed_IsRefused**: An unknown mark, a
mark whose chip is not in the message, a request while a shape is drawn, while marks are not allowed, and without a
picture are refused, and no note changes. This scenario is tested by
`AnnotationOverlayViewModel_BeginEditNote_UnknownOutsideMessageWhileDrawingOrNotAllowed_IsRefused`.

**AnnotationOverlayViewModel_EditNote_Committed_ChangesOnlyTheNoteAndReportsNoNewMark**: A changed note with a line
break is confirmed; the registry holds the same mark with the cleaned note, the shown marks are announced again with it,
no mark is reported, and the card is closed. This scenario is tested by
`AnnotationOverlayViewModel_EditNote_Committed_ChangesOnlyTheNoteAndReportsNoNewMark`.

**AnnotationOverlayViewModel_EditNote_CanceledOrEscaped_KeepsTheNoteAndTheMark**: A changed note is canceled with the
command, with Escape, and after a skip that does nothing; the card closes, the tool stays, and the mark keeps its note.
This scenario is tested by `AnnotationOverlayViewModel_EditNote_CanceledOrEscaped_KeepsTheNoteAndTheMark`.

**AnnotationOverlayViewModel_EditNote_NextShapeOtherToolOrOtherMark_SavesTypedNote**: A typed note is saved when a shape
is started, when another tool is chosen, and when the note of another mark is opened, which then opens with its own
note. This scenario is tested by `AnnotationOverlayViewModel_EditNote_NextShapeOtherToolOrOtherMark_SavesTypedNote`.

**AnnotationOverlayViewModel_EditNote_MarkLeavesMessageOrDrawingNotAllowed_GivesUpTheEdit**: The change goes on while
another chip leaves the message, and is given up without a change when the mark's own chip leaves or marks are no
longer allowed. This scenario is tested by
`AnnotationOverlayViewModel_EditNote_MarkLeavesMessageOrDrawingNotAllowed_GivesUpTheEdit`.

**AnnotationOverlayViewModel_EditNote_NewPictureOrPictureLost_GoesOnOrGivesUpTheEdit**: While a changed note is open, a
new picture of the file is shown and the card stays open for the mark with the typed note; then no picture is shown, as
when the file can no longer be drawn, and the change is given up with the note unchanged. This scenario is tested by
`AnnotationOverlayViewModel_EditNote_NewPictureOrPictureLost_GoesOnOrGivesUpTheEdit`.

**AnnotationOverlayStyle_NoteLabel_StaysInsidePictureOnEitherSideOfTheBadge**: On a picture of 800 by 600 the label of
a mark near the left is to the right of its badge, at its height, at the widest allowed and inside the picture; the
label of a mark near the right edge flips to the left and stays inside; on a picture 30 wide the width is zero or more
and inside; the size of the text stays from 8 to 18. This scenario is tested by
`AnnotationOverlayStyle_NoteLabel_StaysInsidePictureOnEitherSideOfTheBadge`.

**AnnotationOverlayView_MarkWithNote_ShowsOneLineLabelBesideItsBadge**: A mark with the note `door is blocked` has one
label with that text on one line, set to trim with an ellipsis, to the right of the badge at its top and height, inside
the picture, and named `Note of mark 1: door is blocked`. This scenario is tested by
`AnnotationOverlayView_MarkWithNote_ShowsOneLineLabelBesideItsBadge`.

**AnnotationOverlayView_LongNoteNearEdge_IsCutWithEllipsisInsidePictureAndNoteLessMarkHasNoLabel**: A long note on a
mark near the right edge is drawn left of its badge, on one line that is cut short, inside the picture and no wider than
the cap; a mark without a note has no label. This scenario is tested by
`AnnotationOverlayView_LongNoteNearEdge_IsCutWithEllipsisInsidePictureAndNoteLessMarkHasNoLabel`.

**AnnotationOverlayView_NoteLabel_UsesThemeColorsInDarkAndLight**: In the dark and the light theme the label has the
overlay surface of the theme behind the primary text color of the theme. This scenario is tested by
`AnnotationOverlayView_NoteLabel_UsesThemeColorsInDarkAndLight`.

**AnnotationOverlayView_NoteChanged_LabelShowsTheNewNote**: A note changed in the registry is drawn at once, and a note
taken away leaves no label and the badge. This scenario is tested by
`AnnotationOverlayView_NoteChanged_LabelShowsTheNewNote`.

**AnnotationOverlayView_MarkWithNote_BadgeLineAndLabelHaveTheWholeNoteAsTooltip**: The badge, the line, and the label of
a mark with a note of 150 characters answer the pointer and have `Mark 1: <the whole note>` as their tooltip; a mark
without a note has none. This scenario is tested by
`AnnotationOverlayView_MarkWithNote_BadgeLineAndLabelHaveTheWholeNoteAsTooltip`.

**AnnotationOverlayView_DoubleClickMarkWithSelect_OpensNoteCardFilledWithFocusInNote**: With the Select tool, two quick
clicks on the badge open the note card for that mark, with its note in the box, the caret at the end, the focus in it,
the name `Note for mark 1`, *Cancel* in place of *Skip note*, and *Save note*; no shape is drawn and no tool is chosen.
This scenario is tested by `AnnotationOverlayView_DoubleClickMarkWithSelect_OpensNoteCardFilledWithFocusInNote`.

**AnnotationOverlayView_EditNote_EnterSavesAndEscapeKeepsTheMark**: A double-click on the note label, typing, and Enter
save the longer note on the same mark and the label shows it; a double-click on the line, typing, and Escape close the
card with the note, the mark, and its chip unchanged. This scenario is tested by
`AnnotationOverlayView_EditNote_EnterSavesAndEscapeKeepsTheMark`.

**AnnotationOverlayView_EditNote_ButtonsSaveOrCancelAndFocusGoesBack**: With the focus on the stage, the note opened as
the host does takes the focus; *Save note* saves the typed note and *Cancel* keeps it, and each time the focus comes
back to the stage. This scenario is tested by `AnnotationOverlayView_EditNote_ButtonsSaveOrCancelAndFocusGoesBack`.

**AnnotationOverlayView_EditNote_ShapeStarted_FocusStaysOnTheLayer**: With the rectangle tool and the focus on the
stage, the note of a mark is opened as the host does and changed; a press and drag on the picture starts a shape, which
saves the typed note and closes the card, and while the shape is drawn the layer keeps the focus instead of the stage.
This scenario is tested by `AnnotationOverlayView_EditNote_ShapeStarted_FocusStaysOnTheLayer`.
