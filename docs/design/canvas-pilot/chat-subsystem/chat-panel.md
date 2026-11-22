### ChatPanel

![Chat Structure](ChatSubsystemView.svg)

#### Purpose

`ChatPanel` is the panel of the conversation. It shows the conversation of the open project (the messages that were
sent, the answers of the AI copilot with the tools it called, and the notices), hosts the editing control of the message
that is being composed, offers the button that sends it, the button that listens while it is held, and the button that
stops the copilot, says whether the copilot can be used, and joins the marks that are drawn on the picture to the
message. It exists so that the rules of the composer, the dictation service, the annotation layer, and the turn of the
copilot stay where they are tested without a window, while the user sees one panel in which they work together. It can
be placed in any column of a window and knows nothing of the window around it.

#### Data Model

The unit is made of these parts, all in the user interface layer:

- `ChatPanelViewModel`: the view model of the panel. It holds the composer, the annotation layer, the optional dictation
  service, the optional AI copilot (`IAgentService`) with the `ChatTurnRunner` made for it, the transcript
  (`Transcript`, an observable collection of `TranscriptRowViewModel`; `SentEntries` lists its
  `TranscriptEntryViewModel` rows), `Thumbnails` (the provider of chip pictures for the message being written), and
  `InspectorLauncher`. It exposes `CanSend`, `CanTalk`, `IsTalking`, `TalkText`, `HasTranscript`, `SendCommand` (the
  composer's command), `BeginPushToTalkAsync`, `EndPushToTalkAsync`, and `PrepareDictationAsync`, and for the copilot
  `IsCopilotBusy`, `StopCommand` and `Stop`, `OpenSettingsCommand` with the event `OpenSettingsRequested`,
  `RefreshReadinessAsync` with `CheckAgainCommand`, `HasReadinessIssue`, `ReadinessMessage`, and `WaitForTurnAsync`. It
  implements `IPushToTalkTarget`. The constructor takes the copilot and the clock as optional last arguments; without a
  copilot the panel only appends the entry of a sent message and shows nothing of the copilot.
- `ChatPanelView` (a `UserControl`): the readiness banner (hidden unless `HasReadinessIssue`, with the sentence, an
  "Open Settings" button, and a "Check again" button), the transcript list, an empty-transcript hint, the row with the
  indicator "The copilot is working..." and the Stop button (visible only while `IsCopilotBusy`), the composer's editing
  view bound to the composer, the talk button, and the Send button. Its code handles the hold of the talk button, asks
  for the readiness and prepares dictation when the panel is shown, and scrolls to the end when a row is added and while
  a reply grows.
- `TranscriptEntryViewModel`: one sent message. It holds the `SentMessage`, its `Payload` when it is a `MessagePayload`,
  `Items` (the runs of text with their origin and the chips, in order), `Summary` (for example `2 marks, 1 attachment`),
  `SentText` (the time), `Thumbnails`, and `ViewPayloadCommand`. It owns the picture provider it made and releases it in
  `Dispose`. `TranscriptItem` is one run of text or one chip, with the chip to ask for the picture.
- `TranscriptEntryView` (a `UserControl`): the entry, with the sent time, the "View payload" button, the words in a
  selectable text block whose inline containers hold the composer's `ChipView` without its delete button, and the
  summary.
- `ChipThumbnailProvider`: implements the composer's `IChipThumbnailProvider`. It is given a function from a mark chip
  to the PNG bytes of the area around the mark, and the composer's `ImageChipThumbnailProvider` (by default) for picture
  files.
- `IPayloadInspectorLauncher`: the seam by which the panel asks for the inspector window.
- `TranscriptRowViewModel`: the base of every row of the transcript (an `ObservableObject` and `IDisposable` that tells
  its `Kind`: `User`, `Assistant`, `Tool`, or `Notice`). `TranscriptEntryViewModel` is the user row; the other three are
  added by the copilot's turn (see `ChatTurnRunner`) and are made, kept, and changed on the user interface thread. The
  view of each row is found by the application's view locator from the name of its view model:
  - `AssistantReplyViewModel` and `AssistantReplyView`: a reply of the copilot with `Text`, `HasText`, and `State`
    (`Streaming`, `Completed`, `Stopped`, `Failed`) with `IsStreaming`, `IsComplete`, and `StatusLabel` (`Stopped` or
    `Failed`, otherwise empty). `AppendText` adds a piece while it streams; `Complete`, `Stop`, and `Fail` end it once,
    and after that it never changes. A reply made from restored text starts in a given state. `Blocks` is the text read
    once by `KnowledgeMarkdown` when the reply is complete, and `null` otherwise. The view shows the text as plain
    selectable text while `IsComplete` is false and draws `Blocks` with the Knowledge subsystem's `MarkdownBlocksPanel`
    once it is true; stopped and failed text stays plain, because it may end in the middle of a construct. A "Stopped"
    or "Failed" label is shown next to the name.
  - `ToolActivityRowViewModel` and `ToolActivityRowView`: one tool call with `CallId`, `ToolName`, `ArgumentSummary`,
    `Summary` (one sentence), `IsRunning`, `Succeeded`, `HasImage`, and `HasFileReport`. `Finish` ends it with the
    result, `ReportFile` says that a file was created or replaced (the report stays when the tool then finishes with
    success and is replaced by a refusal when it does not), and `Abandon` ends a call that has no result. The view shows
    a moving bar while the call is under way and the sentence, in a warning color when the tool failed.
  - `NoticeRowViewModel` and `NoticeRowView`: an immutable notice with `Severity` (`Information` or `Error`), `Message`,
    `Remedy`, `CanOpenSettings`, and `OpenSettingsCommand`, which calls the action that the notice was made with. The
    view shows the message, the remedy when there is one, and an "Open Settings" button when the settings can help.

**State of the panel.** The view model holds the folder of the project the transcript belongs to, so that it can tell
that the project changed, the latest readiness report and a counter of the checks, the task of the latest turn, and
the `TranscriptSink` through which the runner adds its rows. The view holds one flag, whether the talk button is held,
and the view model it follows.

#### Key Methods

**Joining the layer to the composer.** The constructor, and then every `ComposerViewModel.Changed`, sets
`IsDrawingAllowed` of the layer to `CanEdit` of the composer and calls `SetActiveMarkIds` with the composer's mark chip
identities. The panel subscribes to `MarkAdded` of the layer and calls `ComposerViewModel.AddMark`, which makes one edit
at the caret. Nothing else about marks is known here.

**Following the composer.** The panel forwards `CanSend`, `CanEdit` and `IsSending` (as `CanTalk` and `CanSend`),
`IsDictating`, and `DictationState` (as `TalkText`) as property changes of its own. A change of `ProjectFolder` that
differs from the folder the transcript belongs to empties the transcript and, when a turn runs, stops it first (see
below). `MessageSent` appends an entry with the message and the folder of the project and, when the panel has a copilot,
starts the turn.

**The turn.** With a copilot the constructor makes a `ChatTurnRunner` that adds its rows to the transcript through a
`TranscriptSink` and calls `RaiseOpenSettings` from the error notices. `MessageSent` calls the runner and keeps the task
(`WaitForTurnAsync`); the runner reports a failure as a row and never throws, and the panel logs the exception that
would still escape. When `BusyChanged` of the runner fires, the panel blocks the composer with
`ComposerViewModel.CopilotWorkingText` while a turn runs and lifts the block when it ends, announces `IsCopilotBusy`,
and tells `StopCommand` to check again. `Stop` calls the runner's `Stop`. A change of project while a turn runs sets
`TranscriptSink.Discarding`, stops the turn, and empties the transcript, so that rows that the old turn still adds are
dropped and disposed; the flag is cleared when the turn has ended. Disposing the panel stops a running turn the same
way.

**Readiness.** `RefreshReadinessAsync` asks `IAgentService.GetReadinessAsync`, numbers the check, and shows the answer
only when no later check was started and the panel is not disposed: `HasReadinessIssue` is true for every status other
than `Ready` (the warning for a small context window included, which does not block sending), and `ReadinessMessage` is
the sentence of the report, which is the actionable sentence of the Agent subsystem, such as the one that says that
Copilot support needs an AgentKit update. A check that throws is logged by the type of the exception and shown as "The
copilot could not be checked: ..."; cancellation shows nothing. The view calls it when it is shown, `CheckAgainCommand`
calls it from the banner, and whoever knows the settings (the composition root) calls it when they change; the panel
does not depend on the Settings subsystem. `OpenSettingsCommand` raises `OpenSettingsRequested`, which the window shell
turns into its own command that opens the settings.

**Hold to talk in the view.** `BeginHold` starts a hold, when none is held and `CanTalk` is true, by calling
`BeginPushToTalkAsync` and sets the flag; on the press it also captures the pointer to the button. `EndHold` clears the
flag and calls `EndPushToTalkAsync`. The press, the release, the loss of the pointer capture, the loss of the focus, the
space and enter keys, and the removal of the panel from the visual tree call them, so that each hold starts once and
ends once whichever way it ends. The calls return tasks that the view does not wait for; the composer never throws for a
state the user can cause, so nothing is lost by not observing them.

**Entries.** `TranscriptEntryViewModel` walks the snapshot's segments once: runs of text keep their origin; an
attachment chip is paired with the staged attachment at the same position in the order, and a picture that was copied is
asked for from `SafePathCombine(projectFolder, relativePath)`; a mark chip stands for itself. The summary counts the
mark chips and the attachment chips. `ViewPayloadCommand` can run only when the payload is a `MessagePayload`; it makes
a `PayloadInspectorViewModel` for it and gives that to the launcher the panel has at that moment, and does nothing when
there is none. The view lays the items out as inlines; a chip is a `ChipView` with its delete button hidden, whose
picture is asked for through a small adapter that passes the chip of the project copy to the provider.

**Pictures.** `ChipThumbnailProvider.GetThumbnailAsync` passes every chip that is not a mark to the file provider. For a
mark it makes the picture on a thread-pool thread: it calls the function, decodes the PNG to 64 pixels wide, keeps the
result by the identity of the mark (also a missing picture), and returns it. For the panel's own provider the function
finds the mark in the registry and has `IMarkImageComposer.Compose` cut the area, for an entry it takes the cut-out from
the payload by the mark's number.

#### Error Handling

Programming errors throw: null arguments (`ArgumentNullException`). Everything that depends on the machine or the user
is handled without an exception reaching the interface thread: dictation that cannot work is a status text of the
composer; a message that cannot be sent keeps its draft and has a status text; preparing dictation that fails is logged
and returns; a picture that cannot be made leaves the icon; a "View payload" without a launcher, or for a message
without a payload, does nothing; a turn that fails is an error notice with the remedy (made by the runner); a readiness
check that fails is a sentence in the banner. A failed or cancelled task of the talk button is the composer's to report.
The logger gets facts only (the type of an exception), never the words of a message or an answer.

#### Dependencies

- **ComposerViewModel**, **ComposerEditorView**, **ChipView**, **IChipThumbnailProvider**,
  **ImageChipThumbnailProvider**, **SentMessage**, and the chips and segments of **ComposerDocument**: the message, its
  editing, what is sent, and the block of sending while the copilot works.
- **MarkRegistry**, **Mark**, and **AnnotationOverlayViewModel**: the marks of the draft and the layer that draws them.
- **MarkImageComposer** (through `IMarkImageComposer`), **MessagePayload**, and **PayloadInspector**: the pictures of
  marks and the payload that "View payload" shows.
- **DictationService** (through `IDictationService`): prepared ahead of the first press.
- **AgentService** (through `IAgentService`, `AgentReadinessReport`, and the events only): the readiness and the turns,
  by way of the **ChatTurnRunner**.
- **KnowledgeMarkdown** and **MarkdownBlocksPanel** of the Knowledge subsystem: the final text of a reply drawn as
  Markdown. This is the only use of the Knowledge subsystem.
- **RollingFileLogger**: failures of preparing dictation, of a check, and of a turn.
- **PathHelpers**: the path of an attached picture in the project.
- **Avalonia** and **CommunityToolkit.Mvvm** (OTS): controls, pointer and key events, bitmaps, observable properties,
  and commands. The rows of the copilot use CommunityToolkit.Mvvm only.

#### Callers

- The composition root (`ApplicationComposition`) builds the panel over the composer, the registry, the layer, the mark
  picture composer, the dictation service, and the copilot, holds it as `Chat`, and disposes it. It calls
  `RefreshReadinessAsync` when the settings change and handles `OpenSettingsRequested` with the shell's command that
  opens the settings.
- The main window places the view in the chat column of its workspace and sets `InspectorLauncher` to itself; the talk
  key of the window calls `BeginPushToTalkAsync` and `EndPushToTalkAsync` on the panel.
- `ChatTurnRunner` makes and changes the rows of the copilot.
