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
- Unrestricted agent tools are off unless the user checks the option.
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
touching the opt-in; the opt-in and the applied setting are both off. This scenario is tested by
`AiProviderSettingsPageViewModel_UnrestrictedAgentTools_NotChosen_StaysOff`.

**AiProviderSettingsPageView_SelectProvider_EnablesMatchingEntry**: The view is shown with GitHub Copilot
selected, so only the path entry is enabled; Ollama is chosen in the combo box and only the endpoint entry is
enabled. This scenario is tested by `AiProviderSettingsPageView_SelectProvider_EnablesMatchingEntry`.

**AiProviderSettingsPageView_InvalidContextWindow_ShowsError**: The error line starts hidden; typing 100 shows
it with the bounds, and typing 16384 hides it and updates the view model. This scenario is tested by
`AiProviderSettingsPageView_InvalidContextWindow_ShowsError`.

**AiProviderSettingsPageView_UnrestrictedAgentToolsCheckBox_StartsOffAndTogglesSetting**: The check box starts
unchecked; checking it sets the view model's opt-in. This scenario is tested by
`AiProviderSettingsPageView_UnrestrictedAgentToolsCheckBox_StartsOffAndTogglesSetting`.

**AiProviderSettingsPageView_LoadedSettings_AreShownInControls**: A view model loaded with a model name and a
context window is shown; the text boxes display those values. This scenario is tested by
`AiProviderSettingsPageView_LoadedSettings_AreShownInControls`.

**ProviderCheck_CheckAsync_Implementation_ReturnsResultForTheSettingsGiven**: A provider check that is written against
`IProviderCheck`, and that names no type of the `Agent` subsystem, checks settings with a context window of 8192 and
settings with the default; the results are a not-usable result and a usable result that compare equal to the expected
values. This scenario is tested by `ProviderCheck_CheckAsync_Implementation_ReturnsResultForTheSettingsGiven`.
