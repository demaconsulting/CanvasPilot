### PayloadInspector

#### Verification Approach

`PayloadInspector` is verified with unit tests in
`test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/PayloadInspectorTests.cs` for the view models and with headless
Avalonia UI tests in `test/DemaConsulting.CanvasPilot.UiTests/PayloadInspectorViewTests.cs` for the window. The unit
tests make payloads by hand from the real payload types (with bytes that are not pictures, because those tests need no
graphics platform); the UI tests make payloads whose pictures are real PNG files, show the real `PayloadInspectorView`,
and compare what it shows with what the view model and the payload hold. One UI test composes the real main window and
has it show the inspector through the launcher seam. The text is compared with the output of the real
`PayloadDescriber`, never with a copy of its format.

#### Test Environment

The standard test runner, and for the window the headless Avalonia platform with the Skia back end and the application's
own `App`. No window is shown on a screen. The look of the window on a real screen (the monospaced text, the proportions
of the pictures) is checked by hand with the checks of the `Chat` subsystem.

#### Acceptance Criteria

- All unit tests and UI tests of the unit pass with zero failures.
- The description is exactly the output of the payload describer for the payload, and the window shows it as read-only,
  selectable text.
- The pictures are the annotated picture of each marked file followed by the area around each of its marks, in order,
  each with a caption that names what it is and each decoded from the payload's bytes; a message without marks shows
  none and says so.
- Close closes the window and releases the decoded pictures, however the window is closed; releasing twice is harmless;
  bytes that are not a picture give no picture and no failure.
- The main window shows the inspector as a dialog that it owns, and closing the dialog completes the request.

#### Test Scenarios

**PayloadInspectorViewModel_New_NullPayload_Throws**: A missing payload or caption is refused. This scenario is tested
by `PayloadInspectorViewModel_New_NullPayload_Throws`.

**PayloadInspectorViewModel_Description_IsExactlyWhatThePayloadDescriberWrites**: For a payload with two marks the
description equals the describer's output and contains the text with `[Mark 1] [Mark 2]`, the note, and the reason that
the picture cannot say what lies under a mark. This scenario is tested by
`PayloadInspectorViewModel_Description_IsExactlyWhatThePayloadDescriberWrites`.

**PayloadInspectorViewModel_Pictures_AnnotatedPictureThenEachMarkCrop_WithCaptions**: The pictures are the annotated
picture and the areas of the two marks, in order, with captions that name the file, the format, the size, the mark
numbers, and the area, and each has the byte count of the payload's picture. This scenario is tested by
`PayloadInspectorViewModel_Pictures_AnnotatedPictureThenEachMarkCrop_WithCaptions`.

**PayloadInspectorViewModel_Pictures_MessageWithoutMarks_HasNone**: A text-only payload has no pictures and its text is
described. This scenario is tested by `PayloadInspectorViewModel_Pictures_MessageWithoutMarks_HasNone`.

**PayloadInspectorViewModel_CloseCommand_RaisesCloseRequested**: The close command raises one close request. This
scenario is tested by `PayloadInspectorViewModel_CloseCommand_RaisesCloseRequested`.

**PayloadPictureViewModel_Bitmap_BytesAreNotAPicture_IsNullAndDisposeTwiceIsHarmless**: Bytes that are not a picture
give no bitmap, disposing twice does nothing, and the caption is kept. This scenario is tested by
`PayloadPictureViewModel_Bitmap_BytesAreNotAPicture_IsNullAndDisposeTwiceIsHarmless`.

**PayloadInspectorView_Shown_ShowsTheDescriptionAsReadOnlyText**: The window shows the description in a text box that
cannot be edited, with the title `Payload`. This scenario is tested by
`PayloadInspectorView_Shown_ShowsTheDescriptionAsReadOnlyText`.

**PayloadInspectorView_Shown_ShowsAnnotatedPictureAndEachMarkCropWithCaptions**: The window shows three captions, equal
to those of the view model, and three decoded pictures, and not the note about missing pictures. This scenario is tested
by `PayloadInspectorView_Shown_ShowsAnnotatedPictureAndEachMarkCropWithCaptions`.

**PayloadInspectorView_Shown_MessageWithoutMarks_SaysItHasNoPictures**: For a payload without marks the window shows the
note and no picture. This scenario is tested by `PayloadInspectorView_Shown_MessageWithoutMarks_SaysItHasNoPictures`.

**PayloadInspectorView_CloseButton_ClosesWindowAndReleasesPictures**: Pressing Close closes the window, and the picture
that was decoded is released. This scenario is tested by
`PayloadInspectorView_CloseButton_ClosesWindowAndReleasesPictures`.

**MainWindowView_ShowPayloadInspector_ShowsDialogOwnedByTheWindow**: The composed main window is asked to show an
inspector; a dialog owned by the window shows the description, and closing it completes the request. This scenario is
tested by `MainWindowView_ShowPayloadInspector_ShowsDialogOwnedByTheWindow`.
