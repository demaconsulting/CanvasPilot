### ProviderPill

![AppShell Structure](AppShellSubsystemView.svg)

#### Purpose

`ProviderPill` is the pill in the title bar of the main window that says which AI provider and model the settings choose
and whether the provider can be used. It tells the state with words as well as with a colored dot, from the same check
that the readiness banner of the chat panel shows, and using it opens the settings. It belongs to the AppShell subsystem
and not to the chat panel so that the chat panel does not depend on the settings window.

#### Data Model

**ProviderPillViewModel** (`AppShellSubsystem/ProviderPillViewModel.cs`): the view model. The composition root creates
it after the chat panel with `new ProviderPillViewModel(chat, settings, shell.OpenSettingsCommand)` and gives it to the
shell (`MainWindowShell.ProviderPill`, with `HasProviderPill`); the shell disposes it. It holds the chat panel, the
settings service, the command, and the synchronization context of the thread that created it, and no state of its own.

- *State*: `ProviderPillState` is `Checking`, `Ready`, `NeedsAttention`, or `Error`. It is worked out when asked from
  the chat panel: a chat panel without a copilot (`HasCopilot` false) gives `Error`; a check that is running or has not
  answered (`IsCheckingReadiness` or no `Readiness`) gives `Checking`; otherwise the status of the readiness report
  gives `Ready` for `Ready`, `Error` for `CopilotRuntimeUnavailable`, and `NeedsAttention` for every other status. The
  flags `IsChecking`, `IsReady`, `NeedsAttention`, and `IsError` are what the view binds to.
- *Provider and model*: `ProviderName` is `Ollama` or `Copilot` by the provider that the settings choose. `ModelName` is
  the model of the settings, or `default model` when it is empty.
- *Words*: `DetailText` is the model while the state is `Ready`, `checking` while it is `Checking`, and otherwise the
  few words for the status of the report, the table below. `SeparatedDetailText` is a space, a middle dot, a space, and
  the detail; it is what the view shows after the provider name.
- *Sentences*: `AccessibleName` is `AI provider: {ProviderName}, {DetailText}. Opens the settings.` and is the
  automation name of the button. `ToolTipText` is the sentence of the check (`Readiness.Message`, the same one that the
  banner of the chat panel shows; for GitHub Copilot that can be used it ends with the sentence that names the account,
  the host, and the kind of sign-in, such as "Signed in as mnixon on github.com (Copilot CLI sign-in).", and never a
  token), `Checking whether the AI provider can be used...` before the first answer, and
  `There is no AI provider in this application.` when there is no copilot.
- *Command*: `OpenSettingsCommand` is the command that was passed in, the settings command of the shell, which opens the
  settings dialog on its first page, the AI provider.

| Status of the check | State | Words |
| --- | --- | --- |
| Not configured | Needs attention | not set up |
| Copilot CLI missing | Needs attention | CLI not found |
| Copilot CLI too old | Needs attention | CLI too old |
| Copilot not signed in | Needs attention | sign-in needed |
| Copilot runtime unavailable | Error | unavailable |
| Ollama unreachable | Needs attention | Ollama not running |
| Ollama model missing | Needs attention | model missing |
| Ollama context too small | Needs attention | small context |
| Model settings invalid | Needs attention | model not usable |
| Model lacks vision | Needs attention | cannot see pictures |
| Any other status | Needs attention | unavailable |

An open point of the redesign: when the application runs with Ollama and no copilot, the chat panel has no copilot, so
the pill shows `Error` and the word `unavailable` there too; the pill reports on the chat panel's check and does not run
one.

**ProviderPillView** (`AppShellSubsystem/ProviderPillView.axaml` and `.axaml.cs`): a user control whose data context is
the view model; its only code is the call that loads the XAML. It holds one `Button` (`ProviderPill`) with the
`PillButton` control theme, at least 26 pixels high, whose command is `OpenSettingsCommand`, whose tooltip is
`ToolTipText`, and which is marked as a part of the window that takes input
(`WindowDecorationProperties.ElementRole="User"`) so that the title bar does not take a click on it for a drag of the
window. In the button, left to right:

- *The dot*: a panel of 14 pixels with a halo of 14 and a core of 8 pixels. By the class that follows the state the core
  is green (`CpSuccess`) with a faint halo when ready, amber (`CpWarning`) when attention is needed, red (`CpDanger`)
  when in error, and a hollow ring in the accent text color (`CpAccentText`) while checking. The words next to the dot
  say the same, so the color is never the only sign.
- *The words*: the provider name in bold (`ProviderPillName`) and the separated detail (`ProviderPillDetail`), which is
  trimmed to 200 pixels and is amber or red in the two states that need it.
- *A gear icon* that shows that the pill leads to the settings: one `Path` of 14 pixels drawn as lines (the outline of
  the gear and a circle for its hub, `CpIconSettingsOutline`, stroke 1.3 in the secondary text color), not the filled
  glyph of `CpIconSettings`, whose two thin rings run together at this size. The pill is a grid (the dot, the words, the
  gear) and not a stack, so a long model name is trimmed with an ellipsis and the gear keeps its place at the right end.

**Placement**: `MainWindowView` puts the pill in a `ContentControl` (`ProviderPillHost`) in the right of the title bar,
with a 16 pixel margin, so that it stays inside the edge of the bar and clear of the system buttons.

**AutomationIds**: `ProviderPill` (the button), `ProviderPillHost` (the host in the title bar), `ProviderPillName`, and
`ProviderPillDetail`.

#### Key Methods

**Constructor**: refuses a missing chat panel, settings service, or command, subscribes to the property changes of the
chat panel and to the `Changed` event of the settings, and remembers the synchronization context of the creating thread.

**State** (property): the rule above. Nothing is cached, so the answer is always the current one.

**OnChatPropertyChanged**: when the chat panel announces a change of `Readiness` or `IsCheckingReadiness`, announces a
change of every property of the pill (an empty property name), because the state, the words, the tooltip, and the name
all depend on the same check.

**OnSettingsChanged**: announces a change of every property, because another provider or model may now be chosen.

**RunOnOwnerThread**: runs the announcement directly when it is already on the thread that created the pill, or when the
pill was created without a synchronization context (as in unit tests), and otherwise posts it to that context, because
the settings and the check may announce from another thread and the view may be touched only from its own. An
announcement that arrives after `Dispose` is dropped.

**Dispose**: marks the pill disposed and unsubscribes from the chat panel and the settings.

#### Error Handling

The view model has no operation that can fail. Missing constructor arguments are refused with an
`ArgumentNullException`. A provider that cannot be used is not an error of the pill: it is one of its states, shown with
words and a colored dot and explained by the tooltip. A status that the pill does not know falls back to `unavailable`.

#### Dependencies

- **ChatPanel** — `ChatPanelViewModel` supplies `HasCopilot`, `IsCheckingReadiness`, and `Readiness` and their change
  notifications; the pill reads the check and never runs one.
- **AgentContract** — `AgentReadinessStatus` and `AgentReadinessReport` (status and message of the check).
- **SettingsService** — the chosen provider and model and the `Changed` event.
- **MainWindowShell** — the settings command that using the pill runs.
- **ThemeResources** — the `PillButton` control theme, the state colors, the settings icon, and the sizes.
- **CommunityToolkit.Mvvm** — `ObservableObject`, the base of the view model.

#### Callers

- **ApplicationComposition** creates the pill over the chat panel of the application.
- **MainWindowShell** holds it as `ProviderPill` and disposes it. The `Welcome` unit follows it to show its notice about
  the AI provider and takes the provider name and the tooltip sentence from it.
- **MainWindowView** shows it in the title bar.
