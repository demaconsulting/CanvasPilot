### ComposerEditor

#### Verification Approach

`ComposerEditor` is verified with headless Avalonia UI tests in `test/DemaConsulting.CanvasPilot.UiTests`
(`ComposerEditorViewTests.cs` for the view, `ComposerEditorSupportTests.cs` for the reader of drops and the clipboard,
the preview provider, the chip control, the dictated ranges, and the rules for refused characters and copied text, and
`ComposerUiTestSupport.cs` for the harness). The tests use the real `ComposerEditorView` with the real
`ComposerViewModel`, `ComposerDocument`, `ComposerHistory`, `AttachmentIntake`, `AttachmentStore`, and `MarkRegistry`
over a temporary project folder, and the real AvaloniaEdit editing surface, because the unit exists to connect them and
the rule under test is that the surface and the message agree. Only what has no place in a view test is replaced:
`IdleDictationService` (always ready, never listens), `SimpleMessageBuilder`, and `FixedThumbnailProvider` and a
throwing provider for the pictures of chips. Input is sent through the window as the platform delivers it: text input
for typing and for the commit of an input method, physical key presses (where Ctrl is the command key of the platform
that runs the tests: the Command key on macOS) for Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z, Backspace, Delete, Enter, Ctrl+C,
Ctrl+X, and Ctrl+V, pointer presses for the delete button of a chip, and drag events for drops. Files and folders for
drops come from the window's storage provider, pictures are bitmaps in data transfer items, and the clipboard is the
window's in-memory clipboard. After every operation the harness checks the central rule: the text of the surface equals
the flat text of the message, and the number of placeholder characters equals the number of chips.

#### Test Environment

The standard test runner with the headless Avalonia platform and the Skia back end; the application's own `App`, which
loads the style of the editing control. A temporary folder per test holds the project folder and the files that are
dropped, and is removed afterwards (a preview that is still being decoded in the background can hold a file for a
moment, so the removal retries). No window is shown on a screen, no real clipboard, no operating system drag and drop,
and no real input method is used.

**Checks by hand before a release** (the headless platform cannot show them; they are not evidence for a requirement):

1. *Input method.* With a Windows input method (for example Japanese or Chinese, or the emoji panel opened with the
   Windows key and the full stop), compose a word in the editing surface, commit it, and check that the committed text
   appears once, that one Ctrl+Z removes the whole commit, and that the surface still agrees with what is sent (view the
   payload).
2. *Real clipboard.* Take a screenshot with the Snipping Tool and paste it with Ctrl+V; copy files in the file manager
   and paste them; copy text from a browser and paste it: each becomes one chip or one piece of text and one undo step.
3. *Real drag and drop.* Drag a file, a folder, several files, a picture from a browser, and selected text from another
   program onto the surface: the pointer shows the copy effect over the surface, the highlight shows, and each drop is
   one step.
4. *Looks.* Chips are readable and aligned with the text in the light and the dark theme, long names are shortened with
   an ellipsis, the dictated background is visible but does not hide the text, and the surface wraps long lines with
   chips in them. A chip is a pill; a mark chip is tinted pink with a pink outline and icon like the mark on the
   picture; the cross of a chip is announced by a screen reader as "Remove" and the name of the chip; the placeholder
   is readable in both themes and disappears when typing starts.

#### Acceptance Criteria

- All UI tests of the unit pass with zero failures.
- After every operation the surface and the message hold the same text.
- Each typed character, each Backspace or Delete, each commit of several characters, each replacement of a selection,
  and each line break is one undo step, and Ctrl+Z, Ctrl+Y, and Ctrl+Shift+Z undo and redo through the view model while
  the surface keeps no history of its own.
- A typed placeholder character never enters; dropped text with a placeholder or a control character is cleaned.
- A chip added through the view model, by a drop, or by a paste is drawn inline in the order of the message with its
  name, icon or picture, and tooltip; deleting a chip with its button, Backspace, or Delete is one step that undo
  restores in the same place; a chip that takes the place of another is drawn.
- A drop of files, folders, a picture, or text, and a paste of the same from the clipboard, become the chips and text
  that the message accepts, each as one step, with files taken before a picture and a picture before text; nothing is
  opened; nothing is changed while no project is open.
- Copy and cut put readable text with names in brackets on the clipboard, never the placeholder character, and a cut is
  one step.
- Dictated text has its range drawn and only that, and the range disappears when the block is undone.
- Previews are made only for picture files of at most 20 MB, are reused until the file changes, and are never made for
  anything else; a failing provider leaves the icon.
- While the message of an open project is empty the surface shows the placeholder text, and typing hides it.
- The tooltip of a mark's chip is `Mark n on the picture`, followed by `: <note>` when the mark has a note, and follows
  a change of the note; a double-click on a mark chip of the message, its Edit note item (named and showing F2), and F2
  with the caret beside it ask the view model for that mark, with no selection and no change of the message; chips of
  other kinds and chips of sent messages offer no change of a note.
- The help text of every chip is its tooltip, and the text of the message that assistive technology reads gives a mark
  with a note as `[Mark n: note]` and every other chip as `[label]`, following a change of the note.
- The Edit note item of a mark's chip is disabled while the message is being sent and enabled again when sending was
  canceled; a chip whose offer cannot be used asks nothing on a double-click or its item.

#### Test Scenarios

**ComposerEditorView_TypeCharacters_EachCharacterIsOneStepUndoneByCtrlZ**: Three characters are typed one by one; each
Ctrl+Z removes one, and the surface equals the message after each operation. This scenario is tested by
`ComposerEditorView_TypeCharacters_EachCharacterIsOneStepUndoneByCtrlZ`.

**ComposerEditorView_RedoKeys_CtrlYAndCtrlShiftZ_RedoOneStepEach**: After three steps were undone, Ctrl+Y and then
Ctrl+Shift+Z each bring back one character, with the caret after it. This scenario is tested by
`ComposerEditorView_RedoKeys_CtrlYAndCtrlShiftZ_RedoOneStepEach`.

**ComposerEditorView_BuiltInUndo_IsSwitchedOffAndItsCommandsRemoved**: After the draft is cleared from outside, Ctrl+Z
does not bring the text back from a history of the surface, which has none. This scenario is tested by
`ComposerEditorView_BuiltInUndo_IsSwitchedOffAndItsCommandsRemoved`.

**ComposerEditorView_TypePlaceholderCharacter_IsRefused**: The placeholder character is typed, then a letter; only the
letter enters and there is no chip. This scenario is tested by `ComposerEditorView_TypePlaceholderCharacter_IsRefused`.

**ComposerEditorView_InputMethodCommit_IsOneStep**: Five characters are committed at once; one Ctrl+Z removes them all.
This scenario is tested by `ComposerEditorView_InputMethodCommit_IsOneStep`.

**ComposerEditorView_BackspaceAndDelete_EachCharacterIsOneStep**: Backspace removes the character before the caret and
Delete the one after it, each as one step, and pressing Ctrl+Z twice puts both back. This scenario is tested by
`ComposerEditorView_BackspaceAndDelete_EachCharacterIsOneStep`.

**ComposerEditorView_TypeOverSelection_IsOneStep**: One character typed over a selection of three replaces it as one
step, and one undo restores the selected text. This scenario is tested by
`ComposerEditorView_TypeOverSelection_IsOneStep`.

**ComposerEditorView_EnterKey_KeepsSurfaceAndMessageEqual**: Enter after an indented line, typing on the new line, and
pressing Ctrl+Z twice keeps the surface equal to the message, which holds line feeds only. This scenario is tested by
`ComposerEditorView_EnterKey_KeepsSurfaceAndMessageEqual`.

**ComposerEditorView_AddChip_IsDrawnInlineBetweenTexts**: A file dropped through the view model between two letters is
drawn as a chip control with its name, its automation identifier, and its path as the tooltip. This scenario is tested
by `ComposerEditorView_AddChip_IsDrawnInlineBetweenTexts`.

**ComposerEditorView_ChipDeleteButton_RemovesChipAndUndoRestoresIt**: A real mouse click on the delete button removes
the chip, and Ctrl+Z draws it again between the letters. This scenario is tested by
`ComposerEditorView_ChipDeleteButton_RemovesChipAndUndoRestoresIt`.

**ComposerEditorView_BackspaceAndDeleteOverChip_RemoveChipAsOneStep**: Backspace after a chip and Delete before another
each remove one chip as one step, and pressing Ctrl+Z twice brings them back. This scenario is tested by
`ComposerEditorView_BackspaceAndDeleteOverChip_RemoveChipAsOneStep`.

**ComposerEditorView_ChipReplacesChip_DrawsTheNewChip**: A selected chip is replaced by a pasted file; the text of the
surface does not change, and the chip that is drawn is the new one. This scenario is tested by
`ComposerEditorView_ChipReplacesChip_DrawsTheNewChip`.

**ComposerEditorView_AddMarkThenType_ChipIsOnItsOwnLineAboveTheWords**: With words typed, two marks are added through
the view model. The message is the two chips, a line feed, and the words; both chips are drawn at the same height on the
first line, the caret, at the end of the words, is below the bottom of the chips, and the surface equals the message.
This scenario is tested by `ComposerEditorView_AddMarkThenType_ChipIsOnItsOwnLineAboveTheWords`.

**ChatPanelView_EmptyMessageMarkWithNoteThenClickAndType_ChipRowIsSeparateFromTheWords**: With an empty message a
rectangle is drawn and added with the note `this one` through the annotation layer; the message read by assistive
technology is `[Mark 1: this one]` and a line feed and the caret is on the line below. A click on the row of the chip
puts the caret at the end of the row, and the words that are then typed are read as `[Mark 1: this one]`, a line feed,
and the words, and are drawn on a line below the chip. This scenario is tested by
`ChatPanelView_EmptyMessageMarkWithNoteThenClickAndType_ChipRowIsSeparateFromTheWords`.

**ComposerEditorView_AutomationPeer_IsANamedEditFieldWithTheTextOfTheMessage**: For a message with words and a mark chip
the automation peer of the editing surface is an edit field named `Message` whose value is `[Mark 1]` and the words on
the line below, that is not read-only; setting its value replaces the message and the surface follows; the text area has
the name `Message` too. This scenario is tested by
`ComposerEditorView_AutomationPeer_IsANamedEditFieldWithTheTextOfTheMessage`.

**ComposerEditorView_AddMark_ShowsMarkChipAndUndoCommandRemovesIt**: A mark added through the view model is drawn as a
chip named `Mark 1`, and the undo command of the view model removes it from the surface. This scenario is tested by
`ComposerEditorView_AddMark_ShowsMarkChipAndUndoCommandRemovesIt`.

**ComposerEditorView_DropOfFilesAndPicture_FourChipsOneStep**: Content of three files and a picture handed to the view
model gives four chips, drawn, and one undo removes all of them. This scenario is tested by
`ComposerEditorView_DropOfFilesAndPicture_FourChipsOneStep`.

**ComposerEditorView_DropFiles_BecomesChipsInsteadOfPathText**: A real drop of a file, a folder, and a picture file on
the surface gives a file chip, a folder chip, and a picture chip, no path text, and one undo step. This scenario is
tested by `ComposerEditorView_DropFiles_BecomesChipsInsteadOfPathText`.

**ComposerEditorView_DropFilesWithPathText_IsChipsOnly**: A drop of a file that also carries its path as text gives one
chip and no text. This scenario is tested by `ComposerEditorView_DropFilesWithPathText_IsChipsOnly`.

**ComposerEditorView_DragOver_AcceptableAndNot_ShowsDropHighlightOnlyForAcceptable**: A drag over the text shows the
highlight for text and not for an empty transfer, and leaving hides it. This scenario is tested by
`ComposerEditorView_DragOver_AcceptableAndNot_ShowsDropHighlightOnlyForAcceptable`.

**ComposerEditorView_DropPictureAndText_BecomePictureChipAndText**: A dropped bitmap becomes a pasted-picture chip whose
file is saved in the project's private data folder, and dropped text becomes text. This scenario is tested by
`ComposerEditorView_DropPictureAndText_BecomePictureChipAndText`.

**ComposerEditorView_DropTextWithPlaceholder_IsCleaned**: Dropped text with a placeholder and a control character enters
without them. This scenario is tested by `ComposerEditorView_DropTextWithPlaceholder_IsCleaned`.

**ComposerEditorView_DropWithoutProject_ChangesNothing**: With no project, a dropped file adds nothing, the hint that a
project must be open is shown, and the surface is read-only. This scenario is tested by
`ComposerEditorView_DropWithoutProject_ChangesNothing`.

**ComposerEditorView_PasteAsync_FilesPictureAndText_EachIsOneStep**: Two files, a picture, and text are pasted from the
clipboard in turn, each as one step, and pressing Ctrl+Z three times takes them back. This scenario is tested by
`ComposerEditorView_PasteAsync_FilesPictureAndText_EachIsOneStep`.

**ComposerEditorView_CtrlV_PastesClipboardTextAsOneStep**: Ctrl+V with text on the clipboard inserts it at the caret as
one step through the message, and one undo removes it. This scenario is tested by
`ComposerEditorView_CtrlV_PastesClipboardTextAsOneStep`.

**ComposerEditorView_CtrlV_FileOnClipboard_MakesChip**: Ctrl+V with a file on the clipboard makes one chip and no text.
This scenario is tested by `ComposerEditorView_CtrlV_FileOnClipboard_MakesChip`.

**ComposerEditorView_CopyAndCut_PutReadableTextOnClipboard**: Ctrl+C of a selection with a chip puts `ab[copy.txt]cd` on
the clipboard, Ctrl+X empties the message, and undo brings it back as one step. This scenario is tested by
`ComposerEditorView_CopyAndCut_PutReadableTextOnClipboard`.

**ComposerEditorView_DictatedBlock_IsShownAndItsRangeIsDrawn**: A dictated block between typed text and a typed letter
has its range, and undoing the letter and the block removes the range. This scenario is tested by
`ComposerEditorView_DictatedBlock_IsShownAndItsRangeIsDrawn`.

**ComposerEditorView_ProjectOpenedAndClosed_EditableOnlyWhileProjectIsOpen**: Typing does nothing without a project,
works after one is opened, and closing the project empties the surface. This scenario is tested by
`ComposerEditorView_ProjectOpenedAndClosed_EditableOnlyWhileProjectIsOpen`.

**ComposerEditorView_UndoRedoTarget_IsTheViewModel**: The view and the surface give the view model as the target of undo
and redo, and a view with no view model gives none. This scenario is tested by
`ComposerEditorView_UndoRedoTarget_IsTheViewModel`.

**ComposerEditorView_StatusAndPartialText_AreShown**: A drop of a file that does not exist makes the status line visible
with the file's name, and the strip of live words is hidden while nobody dictates. This scenario is tested by
`ComposerEditorView_StatusAndPartialText_AreShown`.

**ComposerEditorView_ThumbnailProvider_PictureReplacesIcon**: A provider's picture replaces the icon of a chip, the
provider is asked for the chip, and the provider that was set is the one used. This scenario is tested by
`ComposerEditorView_ThumbnailProvider_PictureReplacesIcon`.

**ComposerEditorView_ManyChipsAndWrappedText_StayConsistentThroughUndoAll**: Ten words alternate with ten dropped files,
and every step is undone with the keyboard; the surface equals the message throughout and ends empty. This scenario is
tested by `ComposerEditorView_ManyChipsAndWrappedText_StayConsistentThroughUndoAll`.

**ComposerEditorView_ContainsForbiddenCharacter_Table_FollowsRule**: Plain text, tabs, and line breaks are allowed; the
placeholder and other control characters are refused. This scenario is tested by
`ComposerEditorView_ContainsForbiddenCharacter_Table_FollowsRule`.

**ComposerEditorView_DescribeForClipboard_TextAndChips_ReadableWithoutPlaceholder**: Text with a file chip and a mark
chip is described as `see [plan.docx][Mark 2] now`. This scenario is tested by
`ComposerEditorView_DescribeForClipboard_TextAndChips_ReadableWithoutPlaceholder`.

**DataTransferReader_Read_FilesAndFolder_ReturnsLocalPaths**: A drop of a file and a folder is read as their paths in
order and nothing else. This scenario is tested by `DataTransferReader_Read_FilesAndFolder_ReturnsLocalPaths`.

**DataTransferReader_Read_FilesWithText_OnlyFilesAreTaken**: A file with its path as text is read as the file only. This
scenario is tested by `DataTransferReader_Read_FilesWithText_OnlyFilesAreTaken`.

**DataTransferReader_Read_Bitmap_ReturnsPngBytes**: A dropped bitmap is read as the PNG bytes of a picture of the same
size. This scenario is tested by `DataTransferReader_Read_Bitmap_ReturnsPngBytes`.

**DataTransferReader_Read_TextAndEmpty_ReturnsTextOrNothing**: Dropped text is read as text, and an empty transfer as
nothing. This scenario is tested by `DataTransferReader_Read_TextAndEmpty_ReturnsTextOrNothing`.

**DataTransferReader_IsAcceptable_FilesPictureTextAndNothing**: A drag with files, a picture, or text is acceptable and
one with nothing is not. This scenario is tested by `DataTransferReader_IsAcceptable_FilesPictureTextAndNothing`.

**DataTransferReader_NullArguments_Throw**: Each entry point refuses null. This scenario is tested by
`DataTransferReader_NullArguments_Throw`.

**DataTransferReader_ReadAsync_Clipboard_FilesPictureTextAndEmpty**: The clipboard is read as nothing when empty, then
as text, a picture, and a file as each is put on it. This scenario is tested by
`DataTransferReader_ReadAsync_Clipboard_FilesPictureTextAndEmpty`.

**ImageChipThumbnailProvider_GetThumbnailAsync_PictureFile_ReturnsCachedPreviewUntilChanged**: A PNG file gets a
preview, the same preview again while unchanged, and a new one after it changed. This scenario is tested by
`ImageChipThumbnailProvider_GetThumbnailAsync_PictureFile_ReturnsCachedPreviewUntilChanged`.

**ImageChipThumbnailProvider_GetThumbnailAsync_NotAPictureMissingOrTooLarge_ReturnsNull**: A text file with a picture
name, a missing file, and a file over the limit have no preview and cause no failure. This scenario is tested by
`ImageChipThumbnailProvider_GetThumbnailAsync_NotAPictureMissingOrTooLarge_ReturnsNull`.

**ImageChipThumbnailProvider_GetThumbnailAsync_OtherChipsAndCancellation_ReturnNull**: File, folder, and mark chips and
a cancelled request have no preview, and a missing chip is refused. This scenario is tested by
`ImageChipThumbnailProvider_GetThumbnailAsync_OtherChipsAndCancellation_ReturnNull`.

**ChipView_Show_EachKindOfChip_ShowsLabelIconAndTooltip**: A chip of each kind shows its name, a distinct icon, and its
tooltip. This scenario is tested by `ChipView_Show_EachKindOfChip_ShowsLabelIconAndTooltip`.

**ChipView_DeleteButton_RaisesDeleteRequested**: Pressing the delete button of a chip raises one request, and the label
helper and `Show` refuse a missing chip. This scenario is tested by `ChipView_DeleteButton_RaisesDeleteRequested`.

**ChipView_ProviderThrows_KeepsIcon**: A provider that throws leaves the icon and breaks nothing. This scenario is
tested by `ChipView_ProviderThrows_KeepsIcon`.

**DictatedTextBackgroundRenderer_GetRanges_DictatedTextBetweenChipsAndTyped_ReturnsOnlyDictated**: Of typed text, a
chip, dictated text, typed text, and dictated text, only the two dictated stretches are reported, at offsets that count
the chip as one position. This scenario is tested by
`DictatedTextBackgroundRenderer_GetRanges_DictatedTextBetweenChipsAndTyped_ReturnsOnlyDictated`.

**ChatPanelView_ComposerCard_PlaceholderNamesAndHint**: This UI test of the chat panel hosts the real editing view in
the card of the message. A new panel in an open project shows the placeholder, whose text is the constant of the view
model,
over the empty surface, and typing "Make it blue" hides it. It is described in the verification of the `ChatPanel`
unit, and is the evidence for the placeholder because the view is tested here only without the card around it. This
scenario is tested by `ChatPanelView_ComposerCard_PlaceholderNamesAndHint`.

**ChipView_Show_MarkWithNote_TooltipAddsTheNote**: The chip of mark 2 shown with a note has the tooltip
`Mark 2 on the picture: the door is blocked`; shown with no note or an empty one it has `Mark 2 on the picture`; the
helper refuses a missing chip. This scenario is tested by `ChipView_Show_MarkWithNote_TooltipAddsTheNote`.

**ChipView_DoubleClickAndEditNoteItem_RaiseEditNoteRequestedOnlyForEditableMarkChips**: An editable mark chip, a mark
chip of a sent message, and an editable file chip are double-clicked in a window, and the Edit note item of the first is
chosen. Only the editable mark chip asks, twice; only it has the menu, whose item is `Edit note…`, shows F2, and is
named `Edit the note of Mark 1`; turning the change off takes the menu away. This scenario is tested by
`ChipView_DoubleClickAndEditNoteItem_RaiseEditNoteRequestedOnlyForEditableMarkChips`.

**ComposerEditorView_MarkNoteChanged_ChipTooltipShowsTheNewNote**: A mark chip with the note `old note` is drawn; the
note is changed in the registry and the chip, drawn again, says `Mark 1 on the picture: new note`, offers the change of
its note, and the message and what can be undone are as before. This scenario is tested by
`ComposerEditorView_MarkNoteChanged_ChipTooltipShowsTheNewNote`.

**ComposerEditorView_MarkChipDoubleClickedOrF2_AsksToEditTheNoteOfThatMark**: Two mark chips stand above the words
`hi`. Two quick clicks on the second chip ask for the second mark and select nothing; F2 right after the first chip asks
for the first mark; F2 in the words asks for nothing; the message does not change. This scenario is tested by
`ComposerEditorView_MarkChipDoubleClickedOrF2_AsksToEditTheNoteOfThatMark`.

**ChipView_NoteEditNotEnabled_ItemDisabledAndNothingAsked**: A mark chip that offers the change of its note, but not
now, is double-clicked in a window and its Edit note item, which is disabled, is chosen; nothing is asked. When the
offer can be used again the item is enabled. This scenario is tested by
`ChipView_NoteEditNotEnabled_ItemDisabledAndNothingAsked`.

**ChipView_Show_HelpTextIsTheTooltip**: The help text of a file chip is its path, and that of the chip of mark 2 is
`Mark 2 on the picture: the door is blocked` with that note and `Mark 2 on the picture` without, equal to the tooltip.
This scenario is tested by `ChipView_Show_HelpTextIsTheTooltip`.

**ComposerEditorView_DescribeForScreenReader_MarkWithNote_ReadsTheNoteAfterTheLabel**: Text, a mark with the note
`make it red`, a mark with an empty note, a file chip, and text are described as
`see [Mark 1: make it red][Mark 2][plan.docx] now`; a missing argument is refused. This scenario is tested by
`ComposerEditorView_DescribeForScreenReader_MarkWithNote_ReadsTheNoteAfterTheLabel`.

**ComposerEditorView_MarkWithNote_ScreenReaderReadsTheNote**: A chip of a mark with the note `make it red` and one of a
mark without a note stand above `hello`. The automation peer of the surface reads `[Mark 1: make it red][Mark 2]` and
the words, and the help texts of the chips are their tooltips; after the note is changed to `make it blue` both follow.
This scenario is tested by `ComposerEditorView_MarkWithNote_ScreenReaderReadsTheNote`.

**ComposerEditorView_MessageBeingSent_EditNoteItemIsDisabled**: A mark chip with a note stands above `go` in a message
whose sending is held by the builder. The Edit note item of the chip is enabled before sending, disabled while the
message is being sent, and enabled again after the sending was canceled, with the chip still in the message. This
scenario is tested by `ComposerEditorView_MessageBeingSent_EditNoteItemIsDisabled`.
