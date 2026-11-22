### AiProviderSettingsPage

#### Verification Approach

`AiProviderSettingsPage` is verified at two levels. Unit tests in `AiProviderSettingsPageTests.cs` drive
`AiProviderSettingsPageViewModel` directly with real `AppSettings` records; no mocking is required because the
page depends on nothing but the settings records. Headless UI tests in `AiProviderSettingsPageViewTests.cs`
(the `UiTests` project, using Avalonia's headless platform) show the real `AiProviderSettingsPageView` bound to
a view model and interact with its controls by automation identifier. Data-driven tests cover the context
window and endpoint boundaries.

#### Test Environment

Unit tests use the standard test environment. UI tests run under the Avalonia headless platform with the Skia
renderer, serialized with test parallelization disabled; no display or windowing system is required. No file,
network, or external services are required.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- The page shows the documented defaults, round-trips every choice, trims text entries, and leaves values it
  does not own unchanged.
- Context window entries below 512, above 2,000,000, or not whole numbers make the page invalid with a message
  that states the bounds; 512 and 2,000,000 are accepted; applying an invalid page throws.
- The Ollama endpoint must be an absolute `http` or `https` URL only while Ollama is selected, and an invalid
  entry never replaces the stored endpoint.
- The Copilot path is applied trimmed, can be cleared, and is rejected for impossible characters only while
  GitHub Copilot is selected.
- Unrestricted agent tools are not offered: the view has no check box and no text about them, and the stored value is
  loaded and applied unchanged, as is the reasoning level.
- The reasoning level combo box is disabled, shows the stored value, and has the note "Not available yet: the AI
  library does not expose it"; the Copilot path has a note about the installed executable, its minimum version, and
  shims (the version in the note is the minimum of the locator).
- The context window is labeled as an Ollama setting; while Ollama is selected a valid value below 16,384 shows a
  warning that recommends 32,768 and leaves the page valid, 16,384 and an invalid entry show none, and while GitHub
  Copilot is selected a note replaces the warning.
- Check connection gives the entries of the page, merged into the loaded settings, to the provider check, shows the
  answer (in red for a provider that cannot be used), is unavailable while the entries are invalid or a check runs,
  drops an answer that an edit or the closing of the dialog overtook, shows an exception as a sentence, and is not
  offered without a check.
- In the view, the Ollama endpoint entry is enabled only for Ollama and the Copilot path entry only for GitHub
  Copilot, and the error line is visible only while the entry is invalid.

#### Test Scenarios

**AiProviderSettingsPageViewModel_Load_DefaultSettings_ShowsDefaults**: Default settings are loaded; GitHub
Copilot, an empty model name, 32768 tokens, Auto, the default endpoint, an empty path, and the opt-in off are
shown and the page is valid. This scenario is tested by
`AiProviderSettingsPageViewModel_Load_DefaultSettings_ShowsDefaults`.

**AiProviderSettingsPageViewModel_LoadApply_AllValues_RoundTrips**: Settings with a non-default value in every
AI provider field are loaded and applied to default settings; the applied AI provider section equals the
loaded one. This scenario is tested by `AiProviderSettingsPageViewModel_LoadApply_AllValues_RoundTrips`.

**AiProviderSettingsPageViewModel_Apply_EditedValues_AreAppliedTrimmed**: Ollama, High, a padded model name,
context window, and endpoint are entered and the opt-in is checked; the applied values are trimmed and typed.
This scenario is tested by `AiProviderSettingsPageViewModel_Apply_EditedValues_AreAppliedTrimmed`.

**AiProviderSettingsPageViewModel_Apply_OtherSettings_AreUnchanged**: Settings with recent projects and speech
values are loaded, the model name is edited, and the page is applied; only the AI provider section changed.
This scenario is tested by `AiProviderSettingsPageViewModel_Apply_OtherSettings_AreUnchanged`.

**AiProviderSettingsPageViewModel_SelectedProvider_Switched_EnablesMatchingEntry**: The page starts with GitHub
Copilot selected, then Ollama is selected; the Ollama entry becomes the enabled one and the Copilot entry does
not. This scenario is tested by `AiProviderSettingsPageViewModel_SelectedProvider_Switched_EnablesMatchingEntry`.

**AiProviderSettingsPageViewModel_ContextWindowText_OutOfBounds_IsInvalid**: Each of 511, 2000001, abc, empty,
-5, 1.5, and 8,192 is entered; the page is invalid and the message names 512 and 2000000. This scenario is
tested by `AiProviderSettingsPageViewModel_ContextWindowText_OutOfBounds_IsInvalid`.

**AiProviderSettingsPageViewModel_ContextWindowText_AtBounds_IsValid**: 512 and 2000000 are entered; the page
is valid and applies the value. This scenario is tested by
`AiProviderSettingsPageViewModel_ContextWindowText_AtBounds_IsValid`.

**AiProviderSettingsPageViewModel_Apply_InvalidEntries_ThrowsInvalidOperationException**: An invalid context
window is entered and the page is applied; `InvalidOperationException` is thrown. This scenario is tested by
`AiProviderSettingsPageViewModel_Apply_InvalidEntries_ThrowsInvalidOperationException`.

**AiProviderSettingsPageViewModel_ContextWindowText_Edited_RaisesIsValidChanged**: An invalid context window is
typed; change notifications for the validity and the error message are raised. This scenario is tested by
`AiProviderSettingsPageViewModel_ContextWindowText_Edited_RaisesIsValidChanged`.

**AiProviderSettingsPageViewModel_OllamaEndpoint_InvalidWhenOllamaSelected_IsInvalid**: With Ollama selected,
each of an empty text, `localhost:11434`, `ftp://server`, and `not a url` is entered; the page is invalid with
an endpoint message. This scenario is tested by
`AiProviderSettingsPageViewModel_OllamaEndpoint_InvalidWhenOllamaSelected_IsInvalid`.

**AiProviderSettingsPageViewModel_OllamaEndpoint_InvalidWhenCopilotSelected_IsIgnoredAndStoredValueKept**: With
GitHub Copilot selected, a junk endpoint is typed; the page is valid and applying keeps the stored endpoint.
This scenario is tested by
`AiProviderSettingsPageViewModel_OllamaEndpoint_InvalidWhenCopilotSelected_IsIgnoredAndStoredValueKept`.

**AiProviderSettingsPageViewModel_CopilotCliPath_EditedAndCleared_IsApplied**: A stored path is replaced by a
padded path, which is applied trimmed, and then cleared, which applies an empty path. This scenario is tested by
`AiProviderSettingsPageViewModel_CopilotCliPath_EditedAndCleared_IsApplied`.

**AiProviderSettingsPageViewModel_CopilotCliPath_InvalidCharacters_IsErrorOnlyForCopilot**: A path with a null
character is typed; the page is invalid while GitHub Copilot is selected and valid, without a message, once
Ollama is selected. This scenario is tested by
`AiProviderSettingsPageViewModel_CopilotCliPath_InvalidCharacters_IsErrorOnlyForCopilot`.

**AiProviderSettingsPageViewModel_UnrestrictedAgentTools_NotChosen_StaysOff**: Other entries are edited without
touching the stored flag; the applied setting is off. This scenario is tested by
`AiProviderSettingsPageViewModel_UnrestrictedAgentTools_NotChosen_StaysOff`.

**AiProviderSettingsPageViewModel_UnrestrictedAgentTools_Stored_IsKeptUnchanged**: Settings with the flag on and
the reasoning level High are loaded, another entry is edited, and the page is applied; both values are as they were.
This scenario is tested by `AiProviderSettingsPageViewModel_UnrestrictedAgentTools_Stored_IsKeptUnchanged`.

**AiProviderSettingsPageViewModel_Notes_ReasoningAndCliPath_ExplainTheLimits**: The reasoning note is the agreed
sentence, and the note of the Copilot path names `copilot.exe`, 1.0.113, and shims. This scenario is tested by
`AiProviderSettingsPageViewModel_Notes_ReasoningAndCliPath_ExplainTheLimits`.

**AiProviderSettingsPageViewModel_CopilotCliPathNote_MinimumVersion_IsTheLocatorsMinimum**: The note of the Copilot
path contains the minimum version that `CopilotCliLocator` checks. This scenario is tested by
`AiProviderSettingsPageViewModel_CopilotCliPathNote_MinimumVersion_IsTheLocatorsMinimum`.

**AiProviderSettingsPageViewModel_ContextWindowWarning_BelowRecommendedMinimum_WarnsWithoutInvalidating**: With Ollama
selected, 16383 gives the warning with 16,384 and 32,768 and the page stays valid, 16384 and 32768 give none, and the
change is announced. This scenario is tested by
`AiProviderSettingsPageViewModel_ContextWindowWarning_BelowRecommendedMinimum_WarnsWithoutInvalidating`.

**AiProviderSettingsPageViewModel_ContextWindowWarning_InvalidOrCopilot_GivesNoWarning**: An invalid window has the
error and no warning; a small valid window with GitHub Copilot selected has no warning and the Ollama-only note, and
the page with Ollama selected has no note. This scenario is tested by
`AiProviderSettingsPageViewModel_ContextWindowWarning_InvalidOrCopilot_GivesNoWarning`.

**AiProviderSettingsPageViewModel_CheckConnection_NoProviderCheck_IsNotOffered**: A page without a check has no
connection check and its command cannot run. This scenario is tested by
`AiProviderSettingsPageViewModel_CheckConnection_NoProviderCheck_IsNotOffered`.

**AiProviderSettingsPageViewModel_CheckConnection_ValidEntries_ChecksTheEntriesAndShowsTheAnswer**: A check that
records its input is given a page with Ollama, a padded model name, a window of 8192, and a stored flag; it receives
the trimmed edited entries and the stored flag, its answer is shown as a success, and editing an entry removes the
answer. This scenario is tested by
`AiProviderSettingsPageViewModel_CheckConnection_ValidEntries_ChecksTheEntriesAndShowsTheAnswer`.

**AiProviderSettingsPageViewModel_CheckConnection_FailureAndInvalidEntries_ShowFailureAndBlockTheButton**: A failing
answer is shown as a failure; an invalid entry makes the command unavailable; while a held check runs the text is
"Checking the connection..." and the command is unavailable. This scenario is tested by
`AiProviderSettingsPageViewModel_CheckConnection_FailureAndInvalidEntries_ShowFailureAndBlockTheButton`.

**AiProviderSettingsPageViewModel_CheckConnection_ThrowsOrIsOvertaken_ShowsSentenceOrDropsAnswer**: A check that
throws gives the sentence with the message; a held check whose page is closed before it answers is cancelled and its
late answer is not shown. This scenario is tested by
`AiProviderSettingsPageViewModel_CheckConnection_ThrowsOrIsOvertaken_ShowsSentenceOrDropsAnswer`.

**AiProviderSettingsPageView_SelectProvider_EnablesMatchingEntry**: The view is shown with GitHub Copilot
selected, so only the path entry is enabled; Ollama is chosen in the combo box and only the endpoint entry is
enabled. This scenario is tested by `AiProviderSettingsPageView_SelectProvider_EnablesMatchingEntry`.

**AiProviderSettingsPageView_InvalidContextWindow_ShowsError**: The error line starts hidden; typing 100 shows
it with the bounds, and typing 16384 hides it and updates the view model. This scenario is tested by
`AiProviderSettingsPageView_InvalidContextWindow_ShowsError`.

**AiProviderSettingsPageView_UnrestrictedAgentTools_IsNotOffered**: A page loaded with the flag on is shown; it has no
check box and no text with "unrestricted", and applying still gives the flag on. This scenario is tested by
`AiProviderSettingsPageView_UnrestrictedAgentTools_IsNotOffered`.

**AiProviderSettingsPageView_ReasoningLevel_IsDisabledWithNote**: The combo box is disabled, shows the stored level, and
the note is the agreed sentence. This scenario is tested by
`AiProviderSettingsPageView_ReasoningLevel_IsDisabledWithNote`.

**AiProviderSettingsPageView_ContextWindow_IsOllamaOnlyAndWarnsBelowTheRecommendedMinimum**: The label says "Ollama
only"; with GitHub Copilot selected the note is visible; choosing Ollama hides it, 16383 shows the warning, and 16384
hides it. This scenario is tested by
`AiProviderSettingsPageView_ContextWindow_IsOllamaOnlyAndWarnsBelowTheRecommendedMinimum`.

**AiProviderSettingsPageView_CopilotCliPath_ShowsNoteOnInstalledExecutable**: The note under the path names
`copilot.exe`, 1.0.113, and shims. This scenario is tested by
`AiProviderSettingsPageView_CopilotCliPath_ShowsNoteOnInstalledExecutable`.

**AiProviderSettingsPageView_CheckConnection_ShowsTheAnswerOfTheCheck**: The button is clicked with a check that
fails, then with one that succeeds; the answer is shown, in red the first time, and the second time not; a page
without a check shows no button. This scenario is tested by
`AiProviderSettingsPageView_CheckConnection_ShowsTheAnswerOfTheCheck`.

**AiProviderSettingsPageView_LoadedSettings_AreShownInControls**: A view model loaded with a model name and a
context window is shown; the text boxes display those values. This scenario is tested by
`AiProviderSettingsPageView_LoadedSettings_AreShownInControls`.

**ProviderCheck_CheckAsync_Implementation_ReturnsResultForTheSettingsGiven**: A provider check that is written against
`IProviderCheck`, and that names no type of the `Agent` subsystem, checks settings with a context window of 8192 and
settings with the default; the results are a not-usable result and a usable result that compare equal to the expected
values. This scenario is tested by `ProviderCheck_CheckAsync_Implementation_ReturnsResultForTheSettingsGiven`.
