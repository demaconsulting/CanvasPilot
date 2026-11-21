### StyleKitEditor

#### Verification Approach

`StyleKitEditor` is verified with unit tests in `StyleKitEditorTests.cs` over a real `StyleKitCatalog` on temporary
folders (the kits are real folders and the example files are real files, so what a save does to the folder is read back
from the disk). The tests read the draft, the errors and warnings, the derived values, and the files and the style file
that a save leaves, and use a catalog that fails when the failure of a write is the subject. Headless UI tests in
`StyleKitEditorViewTests.cs` (the `UiTests` project, using Avalonia's headless platform with the Skia renderer) show the
real editor window and the real color control over a real catalog, choose tabs, type into boxes, click buttons with the
mouse, open the flyout of the sliders, and read the controls and the files. The window is shown without an owner there;
showing it modally over the window that manages kits is verified by the tests of that window. The platform picker of the
example files cannot be shown headless; the editor is tested with a picker that answers.

#### Test Environment

Unit tests use the standard test environment with temporary local folders and real file I/O. No network and no window.
UI tests run under the Avalonia headless platform, serialized with test parallelization disabled; no display or
windowing system is required.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The draft of a kit that has every part holds each field, builds the same kit, has no problem, and is not changed.
- A save writes every changed field and the style file read back from the folder equals the draft.
- A color that is not a color, an empty name, a number out of range, a font list that is too long, a Mermaid theme that
  does not exist, a Vega-Lite configuration that is not an object, is not JSON, or is too large, and too many lines of
  advice are errors with the message and the path that the reader of the files gives, and the save is not available
  until they are fixed.
- A font that is not bundled is a warning and the kit can be saved.
- The palette offers the next usual role, keeps the colors that are required, rejects a name that is used twice or
  missing, and the red, green, and blue values and the text agree, with values held to 0 to 255 and text that is not a
  color reading as 0.
- A size that has a number that is not a number says so once, a size with no side, a side out of range, a resolution out
  of range, and a name that is used twice are errors.
- Examples that are added and removed change no file until the save, the save copies the added files and deletes the
  removed ones and the style file names what is left with its captions, a name that is in use gets a number and no file
  is overwritten, a file that is missing or too large is not added and the reason is said, and a save that fails removes
  what it copied and keeps the editor open.
- Canceling leaves every file as it was.
- A kit that is read only and missing arguments are refused.
- The window has a tab for each part of a kit that shows it; Save is off while a color is not a color and the error is
  shown at the color and below the tabs, and Save is on again when it is fixed; Save writes the kit and closes the
  window and Cancel closes it and writes nothing; the buttons that add and remove an export size, a theme variable, a
  line of advice, and an example work, and a width that is not a number is shown as an error.
- The color control paints its swatch with the color of the text, shows the reason when the text is not a color, keeps
  the sliders of the flyout and the text in step in both directions, and does not let the name of a required role be
  changed.

#### Test Scenarios

**StyleKitEditor_Open_KitWithEveryPart_DraftHoldsEveryFieldAndIsClean**: A kit with five colors, three font lists, a
Mermaid theme with two variables, a Vega-Lite configuration, advice, two sizes, and two example files, one with a
caption, is opened; the draft holds each of them, builds a kit that is written as the same text, has no error and no
warning, is not changed, and says that the text reads well on the background. This scenario is tested by
`StyleKitEditor_Open_KitWithEveryPart_DraftHoldsEveryFieldAndIsClean`.

**StyleKitEditor_Save_EveryFieldChanged_IsWrittenAndReadBack**: Every part of the draft is changed, a color, a theme
variable, a line of advice, and a size are added, a line of advice is removed, and the kit is saved; the folder read
back has every new value, the editor reports the saved kit and asked to close once. This scenario is tested by
`StyleKitEditor_Save_EveryFieldChanged_IsWrittenAndReadBack`.

**StyleKitEditor_Validate_WrongValues_GiveTheMessagesOfTheReaderAndStopTheSave**: A color `#12` gives
`palette.primary: '#12' is not a color; use #RRGGBB.` and turns the save off; an empty name, a stroke of 40, a font list
of 130 characters, a theme `neon`, a configuration that is a list, text that is not JSON, one that is over the limit,
and twelve lines of advice each give the error of their property, and fixing each turns the save on again. This scenario
is tested by `StyleKitEditor_Validate_WrongValues_GiveTheMessagesOfTheReaderAndStopTheSave`.

**StyleKitEditor_Validate_FontThatIsNotBundled_IsAWarningAndKitCanBeSaved**: The heading font `Inter, sans-serif` gives
one warning that names `fonts.heading` and `Inter`, no error, and the save stays available. This scenario is tested by
`StyleKitEditor_Validate_FontThatIsNotBundled_IsAWarningAndKitCanBeSaved`.

**StyleKitEditor_Palette_AddRemoveRenameAndSliders_FollowTheRules**: Four colors are added and take `secondary`,
`surface`, `muted`, and `color-1`; a required color cannot be removed and an extra color can; a name that is used twice
and a blank name are errors; the red, green, and blue values read from the text, are written to it and held to 0 to 255,
and read as 0 when the text is not a color. This scenario is tested by
`StyleKitEditor_Palette_AddRemoveRenameAndSliders_FollowTheRules`.

**StyleKitEditor_ContrastText_LowAndHighContrast_AreSaid**: Text that is nearly the color of the background says that
the ratio is low; text that is not a color says nothing. This scenario is tested by
`StyleKitEditor_ContrastText_LowAndHighContrast_AreSaid`.

**StyleKitEditor_Mermaid_SwitchedOffAndVariableRows_FollowTheRules**: A blank variable is left out, a value with no name
and a name that is used twice are errors, and switching the part off leaves the kit with no Mermaid theme. This scenario
is tested by `StyleKitEditor_Mermaid_SwitchedOffAndVariableRows_FollowTheRules`.

**StyleKitEditor_Guidance_BlankLines_AreLeftOut**: A blank line added to each list of advice is in the draft and not in
the kit and is no error. This scenario is tested by `StyleKitEditor_Guidance_BlankLines_AreLeftOut`.

**StyleKitEditor_Presets_AddRemoveAndWrongNumbers_GiveMessages**: New sizes take free names and are valid; a width and a
resolution that are not numbers are each said once; a size with no side, a resolution of 5, a width of 9000, and a name
used twice (ignoring case) are errors; a fixed size is built as the kit expects. This scenario is tested by
`StyleKitEditor_Presets_AddRemoveAndWrongNumbers_GiveMessages`.

**StyleKitEditor_Examples_AddAndRemove_ChangeTheFilesOnlyWhenSaved**: One file is added with a caption and one example
is removed; the folder still has its two files and the editor is changed; after the save the folder has the first file
and the new one, the kit file names both with their captions, and the file that the user chose is still where it was.
This scenario is tested by `StyleKitEditor_Examples_AddAndRemove_ChangeTheFilesOnlyWhenSaved`.

**StyleKitEditor_Cancel_AfterEdits_LeavesTheDiskUntouched**: After changes, an added file, and a removed example, Cancel
leaves every file of the kit folder with its text, says that nothing was saved, and asks to close once. This scenario is
tested by `StyleKitEditor_Cancel_AfterEdits_LeavesTheDiskUntouched`.

**StyleKitEditor_Examples_NameCollisions_GetANumberAndNothingIsOverwritten**: Two files that have the name of an example
that stays are shown as `a-2.svg` and `a-3.svg` and, after the save, the folder has the original and both with their own
text. This scenario is tested by `StyleKitEditor_Examples_NameCollisions_GetANumberAndNothingIsOverwritten`.

**StyleKitEditor_Examples_MissingAndTooLargeFiles_AreNotAddedAndSaveKeepsTheRest**: A file that is not there and a file
one byte over the limit are not added and both reasons are said; a save after another change keeps the example file of
the kit. This scenario is tested by `StyleKitEditor_Examples_MissingAndTooLargeFiles_AreNotAddedAndSaveKeepsTheRest`.

**StyleKitEditor_AddExamplesCommand_PickerSuppliedOrMissing_AddsOrDoesNothing**: With no picker, with a picker that
cancels, and with a picker that chooses a file, the command adds nothing, nothing, and one new example with its text
saying it is new. This scenario is tested by
`StyleKitEditor_AddExamplesCommand_PickerSuppliedOrMissing_AddsOrDoesNothing`.

**StyleKitEditor_IsDirty_ChangeAndChangeBack_FollowsTheDraft**: A change makes the draft changed, changing it back does
not, and a configuration that is not JSON and a new line of advice do. This scenario is tested by
`StyleKitEditor_IsDirty_ChangeAndChangeBack_FollowsTheDraft`.

**StyleKitEditor_Save_CatalogFails_ShowsTheReasonAndRemovesTheCopies**: With a catalog whose write fails with a full
disk, the reason is shown, the file that the save had copied is gone, the editor did not close, and the kit's own
example is still there. This scenario is tested by `StyleKitEditor_Save_CatalogFails_ShowsTheReasonAndRemovesTheCopies`.

**StyleKitEditor_Constructor_ReadOnlyKitAndNullArguments_AreRefused**: A built-in kit is refused with a message that
says it is read only, and missing arguments of the editor and of each row are refused. This scenario is tested by
`StyleKitEditor_Constructor_ReadOnlyKitAndNullArguments_AreRefused`.

**StyleKitEditorView_Show_TabsShowEveryPartOfTheKit**: The window of a new kit has the tabs General, Palette, Type and
shape, Mermaid and Vega-Lite, Guidance, Sizes, and Examples; the first tab shows the name and the id; the palette tab
lists three colors with their text, the role boxes of the required colors are off, and the contrast hint says the text
reads well; the fonts and numbers tab shows the font and the line width and corner radius. This scenario is tested by
`StyleKitEditorView_Show_TabsShowEveryPartOfTheKit`.

**StyleKitEditorView_Save_BadColorThenFixed_IsDisabledThenEnabled**: Typing `#12` into the box of the primary color
turns Save off, shows one error below the tabs and the reason under the color; typing `#112233` turns Save on and hides
the errors. This scenario is tested by `StyleKitEditorView_Save_BadColorThenFixed_IsDisabledThenEnabled`.

**StyleKitEditorView_SaveAndCancelButtons_WriteOrLeaveTheKitAndClose**: In two windows over two kits, the name is
changed; clicking Save writes the new name and closes the window; clicking Cancel closes the window and the kit keeps
its name. This scenario is tested by `StyleKitEditorView_SaveAndCancelButtons_WriteOrLeaveTheKitAndClose`.

**StyleKitEditorView_Sizes_AddAndRemoveRow_ChangeThePresets**: Clicking Add size adds a row with the name `New size` and
the width 1280; a width of `wide` shows the error list; clicking Remove removes the row and the error. This scenario is
tested by `StyleKitEditorView_Sizes_AddAndRemoveRow_ChangeThePresets`.

**StyleKitEditorView_MermaidGuidanceAndExamples_RowsAreAddedAndRemoved**: The list of themes is hidden until the check
box is set; a theme variable is added and removed; a line is added to each list of advice and one is removed; a file
that the picker returns is listed as `logo.svg (new)`, gets a caption that the draft holds, and is removed. This
scenario is tested by `StyleKitEditorView_MermaidGuidanceAndExamples_RowsAreAddedAndRemoved`.

**ColorEditorView_Hex_SwatchFollowsTheTextAndErrorIsShown**: The swatch has the color of the text, follows a new text,
shows no color and the reason for text that is not a color, and the role box of a required color is off. This scenario
is tested by `ColorEditorView_Hex_SwatchFollowsTheTextAndErrorIsShown`.

**ColorEditorView_Sliders_StayInStepWithTheText**: The flyout of the control opens with sliders at the values of the
text; moving two sliders writes the new text; typing a new text moves the sliders; the role box of a color that is not
required is on. This scenario is tested by `ColorEditorView_Sliders_StayInStepWithTheText`.
