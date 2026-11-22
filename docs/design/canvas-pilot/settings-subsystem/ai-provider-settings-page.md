### AiProviderSettingsPage

![Settings Structure](SettingsSubsystemView.svg)

#### Purpose

`AiProviderSettingsPage` is the "AI Provider" page of the settings dialog. It lets the user choose and
validate the AI provider settings, and, when it is given an `IProviderCheck`, check the connection to the provider that
the entries describe before they are saved. The Agent subsystem reads the stored values through `ISettingsService`; the
page itself references no AI service, SDK, or client. The model name is free text for now; a later increment can
replace it with a list supplied by the provider.

Two settings are stored but not acted on, and the page says so. The reasoning level is shown disabled with the note
"Not available yet: the AI library does not expose it", and its value is loaded and applied unchanged. The unrestricted
agent tools flag is not offered at all, because the copilot has no such tools; its value is loaded and applied unchanged
too. The context window is shown as a setting of Ollama only.

#### Data Model

**Providers / SelectedProvider**: choices of `AiProviderKind` — "GitHub Copilot" and "Ollama".

**ModelName**: `string` — the model name; empty uses the provider's default.

**ContextWindowText**: `string` — the context window as typed. Valid when it parses as a whole number with
no sign or separators, in invariant culture, from 512 to 2,000,000. The label says "Ollama only"; while GitHub
Copilot is selected `ContextWindowNote` says that the window applies to Ollama only.

**ContextWindowWarning**: `string` — empty, or, while Ollama is selected and the context window is valid and below
`AiProviderSettings.RecommendedMinimumContextTokens` (16,384), a sentence that recommends 32,768. It is a warning, not
an error: `IsValid` does not change, and an invalid entry has the error and no warning.

**ReasoningLevels / SelectedReasoningLevel**: choices of `ReasoningLevel` — Auto, Low, Medium, High. The combo box is
disabled and `ReasoningLevelNoteText` is shown beside it; the selected value is kept.

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
runs, and then shows the sentence of the answer, in red when `IsUsable` is false (`IsCheckConnectionFailed`). An
exception of the check becomes the sentence "The connection could not be checked: ...". Editing the provider, model,
context window, endpoint, or path, or closing the dialog, removes the answer and drops the answer of a check that is
still running.

**View**: `AiProviderSettingsPageView` is an Avalonia user control with a combo box for the provider, text
boxes, a disabled reasoning level combo box, the Check connection button, notes, and error and warning lines that are
visible only when their message is not empty. Controls carry automation identifiers (`AiProviderComboBox`,
`AiModelNameTextBox`, `AiContextWindowTextBox`, `AiContextWindowWarning`, `AiContextWindowNote`,
`AiReasoningLevelComboBox`, `AiReasoningLevelNote`, `AiOllamaEndpointTextBox`, `AiCopilotCliPathTextBox`,
`AiCopilotCliPathNote`, `AiCheckConnectionButton`, `AiCheckConnectionResult`, and `...Error`).

#### Key Methods

**Load**: Copies `AppSettings.AiProvider` into the editable properties and remembers the settings; the context
window is formatted in invariant culture.

**Apply**: Returns the settings with the `AiProvider` section replaced by the page's entries and every other
section unchanged. Model name and path text are trimmed. The Ollama endpoint and Copilot path entries replace
the stored value only when they are themselves valid, so a half-typed entry for the provider that is not
selected never replaces a stored value. Throws `InvalidOperationException` when the page is not valid.

**CheckConnectionAsync**: Runs the connection check as described above.

**OnClosed**: Removes the answer and cancels the check that still runs.

#### Error Handling

Invalid entries are reported through the error properties and `IsValid`, never by exception, so the dialog can
disable OK and Apply. Only `Apply` on an invalid page throws, which the dialog prevents.

#### Dependencies

- **SettingsDialog** — hosts the page through `ISettingsPage` and `SettingsChoice`.
- **SettingsService** — owns the `AppSettings` and `AiProviderSettings` records and their bounds.
- **CommunityToolkit.Mvvm** and **Avalonia** — property change notification and the view.

#### Callers

- The application composes this page into the settings dialog and gives it the provider check (`AgentReadiness`).
