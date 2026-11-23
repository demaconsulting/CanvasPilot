### AiProviderSettingsPage

![Settings Structure](SettingsSubsystemView.svg)

#### Purpose

`AiProviderSettingsPage` is the "AI Provider" page of the settings dialog. It lets the user choose and validate the AI
provider settings, lists the models of the selected provider when it is given an `IProviderModelSource`, and, when it is
given an `IProviderCheck`, checks the connection to the provider that the entries describe before they are saved. The
Agent subsystem reads the stored values through `ISettingsService`; the page itself references no AI service, SDK, or
client. The page asks what each model can do and what it lacks, so that the user can choose well, but it decides nothing
that the provider decides: the model and the reasoning level are validated by the provider's catalog when a conversation
starts (the factory of agents and the readiness check of the `Agent` subsystem).

The unrestricted agent tools flag is not offered at all, because the copilot has no such tools; its value is loaded and
applied unchanged. The context window is shown as a setting of Ollama only.

#### Data Model

**Providers / SelectedProvider**: choices of `AiProviderKind` — "GitHub Copilot" and "Ollama". A user's choice of
another provider clears the model name and the reasoning level, because a model id of one provider means nothing to the
other, and asks for the models of the new provider.

**ModelName**: `string` — the model id as typed or chosen; empty uses the provider's default model. The text box is
always there, so a model that is not listed, or any model while no list exists, can be typed.

**Models / SelectedModel / HasModelList** (`ModelChoice` in `ModelChoice.cs`): the models that the provider listed, led
by an entry for "Provider default model" (empty id). A `ModelChoice` has an id, the text of the list entry, the
`ProviderModelInfo`, and `IsSelectable` (false for a model that the provider says cannot be used, which the view shows
disabled). The entry text is the name with a short note of what the model lacks ("no tool calling", "no pictures",
"unavailable"). `SelectedModel` and `ModelName` stay in step: choosing an entry sets the name (never for a disabled
entry, and never for the `null` that a combo box reports when its list is replaced); typing a name chooses the entry of
the same id, a name without a tag matching the `:latest` entry, and none when the name is not listed.

**ModelDetails / ModelWarning**: `string` — the capabilities of the chosen model, one fact per line (tool calling,
pictures, reasoning levels, context window), and what it lacks, one sentence per line: a model that cannot be used with
the reason, a model that cannot call tools ("CanvasPilot needs tool calling to draw; it cannot use this model"), a model
that does not accept pictures ("it cannot see your marks or its own drawings"), and the notes that the provider adds. A
model name that is not listed gets a note in `ModelDetails` that it is tried as typed. All models are listed; the page
warns and never blocks.

**IsLoadingModels / ModelListMessage / HasModelSource / RefreshModelsCommand / ModelListLoaded**: the page takes the
source as an optional constructor argument (with the delay to wait after typing). The models are listed when the page is
loaded, when the user chooses another provider, when the Ollama address is edited while Ollama is selected, when the
Copilot path is edited while GitHub Copilot is selected (these two after a pause of 600 ms, and not at all while an
entry is invalid), and when the user chooses Refresh models (which asks the provider again and does not use what it said
a short while ago). Each listing ends the one that runs; the answer of a listing that the entries overtook, or that the
closing of the page ended, is dropped. An entry that is not valid ends the listing that runs and clears its loading
text; the list that is shown and any other message stay. The page lists with the entries as typed, not as saved, so a
different Copilot path makes the shared starter stop the runtime that runs and start another (see the known limit in the
design of `CopilotRuntimeStarter`). `ModelListMessage` says that the models are loading, or why there is no list (the
sentence of the source) and is empty otherwise. `ModelListLoaded` is the task of the listing that was started last,
which lets a caller or a test wait for the list. A source that throws becomes the sentence "The models could not be
listed: ...".

**ReasoningEfforts / SelectedReasoningEffort / IsReasoningEffortEnabled / ReasoningNote**: the entries are "Provider
default" (empty id) and the efforts that the chosen model offers, with the provider's own ids (none, low, medium, high,
xhigh, max, and others). The choice is enabled only while the chosen model offers efforts. The id of the choice is what
is stored. When the chosen model becomes known and does not offer the stored effort, the effort is reset with the note
"The model does not offer the reasoning level ...; the provider chooses instead"; a listed model that offers none clears
it ("This model has no reasoning levels to choose"); an empty model name clears it ("Choose a model to set its reasoning
level"). While nothing is known about the model (no list, or a name that is not listed) the stored effort is kept,
shown, and disabled, with the note that the levels of the model are not known, so that a server that is down does not
erase a setting.

**ContextWindowText**: `string` — the context window as typed. Valid when it parses as a whole number with
no sign or separators, in invariant culture, from 512 to 2,000,000. The label says "Ollama only"; while GitHub
Copilot is selected `ContextWindowNote` says that the window applies to Ollama only.

**ContextWindowWarning**: `string` — empty, or, while Ollama is selected and the context window is valid and below
`AiProviderSettings.RecommendedMinimumContextTokens` (16,384), a sentence that recommends 32,768. It is a warning, not
an error: `IsValid` does not change, and an invalid entry has the error and no warning.

**OllamaEndpoint**: `string` — the endpoint as typed. Valid when it is an absolute `http` or `https` URL with
a host. Enabled, and validated, only while Ollama is selected (`IsOllamaSelected`).

**CopilotCliPath**: `string` — the path of the GitHub Copilot command-line tool; empty means automatic discovery.
Enabled, and validated, only while GitHub Copilot is selected (`IsCopilotSelected`); the only check is that the text has
no characters that are invalid in a file path. `CopilotCliPathNoteText` is shown under it: the installed `copilot.exe`
of version 1.0.113 or later is used, a path set here wins over the search, and launcher shims are ignored (the rule is
that of `CopilotCliLocator` of the `Agent` subsystem).

**UnrestrictedAgentTools**: `bool` — the stored value, `false` by default. It has no control on the page and is applied
unchanged.

**ContextWindowError / OllamaEndpointError / CopilotCliPathError**: `string` — empty when valid, otherwise the
message shown under the entry. The context window message states the bounds.

**IsValid**: `bool` — true when all three errors are empty.

**IProviderModelSource / ProviderModelList / ProviderModelInfo / ProviderEffortChoice** (supporting types, in
`IProviderModelSource.cs`): the seam through which the page reaches the models of the provider without the `Settings`
subsystem depending on the subsystem that implements it (the `ProviderModelCatalog` unit of the `Agent` subsystem does,
and the composition root passes it in), and without naming a type of the AI library.
`ListModelsAsync(AiProviderSettings, refresh, CancellationToken)` takes provider settings that may not have been saved
yet and returns a `ProviderModelList`: the models in the order to show them and a message that is empty when the list is
complete. `ProviderModelInfo` is plain data: the id, the name, whether the model can call tools, accepts pictures, and
reasons, its largest context window, the efforts that it offers, the default effort, whether it can be used and why not,
and the notes of the provider. A provider that cannot be asked is a list with no models and a sentence, not an
exception.

**IProviderCheck / ProviderCheckResult** (supporting types, in `IProviderCheck.cs`): the seam through which the
connection check reaches the provider without the `Settings` subsystem depending on the subsystem that implements it
(the `AgentReadiness` unit of the `Agent` subsystem does, and the composition root passes it in).
`IProviderCheck.CheckAsync(AiProviderSettings, CancellationToken)` takes provider settings that may not have been saved
yet and returns a `ProviderCheckResult(IsUsable, Message)`: whether the provider can be used and one sentence for the
user. A provider that cannot be used is a result, not an exception.

**CheckConnectionCommand / CheckConnectionResult / IsCheckConnectionOk / IsCheckingConnection**: the page takes the
check as an optional constructor argument; without it `HasConnectionCheck` is false and the view shows no button. The
command is available while the entries are valid and no check runs. It applies the entries to the settings that were
loaded last (nothing is stored), gives the provider settings to the check, shows "Checking the connection..." while it
runs, and then shows the sentence of the answer, in the danger color when `IsUsable` is false
(`IsCheckConnectionFailed`). An exception of the check becomes the sentence "The connection could not be checked: ...".
Editing the provider, model,
reasoning level, context window, endpoint, or path, or closing the dialog, removes the answer and drops the answer of a
check that is still running.

**Page heading**: `Title` is `AI Provider`, `Description` is the sentence `Choose which AI provider and model the
copilot uses, and check that it can be reached.`, and `IconKey` is `CpIconSparkle`; the settings dialog shows them in
its list of pages and above the page.

**View**: `AiProviderSettingsPageView` is an Avalonia user control in a scroll viewer with up to three raised cards,
each with a section title and, above each entry, a label in the label look. The card `Provider and model` has a combo
box for the provider, the combo box of the models (visible only while there is a list, with disabled entries for models
that cannot be used), the text box for the model name, the Refresh models button with the sentence about the list, the
details and the warning of the chosen model, and a combo box of the reasoning levels (enabled by the model) with its
note. The card `Connection` has the text boxes for the context window, the Ollama endpoint, and the path of the Copilot
tool, with their notes. The card `Check` is shown only when there is a check; it has the Check connection button and the
answer. The answer is the sentence of the provider check, which for GitHub Copilot that can be used names the account,
the host, and the kind of sign-in (for example "GitHub Copilot CLI {version} is ready. Signed in as octocat on
github.com (Copilot CLI sign-in)."), and for Copilot that is not signed in is the sentence that says to run `copilot`
and type `/login`, with what the library found. Notes and sentences use the hint look, warnings the caution look, and
error lines the error look and are visible only when their message is not empty; the answer of the check is in the
danger color when the provider cannot be used. No color is written in the view: all come from the shared styles of the
controls and the design tokens. Every entry has an automation name (`Provider`, `Model`, `Model name`,
`Reasoning level`, `Context window in tokens`, `Ollama endpoint`, and `GitHub Copilot command-line tool path`). Controls
carry automation identifiers (`AiProviderComboBox`, `AiModelComboBox`, `AiModelNameTextBox`, `AiRefreshModelsButton`,
`AiModelListMessage`, `AiModelDetails`, `AiModelWarning`, `AiContextWindowTextBox`, `AiContextWindowWarning`,
`AiContextWindowNote`, `AiReasoningEffortComboBox`, `AiReasoningEffortNote`, `AiOllamaEndpointTextBox`,
`AiCopilotCliPathTextBox`, `AiCopilotCliPathNote`, `AiCheckConnectionButton`, `AiCheckConnectionResult`, and
`...Error`); none of them changed in the redesign.

#### Key Methods

**Load**: Copies `AppSettings.AiProvider` into the editable properties and remembers the settings; the context
window is formatted in invariant culture. The changes of the entries are not taken for edits while this runs, and the
list of models is asked for once, at the end.

**Apply**: Returns the settings with the `AiProvider` section replaced by the page's entries and every other section
unchanged. Model name and path text are trimmed, and the reasoning effort is the id that was chosen. The Ollama endpoint
and Copilot path entries replace the stored value only when they are themselves valid, so a half-typed entry for the
provider that is not selected never replaces a stored value. Throws `InvalidOperationException` when the page is not
valid.

**StartModelLoad / RefreshModelsAsync**: Starts the listing of the models as described above and ends the one that runs.

**CheckConnectionAsync**: Runs the connection check as described above.

**OnClosed**: Removes the answer, cancels the check and the listing that still run.

#### Error Handling

Invalid entries are reported through the error properties and `IsValid`, never by exception, so the dialog can disable
OK and Apply. Only `Apply` on an invalid page throws, which the dialog prevents. A source of models that fails is shown
as a sentence and leaves the typed name and the stored values as they are.

#### Dependencies

- **SettingsDialog** — hosts the page through `ISettingsPage` and `SettingsChoice`.
- **SettingsService** — owns the `AppSettings` and `AiProviderSettings` records and their bounds.
- **CommunityToolkit.Mvvm** and **Avalonia** — property change notification and the view.

#### Callers

- The application composes this page into the settings dialog and gives it the provider check (`AgentReadiness`) and the
  model source (`ProviderModelCatalog`).
