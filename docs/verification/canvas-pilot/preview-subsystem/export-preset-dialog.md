### ExportPresetDialog

#### Verification Approach

`ExportPresetDialog` is verified at two levels. Unit tests in `ExportPresetDialogTests.cs` drive
`ExportPresetDialogViewModel` and the `ExportPreset` value directly with plain presets, so no window is needed.
Headless UI tests in `ExportPresetDialogViewTests.cs` (the `UiTests` project, using Avalonia's headless platform) show
the real `ExportPresetDialogView` and interact with it through selection and mouse clicks, finding controls by
automation identifier. The headless tests assert what the user can see: the lines, the selected line, the result
sentence, the enabled state, and whether the window closed. The platform behavior of showing the dialog over the main
window belongs to the main window and is verified manually before a release.

#### Test Environment

Unit tests use the standard test environment. UI tests run under the Avalonia headless platform with the Skia renderer,
serialized with test parallelization disabled; no display or windowing system is required. No file, network, or external
service is used.

The look of the window (the pane surface, the footer bar, the primary Export and secondary Cancel buttons, the hint
style of the size lines) is part of the shared look of the dialogs and is checked by hand in both themes before a
release; no test asserts it.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- The dialog lists the choices in the order given with a name, a size line, and a group, and selects the first one, or
  the one with the identifier of the choice of the last time when it is still offered.
- The result sentence follows the selection and names the size, the proportions kept, and the background or
  transparency of each kind of choice.
- Export accepts the selected choice and closes the window; Cancel closes it without accepting; with nothing to choose
  Export cannot run.
- The settings of a preset for the renderer are its size, its background (none when it is transparent), and its
  resolution.

#### Test Scenarios

**ExportPresetDialogViewModel_New_Presets_ListsInOrderWithSizeLinesAndSelectsFirst**: A dialog over the picture as it
is shown and three sizes lists them in order with the size lines `As shown`, `1920 x 1080 px, on #FFFFFF`,
`800 px wide, on #102030, 150 dpi`, and `256 px high, transparent`, the groups `As shown`, `Application`, `Style kit`,
and `Style kit`, and selects the first. This scenario is tested by
`ExportPresetDialogViewModel_New_Presets_ListsInOrderWithSizeLinesAndSelectsFirst`.

**ExportPresetDialogViewModel_New_LastChoice_IsSelectedWhenStillOffered**: The choice of the last time is selected when
a preset with its identifier is still offered, and the first line is selected when it is not. This scenario is tested by
`ExportPresetDialogViewModel_New_LastChoice_IsSelectedWhenStillOffered`.

**ExportPresetDialogViewModel_ResultText_EachKindOfChoice_SaysWhatTheFileWillBe**: Selecting each line in turn gives the
sentence for the picture as it is shown, for a box with a background, for a width only, and for a height only with a
transparent background, and announces the change of the sentence and of the selected preset. This scenario is tested by
`ExportPresetDialogViewModel_ResultText_EachKindOfChoice_SaysWhatTheFileWillBe`.

**ExportPresetDialogViewModel_ExportAndCancel_AcceptOrNot**: Cancel asks the host to close without acceptance; Export
asks it to close with acceptance and keeps the selected preset; a dialog with nothing to choose cannot export and has
no result sentence. This scenario is tested by `ExportPresetDialogViewModel_ExportAndCancel_AcceptOrNot`.

**ExportPreset_ToRenderSettings_GivesTheSettingsOfThePreset**: An opaque preset gives its size, background, and
resolution; a transparent preset drops its background; the picture as it is shown gives settings that change nothing; a
preset with no size is described as the natural size. This scenario is tested by
`ExportPreset_ToRenderSettings_GivesTheSettingsOfThePreset`.

**ExportPresetDialogViewModel_InvalidArguments_Throw**: A missing list of presets and a missing preset are refused. This
scenario is tested by `ExportPresetDialogViewModel_InvalidArguments_Throw`.

**ExportPresetDialogView_Show_ListsChoicesAndSelectsFirst**: The shown window has the title `Export PNG`, three lines
with the first selected, the result sentence for the picture as it is shown, and both buttons enabled. This scenario is
tested by `ExportPresetDialogView_Show_ListsChoicesAndSelectsFirst`.

**ExportPresetDialogView_SelectSizeAndExport_ShowsResultAndAccepts**: Selecting the slide size changes the sentence to
name `1920 x 1080 px`, and clicking Export closes the window and accepts the slide size. This scenario is tested by
`ExportPresetDialogView_SelectSizeAndExport_ShowsResultAndAccepts`.

**ExportPresetDialogView_CancelButton_ClosesWindowWithoutAccepting**: Clicking Cancel closes the window without
accepting. This scenario is tested by `ExportPresetDialogView_CancelButton_ClosesWindowWithoutAccepting`.
