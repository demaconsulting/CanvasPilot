### AiProviderSettingsPage

![Settings Structure](SettingsSubsystemView.svg)

#### Purpose

`AiProviderSettingsPage` is the "AI Provider" page of the settings dialog. It lets the user choose and
validate the AI provider settings. The values are persisted only: no AI service, SDK, or client is referenced
or called, and a later AI agent reads the stored values through `ISettingsService`. The model name is free
text for now; a later increment can replace it with a list supplied by the provider.

#### Data Model

**Providers / SelectedProvider**: choices of `AiProviderKind` — "GitHub Copilot" and "Ollama".

**ModelName**: `string` — the model name; empty uses the provider's default.

**ContextWindowText**: `string` — the context window as typed. Valid when it parses as a whole number with
no sign or separators, in invariant culture, from 512 to 2,000,000.

**ReasoningLevels / SelectedReasoningLevel**: choices of `ReasoningLevel` — Auto, Low, Medium, High.

**OllamaEndpoint**: `string` — the endpoint as typed. Valid when it is an absolute `http` or `https` URL with
a host. Enabled, and validated, only while Ollama is selected (`IsOllamaSelected`).

**CopilotCliPath**: `string` — the path of the GitHub Copilot command-line tool; empty means automatic
discovery. Enabled, and validated, only while GitHub Copilot is selected (`IsCopilotSelected`); the only
check is that the text has no characters that are invalid in a file path.

**UnrestrictedAgentTools**: `bool` — the opt-in; `false` by default, with a warning text beside the check box.

**ContextWindowError / OllamaEndpointError / CopilotCliPathError**: `string` — empty when valid, otherwise the
message shown under the entry. The context window message states the bounds.

**IsValid**: `bool` — true when all three errors are empty.

**IProviderCheck / ProviderCheckResult** (supporting types, in `IProviderCheck.cs`): the seam through which a connection
check can be added to the page without the `Settings` subsystem depending on the subsystem that implements it.
`IProviderCheck.CheckAsync(AiProviderSettings, CancellationToken)` takes provider settings that may not have been
saved yet and returns a `ProviderCheckResult(IsUsable, Message)`: whether the provider can be used and one sentence for
the user. A provider that cannot be used is a result, not an exception. The page does not call the interface yet.

**View**: `AiProviderSettingsPageView` is an Avalonia user control with a combo box for the provider, text
boxes, a reasoning level combo box, a check box, and error lines that are visible only when their message is
not empty. Controls carry automation identifiers (`AiProviderComboBox`, `AiModelNameTextBox`,
`AiContextWindowTextBox`, `AiReasoningLevelComboBox`, `AiOllamaEndpointTextBox`, `AiCopilotCliPathTextBox`,
`AiUnrestrictedAgentToolsCheckBox`, and `...Error`).

#### Key Methods

**Load**: Copies `AppSettings.AiProvider` into the editable properties; the context window is formatted in
invariant culture.

**Apply**: Returns the settings with the `AiProvider` section replaced by the page's entries and every other
section unchanged. Model name and path text are trimmed. The Ollama endpoint and Copilot path entries replace
the stored value only when they are themselves valid, so a half-typed entry for the provider that is not
selected never replaces a stored value. Throws `InvalidOperationException` when the page is not valid.

**OnClosed**: Does nothing; the page starts no background work.

#### Error Handling

Invalid entries are reported through the error properties and `IsValid`, never by exception, so the dialog can
disable OK and Apply. Only `Apply` on an invalid page throws, which the dialog prevents.

#### Dependencies

- **SettingsDialog** — hosts the page through `ISettingsPage` and `SettingsChoice`.
- **SettingsService** — owns the `AppSettings` and `AiProviderSettings` records and their bounds.
- **CommunityToolkit.Mvvm** and **Avalonia** — property change notification and the view.

#### Callers

- The application composes this page into the settings dialog.
