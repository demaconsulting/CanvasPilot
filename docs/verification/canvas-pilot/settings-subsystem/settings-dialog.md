### SettingsDialog

#### Verification Approach

`SettingsDialog` is verified at two levels. Unit tests in `SettingsDialogTests.cs` drive
`SettingsDialogViewModel` directly, using a hand-written `FakeSettingsPage` that records how the dialog drives
it and lets a test change its validity, a real `SettingsService` on a temporary folder, and an NSubstitute
`ISettingsService` where a test needs to observe that nothing was saved or to force a save failure. Headless
UI tests in `SettingsDialogViewTests.cs` (the `UiTests` project, using Avalonia's headless platform) show the
real `SettingsDialogView` over the real `AiProviderSettingsPageViewModel` and a minimal second page, and
interact with it through mouse clicks and list selection by automation identifier. The headless tests assert
the observable result, such as the saved file, window visibility, and enabled state, rather than internals.

#### Test Environment

Unit tests use the standard test environment. UI tests run under the Avalonia headless platform with the Skia
renderer, serialized with test parallelization disabled; no display or windowing system is required. Tests use
temporary local folders and real file I/O; no network or external services are required.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- Every page is loaded from the settings in effect and the first page is selected; the window lists every page
  and shows only the selected page's content.
- OK saves the merged entries and closes the dialog, Apply saves and keeps it open, and Cancel closes it
  without saving.
- OK and Apply are disabled while any page is invalid and are enabled again when every page is valid.
- A failed save shows a message and keeps the dialog open.
- Each page is told that the dialog closed exactly once, however the dialog is closed.

#### Test Scenarios

**SettingsDialogViewModel_New_Pages_LoadsEachPageAndSelectsFirst**: A dialog is created over two pages and
stored settings; both pages receive the settings in effect and the first page is selected. This scenario is
tested by `SettingsDialogViewModel_New_Pages_LoadsEachPageAndSelectsFirst`.

**SettingsDialogViewModel_New_NoPages_HasNoSelectionAndIsValid**: A dialog is created without pages; nothing
is selected and the dialog is valid. This scenario is tested by
`SettingsDialogViewModel_New_NoPages_HasNoSelectionAndIsValid`.

**SettingsDialogViewModel_Ok_ValidPages_SavesMergedSettingsAndRequestsClose**: Two pages each contribute a
value to settings that already hold another value; OK saves all three values in page order and requests a
close with acceptance. This scenario is tested by
`SettingsDialogViewModel_Ok_ValidPages_SavesMergedSettingsAndRequestsClose`.

**SettingsDialogViewModel_Apply_ValidPages_SavesAndKeepsDialogOpen**: A page contributes a value; Apply saves
it, no close is requested, and the page is not told the dialog closed. This scenario is tested by
`SettingsDialogViewModel_Apply_ValidPages_SavesAndKeepsDialogOpen`.

**SettingsDialogViewModel_Cancel_PendingEdits_DoesNotSaveAndRequestsClose**: A page holds a pending value;
Cancel saves nothing and requests a close without acceptance. This scenario is tested by
`SettingsDialogViewModel_Cancel_PendingEdits_DoesNotSaveAndRequestsClose`.

**SettingsDialogViewModel_Commands_InvalidPage_AreDisabledAndDoNotSave**: A page is invalid; OK and Apply are
disabled and nothing is saved when they are run, while Cancel stays enabled. This scenario is tested by
`SettingsDialogViewModel_Commands_InvalidPage_AreDisabledAndDoNotSave`.

**SettingsDialogViewModel_PageBecomesValid_EnablesCommands**: An invalid page becomes valid; the dialog reports
all pages valid, OK and Apply are enabled, and a command state change is signaled. This scenario is tested by
`SettingsDialogViewModel_PageBecomesValid_EnablesCommands`.

**SettingsDialogViewModel_Ok_SaveFails_ShowsErrorAndStaysOpen**: The service throws `IOException` on save; OK
sets an error message that contains the failure text, requests no close, and does not tell the page the dialog
closed. This scenario is tested by `SettingsDialogViewModel_Ok_SaveFails_ShowsErrorAndStaysOpen`.

**SettingsDialogViewModel_Cancel_NotifiesPagesOnce**: Cancel is chosen and the window then reports that it
closed; each page was told exactly once. This scenario is tested by
`SettingsDialogViewModel_Cancel_NotifiesPagesOnce`.

**SettingsDialogViewModel_NotifyClosed_CalledTwice_NotifiesPagesOnce**: The close notification is given twice
without a command; the page was told once. This scenario is tested by
`SettingsDialogViewModel_NotifyClosed_CalledTwice_NotifiesPagesOnce`.

**SettingsDialogView_Show_ListsPagesAndShowsFirstPageContent**: The window is shown over the AI provider page
and a second page; the page list has two entries, the window title is "Settings", and the AI provider controls
are present but the second page's content is not. This scenario is tested by
`SettingsDialogView_Show_ListsPagesAndShowsFirstPageContent`.

**SettingsDialogView_SelectOtherPage_ShowsItsContent**: The second page is selected in the list; its content
appears and the AI provider controls are gone. This scenario is tested by
`SettingsDialogView_SelectOtherPage_ShowsItsContent`.

**SettingsDialogView_OkButton_SavesAndClosesWindow**: A model name is typed and the OK button is clicked; the
window closes and a fresh service loads the name. This scenario is tested by
`SettingsDialogView_OkButton_SavesAndClosesWindow`.

**SettingsDialogView_ApplyButton_SavesAndKeepsWindowOpen**: A model name is typed and the Apply button is
clicked; the name is stored and the window is still open. This scenario is tested by
`SettingsDialogView_ApplyButton_SavesAndKeepsWindowOpen`.

**SettingsDialogView_CancelButton_ClosesWithoutSaving**: A model name is typed and the Cancel button is
clicked; the window closes and no settings file exists. This scenario is tested by
`SettingsDialogView_CancelButton_ClosesWithoutSaving`.

**SettingsDialogView_InvalidPage_DisablesOkAndApply**: An invalid context window is typed; the OK and Apply
buttons are disabled and Cancel is enabled. This scenario is tested by
`SettingsDialogView_InvalidPage_DisablesOkAndApply`.

**SettingsDialogView_CloseWindow_NotifiesPages**: The window is closed directly; the second page was told once.
This scenario is tested by `SettingsDialogView_CloseWindow_NotifiesPages`.
