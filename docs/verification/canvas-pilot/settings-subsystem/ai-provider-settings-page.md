### AiProviderSettingsPage

#### Verification Approach

`AiProviderSettingsPage` is verified at two levels. Unit tests in `AiProviderSettingsPageTests.cs` drive
`AiProviderSettingsPageViewModel` directly with real `AppSettings` records; the only collaborators are fakes of the two
interfaces that the Settings subsystem owns: a source of models (`IProviderModelSource`) whose answer, message, failure,
and delay (a gate that holds the answer back) a test sets and which records what it was asked, and a provider check. The
page is given no delay for typing, so a test waits for the list through `ModelListLoaded`. Headless UI tests in
`AiProviderSettingsPageViewTests.cs` (the `UiTests` project, using Avalonia's headless platform) show the real
`AiProviderSettingsPageView` bound to a view model and interact with its controls by automation identifier. Data-driven
tests cover the context window and endpoint boundaries.

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
  loaded and applied unchanged, as is the reasoning effort of a model that is not known; the Copilot path has a note
  about the installed executable, its minimum version, and shims (the version in the note is the minimum of the
  locator).
- With a source of models the page lists the models when it is loaded, led by the entry for the default model of the
  provider, and chooses the stored one; every model is listed with a note for what it lacks, a model that cannot be used
  is not selectable, and choosing it does not change the model name; the details of the chosen model name tool calling,
  pictures, reasoning levels, and the window; the warning names a model that cannot call tools and one that cannot see
  pictures, and a model that cannot be used with its reason, and is empty for a model that lacks nothing.
- The model name and the choice in the list stay in step: choosing an entry sets the name, typing a name chooses the
  entry (a name without a tag matches the latest tag), a name that is not listed is kept and applied with a note, and
  the default entry empties the name.
- The listing is asked again when the provider changes (the model and the reasoning effort start again), when the Ollama
  address is edited while Ollama is selected, and when the Copilot path is edited while GitHub Copilot is selected,
  never for an entry that is not valid or belongs to the other provider, and when Refresh models is chosen, which asks
  with the refresh flag; the answer of a listing that was overtaken, or that the closing of the page ended, is dropped;
  a source that throws gives the sentence "The models could not be listed: ..." and a page without a source offers no
  list and no Refresh models button.
- When the list cannot be had, the message is shown, the typed model name and the stored reasoning effort are kept and
  applied unchanged, and the reasoning level is disabled with the note that the levels are not known.
- The reasoning level is enabled only while the chosen model offers levels; its entries are the levels of the model with
  the provider's own ids (`none`, `low`, `xhigh`, `max` among them) after the entry that leaves the choice to the
  provider; the chosen id is applied; choosing a model without levels clears it, and a stored level that the known model
  does not offer is reset with a note; a change of the model or the level removes the answer of a connection check.
- The context window is labeled as an Ollama setting; while Ollama is selected a valid value below 16,384 shows a
  warning that recommends 32,768 and leaves the page valid, 16,384 and an invalid entry show none, and while GitHub
  Copilot is selected a note replaces the warning.
- Check connection gives the entries of the page, merged into the loaded settings, to the provider check, shows the
  answer (in red for a provider that cannot be used), is unavailable while the entries are invalid or a check runs,
  drops an answer that an edit or the closing of the dialog overtook, shows an exception as a sentence, and is not
  offered without a check.
- In the view, the Ollama endpoint entry is enabled only for Ollama and the Copilot path entry only for GitHub
  Copilot, and the error line is visible only while the entry is invalid. The model list is shown only while there are
  models, the typed name is always shown, the capabilities, the warning, and the message are shown only when they have
  text, the reasoning level is enabled by the model, and Refresh models asks again and shows the new list.
- The page is titled `AI Provider` with a non-empty sentence and an icon in the list of pages of the settings dialog.
- In the accessibility sweep of the windows of the application, in both theme variants, every entry of the page has a
  name, every button that shows no text has a name and a tooltip, and every enabled button can take the keyboard focus.

#### Test Scenarios

**AiProviderSettingsPageViewModel_Load_DefaultSettings_ShowsDefaults**: Default settings are loaded; GitHub
Copilot, an empty model name, 32768 tokens, the effort that leaves the choice to the provider (disabled), the default
endpoint, an empty path, and the opt-in off are
shown and the page is valid. This scenario is tested by
`AiProviderSettingsPageViewModel_Load_DefaultSettings_ShowsDefaults`.

**AiProviderSettingsPageViewModel_LoadApply_AllValues_RoundTrips**: Settings with a non-default value in every
AI provider field are loaded and applied to default settings; the applied AI provider section equals the
loaded one. This scenario is tested by `AiProviderSettingsPageViewModel_LoadApply_AllValues_RoundTrips`.

**AiProviderSettingsPageViewModel_Apply_EditedValues_AreAppliedTrimmed**: Ollama, a padded model name,
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

**AiProviderSettingsPageViewModel_UnrestrictedAgentTools_Stored_IsKeptUnchanged**: Settings with the flag on, a typed
model, and the reasoning effort `high` are loaded, another entry is edited, and the page is applied; both values are as
they were (no list is known, so the effort is kept). This scenario is tested by
`AiProviderSettingsPageViewModel_UnrestrictedAgentTools_Stored_IsKeptUnchanged`.

**AiProviderSettingsPageViewModel_Notes_CliPath_ExplainTheLimits**: The note of the Copilot path names `copilot.exe`,
1.0.113, and shims. This scenario is tested by `AiProviderSettingsPageViewModel_Notes_CliPath_ExplainTheLimits`.

**AiProviderSettingsPageViewModel_Load_WithModelSource_ListsTheModelsAndChoosesTheStoredOne**: A source with two models,
one of which is stored; the list is the default entry and the two models, the stored one is chosen, the details are
exactly "Tool calling: yes", "Pictures: yes", "Reasoning levels: low, high", and "Context window: up to 200,000 tokens",
the warning is empty, the message is empty, the page is not loading, and the source was asked once about GitHub Copilot
without the refresh flag. This scenario is tested by
`AiProviderSettingsPageViewModel_Load_WithModelSource_ListsTheModelsAndChoosesTheStoredOne`.

**AiProviderSettingsPageViewModel_ModelWithoutCapabilities_IsListedWithNoteAndWarning**: A source with a full model, one
that cannot see, one that cannot call tools, and one that cannot be used. The entries read "blind - no pictures",
"no-tools - no tool calling", and "Off Model (off) - unavailable"; the warnings name the pictures, the tools ("cannot
use this model"), and the reason of the model that cannot be used; the full model has none; the unavailable entry is
not selectable. This
scenario is tested by `AiProviderSettingsPageViewModel_ModelWithoutCapabilities_IsListedWithNoteAndWarning`.

**AiProviderSettingsPageViewModel_ModelChoiceAndTypedName_StayInStep**: Choosing an entry applies its id; typing
`gemma3` chooses `gemma3:latest`; typing `unlisted` chooses nothing, says that it is not in the list, and is applied as
typed; choosing the default entry empties the name. This scenario is tested by
`AiProviderSettingsPageViewModel_ModelChoiceAndTypedName_StayInStep`.

**AiProviderSettingsPageViewModel_ChoosingAnUnusableModel_IsNotTakenAsTheModelName**: Choosing the entry of a model that
cannot be used leaves the model name as it was. This scenario is tested by
`AiProviderSettingsPageViewModel_ChoosingAnUnusableModel_IsNotTakenAsTheModelName`.

**AiProviderSettingsPageViewModel_ReasoningEffort_IsEnabledByTheModelAndAppliedByItsId**: With no model the level is
disabled and says to choose a model; a model with `none`, `low`, `xhigh`, and `max` enables it with the entries of the
provider default and those four, choosing `xhigh` applies `xhigh`, and a model without levels disables it, clears the
effort, and says so. This scenario is tested by
`AiProviderSettingsPageViewModel_ReasoningEffort_IsEnabledByTheModelAndAppliedByItsId`.

**AiProviderSettingsPageViewModel_StoredReasoningEffort_IsKeptWhenOfferedAndResetWhenNot**: A stored `high` for a model
that offers `low` and `high` is chosen and enabled; a stored `xhigh` is reset to the provider default, with the note
that the model does not offer it. This scenario is tested by
`AiProviderSettingsPageViewModel_StoredReasoningEffort_IsKeptWhenOfferedAndResetWhenNot`.

**AiProviderSettingsPageViewModel_ListFails_KeepsTheTypedNameAndTheStoredEffort**: A source that answers no models and
the sentence for a server that does not answer; the list is empty, the sentence is shown, the typed name and the stored
`max` are applied unchanged, and the level is disabled with the note that the levels are not known. This scenario is
tested by `AiProviderSettingsPageViewModel_ListFails_KeepsTheTypedNameAndTheStoredEffort`.

**AiProviderSettingsPageViewModel_RefreshModels_AsksAgainWithRefreshAndShowsTheNewList**: A source that throws at first
gives "The models could not be listed: The runtime stopped."; after it recovers, Refresh models asks again with the
refresh flag and the list is shown. This scenario is tested by
`AiProviderSettingsPageViewModel_RefreshModels_AsksAgainWithRefreshAndShowsTheNewList`.

**AiProviderSettingsPageViewModel_StaleAnswer_IsDroppedWhenTheProviderChanged**: The answer for GitHub Copilot is held
back, Ollama is chosen, and then the held answer is let go; the page said that it was loading, and shows the models of
the provider that is selected now. This scenario is tested by
`AiProviderSettingsPageViewModel_StaleAnswer_IsDroppedWhenTheProviderChanged`.

**AiProviderSettingsPageViewModel_ProviderChange_ClearsTheModelAndAsksTheNewProvider**: A chosen model and level on
GitHub Copilot, then Ollama is chosen; the model and the level are empty and the source is asked about Ollama with the
endpoint of the page. This scenario is tested by
`AiProviderSettingsPageViewModel_ProviderChange_ClearsTheModelAndAsksTheNewProvider`.

**AiProviderSettingsPageViewModel_EditedAddressOrPath_ListsAgainOnlyForTheSelectedProvider**: Editing the Ollama address
while GitHub Copilot is selected asks nothing; editing the Copilot path asks once more with the path; an invalid path
asks nothing; with Ollama selected, editing the address asks with the new address. This scenario is tested by
`AiProviderSettingsPageViewModel_EditedAddressOrPath_ListsAgainOnlyForTheSelectedProvider`.

**AiProviderSettingsPageViewModel_InvalidEntriesDuringListing_ClearTheLoadingText**: The answer for a valid Copilot path
is held back and then the path becomes invalid; the loading state and the loading text are gone, the list stays, and
letting the held answer go changes nothing. A failure sentence that was shown before an invalid entry stays. This
scenario is tested by `AiProviderSettingsPageViewModel_InvalidEntriesDuringListing_ClearTheLoadingText`.

**AiProviderSettingsPageViewModel_NoSourceOrClosed_ShowsNoList**: A page without a source has no list, no Refresh models
command that can run, and no source; a page that is closed while its source holds the answer back shows no list and is
not loading, and the token of the question was cancelled. This scenario is tested by
`AiProviderSettingsPageViewModel_NoSourceOrClosed_ShowsNoList`.

**AiProviderSettingsPageViewModel_ChangedModelOrEffort_ForgetsTheConnectionCheck**: After a connection check shows its
answer, choosing another level removes the answer. This scenario is tested by
`AiProviderSettingsPageViewModel_ChangedModelOrEffort_ForgetsTheConnectionCheck`.

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

**AiProviderSettingsPageView_ReasoningEffort_IsDisabledWhileTheModelIsNotKnown**: A page without a source, loaded with a
typed model and the stored level `xhigh`: the combo box is disabled, shows `xhigh`, and the note says that the levels
are not known. This scenario is tested by
`AiProviderSettingsPageView_ReasoningEffort_IsDisabledWhileTheModelIsNotKnown`.

**AiProviderSettingsPageView_ModelList_ShowsCapabilitiesWarningsAndReasoningLevels**: A page over a source with a model
that does everything and a model that cannot see: the list is shown with three entries, one of them noting "no
pictures"; choosing the full model enables the levels, shows its capabilities, and shows no warning; choosing a level
stores it; choosing the other model shows the warning about pictures and disables the levels, which clears the stored
level. This scenario is tested by `AiProviderSettingsPageView_ModelList_ShowsCapabilitiesWarningsAndReasoningLevels`.

**AiProviderSettingsPageView_ModelListFails_KeepsTheTypedNameAndShowsTheMessage**: A page over a source that cannot be
asked shows no list, the sentence, and the typed name; after the source recovers and Refresh models is clicked, the list
is shown with the typed name chosen and the model that cannot be used disabled, and the source was asked with the
refresh flag. This scenario is tested by
`AiProviderSettingsPageView_ModelListFails_KeepsTheTypedNameAndShowsTheMessage`.

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

**SettingsDialogView_ChosenPage_ShowsHeadingIconsAndErrorBanner**: In the real settings dialog over this page and a
second page, the heading says `AI Provider` and the sentence under it is not empty, and the list shows an icon for the
page. This scenario is tested by `SettingsDialogView_ChosenPage_ShowsHeadingIconsAndErrorBanner`, which is described in
the verification of the `SettingsDialog` unit.

**Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize**: The settings dialog of the real application is shown in
both theme variants and each of its pages, this one among them, is chosen and every control of it is looked at; a text
box, drop-down, and list has a name, a button that shows no text has a name and a tooltip, and every enabled button can
take the keyboard focus. This scenario is tested by `Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize`.
