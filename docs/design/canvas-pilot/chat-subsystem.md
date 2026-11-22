## Chat

![Chat Structure](ChatSubsystemView.svg)

### Overview

The `Chat` subsystem is the panel in which the user composes the messages for the AI copilot and follows its answers:
above, the transcript of the messages that were sent in the open project; below, the composer's editing control; and
under it the button that sends and the button that listens while it is held. It also joins the layer on which the user
marks the picture to the message, so that every mark becomes a chip in it, and it opens the window that shows exactly
what the assistant would receive for a message. It holds no rule of editing, sending, dictation, marking, payload
building, or of the AI provider; those belong to the `Composer`, `Speech`, `Annotation`, `Payload`, and `Agent`
subsystems, and the `Chat` subsystem connects what they do to what the user sees.

The subsystem contains three units: `ChatPanel`, the panel with the transcript, its rows, the buttons, the banner of the
readiness of the copilot, and the joining of marks, `ChatTurnRunner`, the logic of one turn of the conversation with the
copilot, and `PayloadInspector`, the window that shows a payload. It depends on `Composer` (the view model of the
message, the editing view, and the chips), `Annotation` (the registry and the layer of marks), `Payload` (the payload,
its describer, and the composer of mark pictures), `Speech` (dictation, only to prepare it), `Knowledge` (only the
Markdown reader and its panel, to draw the final text of a reply), `Agent` (only `IAgentService`, the readiness report,
and the events of a turn; never a provider, a tool, or a library), and `Logging`, and on nothing else of the
application; in particular it does not depend on `AppShell` or `Settings`, and the `Agent` and `Knowledge` subsystems do
not depend on it. `AppShell` depends on `Chat`: the composition root builds the panel, and the main window shows the
inspector, places the panel in the chat column of the workspace, and opens the settings when the panel asks. The Edit
menu and the talk key of the main window reach the composer and the panel through the seams that `Composer` owns.

### Interfaces

**ChatPanel**: The panel of the conversation.

- *Type*: In-process .NET view models (`ChatPanelViewModel`, `TranscriptEntryViewModel`, `AssistantReplyViewModel`,
  `ToolActivityRowViewModel`, `NoticeRowViewModel`) and Avalonia views that the view locator finds from them
  (`ChatPanelView`, `TranscriptEntryView`, `AssistantReplyView`, `ToolActivityRowView`, `NoticeRowView`), and
  `ChipThumbnailProvider`, which implements the composer's `IChipThumbnailProvider`.
- *Role*: Provider to the host that places it, and consumer of the composer, the registry and the layer of marks, the
  mark picture composer, dictation, and, through the turn runner, the AI copilot.
- *Contract*: The constructor takes the `ComposerViewModel`, the `MarkRegistry`, the `AnnotationOverlayViewModel`, an
  `IMarkImageComposer`, optionally the `IDictationService`, a logger, the `IAgentService` of the copilot, and a
  `TimeProvider`, and joins the layer to the composer. `Transcript` holds the rows of the conversation (one
  `TranscriptEntryViewModel` per sent message, which `SentEntries` lists, followed by the rows of the copilot);
  `SendCommand`, `CanSend`, `CanTalk`, `IsTalking`, and `TalkText` are what the buttons bind to; `BeginPushToTalkAsync`
  and `EndPushToTalkAsync` implement `IPushToTalkTarget`, so that a talk key can be given the panel later;
  `PrepareDictationAsync` loads the speech engine ahead of time; `InspectorLauncher` is set by the host and is what
  "View payload" asks. With a copilot, `IsCopilotBusy`, `StopCommand`, and `Stop` follow and stop the turn,
  `WaitForTurnAsync` completes when it has ended, `RefreshReadinessAsync` and `CheckAgainCommand` check the provider and
  set `HasReadinessIssue` and `ReadinessMessage`, and `OpenSettingsCommand` raises `OpenSettingsRequested`, which the
  host handles. `Dispose` stops following the composer and the layer and stops a turn that runs.
- *Constraints*: Owned by the user interface thread; not safe for concurrent use. It does not own the composer, the
  registry, the layer, or the copilot, which the composition root disposes. Without a copilot nothing of the copilot is
  shown and nothing is blocked.

**ChatTurnRunner**: The logic of one turn of the conversation with the AI copilot.

- *Type*: In-process .NET class (`ChatTurnRunner`) with the rows it keeps (`AssistantReplyViewModel`,
  `ToolActivityRowViewModel`, and `NoticeRowViewModel`, which belong to the `ChatPanel` unit like
  `TranscriptEntryViewModel` and share the base `TranscriptRowViewModel`) and the result type `ChatTurnOutcome`.
- *Role*: Consumer of `IAgentService` of the `Agent` subsystem and provider to the chat panel.
- *Contract*: The constructor takes the `IAgentService`, the transcript collection that rows are added to, optionally a
  `TimeProvider`, a logger, and what opens the settings from an error notice. `RunTurnAsync` gives a sent message (or an
  `AgentTurnRequest`) to the copilot and completes with the outcome when the turn has ended; `Stop` cancels the turn;
  `IsBusy` and `BusyChanged` tell when a turn runs, so that the panel can block the composer; `Dispose` stops it. It
  never throws for what the copilot does.
- *Constraints*: Owned by the user interface thread; one turn at a time; the answer text is added to the reply at most
  once every 50 milliseconds.

**PayloadInspector**: The window that shows a payload.

- *Type*: In-process .NET view model (`PayloadInspectorViewModel`, `PayloadPictureViewModel`) and an Avalonia window
  (`PayloadInspectorView`).
- *Role*: Provider; its seam `IPayloadInspectorLauncher` is owned by the subsystem and implemented by the main window.
- *Contract*: The constructor takes a `MessagePayload` and gives `Description` (the text of `PayloadDescriber`) and
  `Pictures` (the payload's pictures with captions). `CloseCommand` raises `CloseRequested`. `Dispose` releases the
  decoded pictures.
- *Constraints*: The view model is owned by the user interface thread; the window disposes it when it closes. Nothing in
  the inspector can change the payload.

### Design

**The conversation is what was sent and what the copilot answered.** The panel adds an entry for each
`ComposerViewModel.MessageSent`, with the immutable `SentMessage`, and for nothing else of the user, so a draft is never
shown as a message and nothing in the transcript can change. With a copilot, the rows of its turn follow the entry. The
rows are in memory only; they are dropped when the composer's project changes or closes, because a message belongs to
the project it was sent in, and when the panel is disposed. Nothing is written anywhere in this unit.

**The copilot's turn is its own unit.** `ChatTurnRunner` reads the events that `IAgentService` streams for a turn and
keeps the rows that show them, in the order in which things happened: the text of the copilot in a reply that grows (a
reply ends when the copilot calls a tool, and the text after the tool is a new reply), one line for each tool call that
says what was done (for example that a file was created or replaced), and notices and errors. It adds the text of a
reply at most once every 50 milliseconds, so many small pieces do not keep the interface thread busy. `Stop` keeps what
was written and marks the reply as stopped; a failure keeps it and adds an error with what to do, and the offer to open
the settings when they can help. The runner never throws for what the copilot does, and the panel only follows `IsBusy`:
while a turn runs it blocks the composer (`ComposerViewModel.BlockSending`) so that the next message waits, shows the
indicator "The copilot is working..." and the Stop button, and the composer stays editable. The chat knows the copilot
only through the contract of the `Agent` subsystem, so no provider, tool, or library reaches the user interface and the
logic is verified with a scripted copilot and a manual clock.

**What a reply looks like.** While a reply streams its text is plain and selectable, which is the cheapest thing to
grow. When the part of the answer is whole, the reply is complete and its text is read once with `KnowledgeMarkdown` and
drawn with `MarkdownBlocksPanel`, the same controls that show the knowledge cards, so that lists, headings, and code
look as the model meant. A reply that was stopped or failed stays plain with a label, because its text may end in the
middle of a construct. This is the only use of the `Knowledge` subsystem by `Chat`.

**A project change ends the turn.** A turn belongs to the project it was started in. When the project changes the panel
stops the turn, empties the transcript at once, and drops the rows that the old turn still adds until it has ended, so
the answer of one project never appears in another.

**The banner of the readiness.** `RefreshReadinessAsync` asks the copilot whether the provider that the settings choose
can be used and shows the one sentence of the answer, which already says what to do (including that Copilot support
needs an AgentKit update), in a banner that is hidden when everything is ready; a warning that does not block (a small
context window) is shown but does not block sending. The panel does not know the settings: the view asks when it is
shown, the banner's "Check again" asks, and the composition root asks when the settings change. A check that fails is a
sentence in the banner. Opening the settings is a request (`OpenSettingsRequested`) that the banner and the error rows
raise, and the shell, which owns the settings dialog, handles it, so `Chat` does not depend on `AppShell` or `Settings`.

**Sending is the composer's decision.** `SendCommand` is the composer's own command. Whether a message can be sent is
decided where the draft is, in one place, and the button and the panel only follow it; when sending fails, the composer
keeps the draft and says why in its status text, which the composer's editing control shows inside the panel. The reason
that a turn is running is given to the composer and not decided by the panel.

**Hold to talk.** The talk button listens as long as it is held and never longer: the view starts it on the press and
captures the pointer so that a release anywhere is seen, and ends it on the release, on the loss of the capture (a
dialog opened, the window was deactivated), on the loss of the focus, on the release of the space or enter key, and when
the panel leaves the window. One flag makes each hold start and end once. The rules of what listening does, the words
shown while it listens, the one dictated block, and the reasons it cannot work belong to the composer and the dictation
service; the panel's label (`Hold to talk`, `Starting... keep holding`, `Listening... let go to finish`, `Finishing...`)
follows the composer's dictation state, and the reasons appear in the composer's status line, which is part of the
panel.

**Marks are joined here.** The annotation layer and the composer do not know each other, because the composer already
uses the annotation subsystem and a dependency back would be a cycle. The panel is the one place that knows both: when
the layer reports a mark, it adds the chip with `ComposerViewModel.AddMark`, one undo step at the caret; after every
change of the composer it tells the layer which mark chips the message holds and whether marks can be made. Undoing the
chip therefore hides the mark and redoing shows it, with no undo of the marks' own.

**Pictures for chips.** `ChipThumbnailProvider` makes the small pictures of chips. A mark's picture is the area around
the mark. For the message being written it is cut from the picture the mark was drawn on with `IMarkImageComposer`; for
a sent message it is the cut-out that the payload already holds, so the transcript shows what was sent. An attached
picture is previewed by the composer's `ImageChipThumbnailProvider`, for a sent message from its copy in the project and
not from the file that may have been moved. Pictures are made in the background and kept; a failure leaves the chip's
icon.

**The inspector shows the payload itself.** `PayloadInspectorViewModel` does not describe anything: its text is the
output of `PayloadDescriber.Describe`, and its pictures are the PNG bytes of the payload, the annotated picture of every
marked file followed by the area around each of its marks, each with a caption. The window is read-only. It is shown
through `IPayloadInspectorLauncher`, the seam that the subsystem owns and that `IDialogService` of the shell extends, so
the panel needs no window and the dependency runs from `AppShell` to `Chat`.

### Dependencies

The dependencies of the units come from the SysML2 model (`sysml2tools query dependencies`) and are split into two
views. The hub suppliers `RollingFileLogger` and `PathHelpers`, which many units use, are left out of the first.

`ChatPanel` depends on `ComposerViewModel` and `ComposerEditor` (`Composer`), `MarkRegistry` and `AnnotationOverlay`
(`Annotation`), `MarkImageComposer` and `MessagePayload` (`Payload`), `DictationService` (`Speech`), `AgentContract`
(`Agent`, for the readiness report), `KnowledgeMarkdown` (`Knowledge`, for the Markdown of a reply), `PayloadInspector`,
and `ChatTurnRunner`.

![Chat panel dependencies](ChatSubsystemPanelView.svg)

`PayloadInspector` depends on `MessagePayload` and `PayloadDescriber` (`Payload`).

![Payload inspector dependencies](ChatSubsystemInspectorView.svg)

`ChatTurnRunner` depends on the rows of `ChatPanel`, on `AgentContract` (`IAgentService`, the request, and the events of
the `Agent` subsystem), on `ComposerViewModel` (the sent message), and on `MessagePayload`.

![Chat turn dependencies](ChatSubsystemTurnView.svg)
