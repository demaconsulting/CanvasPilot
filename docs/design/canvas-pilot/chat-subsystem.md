## Chat

![Chat Structure](ChatSubsystemView.svg)

### Overview

The `Chat` subsystem is the panel in which the user composes the messages for the AI copilot and follows its answers:
above, the header with the title, a menu of more actions (which holds the item that clears the chat history), and the
button that hides the panel; below it the transcript of the conversation in the open project, which offers prompts that
fit the project (ways to start while it has no drawing, ways to refine once it has one) while it is empty; while the
copilot works, a row that says what it is doing and for how long, with the Stop button; at the bottom the composer card
with the composer's editing control, and under it the button
that attaches files, the button that listens while it is held (with the key that does the same), and the button that
sends. It also joins the layer on
which the user marks the picture to the message, so that every mark becomes a chip
in it, and it opens the window that shows exactly what the assistant would receive for a message. It keeps the words of
the conversation of each project on the user's computer, outside the project, and shows them again when the project is
opened. It holds no rule of editing, sending, dictation, marking, payload building, or of the AI provider; those belong
to the `Composer`, `Speech`, `Annotation`, `Payload`, and `Agent` subsystems, and the `Chat` subsystem connects what
they do to what the user sees.

The subsystem contains five units: `ChatPanel`, the panel with the transcript, its rows, the buttons, the busy row, the
banner of the readiness of the copilot, and the joining of marks, `ChatTurnRunner`, the logic of one turn of the
conversation with the copilot (the rows, the live status, Stop, and the usage caption), `ChatHistoryStore`, which keeps
the words of the conversation of each project on this computer, `PayloadInspector`, the window that shows a payload, and
`FollowUpSuggestions`, the planner of the prompts that the panel offers. It depends on `Composer` (the view model of the
message, the editing view, and the chips), `Annotation` (the registry and the layer of marks), `Payload` (the payload,
its describer, and the composer of mark pictures), `Speech` (dictation, only to prepare it, and the setting of the talk
key, only to name it in the hints), `Knowledge` (only the Markdown reader and
its panel, to draw the final text of a reply), `Agent` (only `IAgentService`, the readiness report, and the events of a
turn; never a provider, a tool, or a library), `Project` (the open project and the path of its file, which name its
history, and the filter that tells which files are drawings), and `Logging`, and on nothing else of the application; in
particular it does not depend on `AppShell` or `Settings`, and the `Agent` and `Knowledge` subsystems do not depend on
it. `AppShell` depends on `Chat`: the
composition root builds the panel and the store, and the main window shows the inspector, places the panel in the chat
column of the workspace, hides and shows that column when the panel asks, and opens the settings when the panel asks.
The Edit menu and the talk key of the main window
reach the composer and the panel through the seams that `Composer` owns.

### Interfaces

**ChatPanel**: The panel of the conversation.

- *Type*: In-process .NET view models (`ChatPanelViewModel`, `TranscriptEntryViewModel`, `AssistantReplyViewModel`,
  `ToolActivityRowViewModel`, `NoticeRowViewModel`) and Avalonia views that the view locator finds from them
  (`ChatPanelView`, `TranscriptEntryView`, `AssistantReplyView`, `ToolActivityRowView`, `NoticeRowView`), and
  `ChipThumbnailProvider`, which implements the composer's `IChipThumbnailProvider`.
- *Role*: Provider to the host that places it, and consumer of the composer, the registry and the layer of marks, the
  mark picture composer, dictation, the setting of the talk key, the planner of the prompts (`FollowUpSuggestions`), the
  file picker that the host gives it (the `IAttachmentPicker` seam of `Composer`),
  and, through the turn runner, the AI copilot.
- *Contract*: The constructor takes the `ComposerViewModel`, the `MarkRegistry`, the `AnnotationOverlayViewModel`, an
  `IMarkImageComposer`, optionally the `IDictationService`, a logger, the `IAgentService` of the copilot, a
  `TimeProvider`, an `IChatHistoryStore`, an `IProjectService`, optionally the `PushToTalkSetting` that names the talk
  key in the hints, and an `IImageSourceFilter` that tells which files are drawings, and joins the layer to the
  composer. `Transcript` holds the rows of the conversation (one `TranscriptEntryViewModel` per sent message, which
  `SentEntries` lists, followed by the rows of the copilot, and, for a project that was opened again,
  `RestoredEntryViewModel` rows of saved text); `SendCommand`, `CanSend`, `CanTalk`, `IsTalking`, and `TalkText` are
  what the buttons bind to; `BeginPushToTalkAsync` and `EndPushToTalkAsync` implement `IPushToTalkTarget`, so that a
  talk key can be given the panel later; `PrepareDictationAsync` loads the speech engine ahead of time;
  `InspectorLauncher` is set by the host and is what "View payload" asks, and `AttachmentPicker` is set by the host and
  is what the attach button (`AttachCommand`) asks for files. `Suggestions`, `HasSuggestions`, `ShowSuggestions`, and
  `UsePrompt` serve the empty conversation and the prompts above the message, `TalkKeyText`, `TalkToolTip`, and
  `EmptyTipText` name the talk key, and `CollapseCommand` raises `CollapseRequested`, which the host handles by hiding
  the panel. With a copilot, `IsCopilotBusy`, `StopCommand`, and `Stop` follow and stop the turn, `IsStopping` tells
  that a stop was asked and the turn has not ended, `BusyText`, `BusyElapsedText`, `BusyHintText`, and `HasBusyHint` are
  what the busy row shows, `WaitForTurnAsync` completes when it has ended, `RefreshReadinessAsync` and
  `CheckAgainCommand` check the provider and set `HasReadinessIssue` and `ReadinessMessage`, and `OpenSettingsCommand`
  raises `OpenSettingsRequested`, which the host handles. With a history store and a project service, the words of each
  turn are saved when it ends, a project that is opened shows its saved conversation, and `ClearHistoryCommand` clears
  it. `Dispose` stops following the composer and the layer and stops a turn that runs.
- *Constraints*: Owned by the user interface thread; not safe for concurrent use. It does not own the composer, the
  registry, the layer, the copilot, or the history store, which the composition root disposes. Without a copilot nothing
  of the copilot is shown and nothing is blocked.

**ChatHistoryStore**: Where the words of the conversation of each project are kept on this computer.

- *Type*: In-process .NET class (`ChatHistoryStore`) behind the interface `IChatHistoryStore`, with the data types
  `ChatHistoryRow`, `ChatHistoryMark`, `ChatHistoryResult`, and the key rule `ProjectHistoryKey`.
- *Role*: Provider to the chat panel; consumer of the project information types of the `Project` subsystem
  (`ProjectInfo`), the logger, and the path helper.
- *Contract*: The constructor takes the history root (a folder, which is created at the first save), and optionally a
  logger and a `TimeProvider`. `Load(project)` gives the saved rows, oldest first; `Save(project, rows)` keeps the
  newest rows within the limits and returns a `ChatHistoryResult`; `Clear(project)` deletes the history of the project.
  `ChatHistoryStore.DefaultHistoryRoot` is `%LOCALAPPDATA%\CanvasPilot\history`. The history of a project is
  `{root}\{key}\history.json`, with `project.json` next to it, and the key is `ProjectHistoryKey.Compute` of the path of
  the project file.
- *Constraints*: Thread-safe (its operations are serialized). It never throws for what the disk or a file does. It
  refuses to work when its root is the project folder or inside it.

**ChatTurnRunner**: The logic of one turn of the conversation with the AI copilot.

- *Type*: In-process .NET class (`ChatTurnRunner`) with the rows it keeps (`AssistantReplyViewModel`,
  `ToolActivityRowViewModel`, and `NoticeRowViewModel`, which belong to the `ChatPanel` unit like
  `TranscriptEntryViewModel` and share the base `TranscriptRowViewModel` (with `Kind` and `OpensGroup`)) and the result
  type `ChatTurnOutcome`.
- *Role*: Consumer of `IAgentService` of the `Agent` subsystem and provider to the chat panel.
- *Contract*: The constructor takes the `IAgentService`, the transcript collection that rows are added to, optionally a
  `TimeProvider`, a logger, and what opens the settings from an error notice. `RunTurnAsync` gives a sent message (or an
  `AgentTurnRequest`) to the copilot and completes with the outcome when the turn has ended; `Stop` cancels the turn;
  `IsBusy` and `BusyChanged` tell when a turn runs, so that the panel can block the composer; `StatusText`,
  `ElapsedText`, `HintText`, `IsStopping`, and `StatusChanged` tell what a running turn is doing, for how long, whether
  a long wait needs explaining, and whether a stop is under way; `Dispose` stops it. When the turn has ended it puts the
  caption of what the turn cost, which `TurnUsage` sums and writes, under the last reply. It never throws for what the
  copilot does.
- *Constraints*: Owned by the user interface thread; one turn at a time; the answer text is added to the reply at most
  once every 50 milliseconds; the elapsed time is ticked once a second by a timer of the clock that it is given, and
  the status is empty when no turn runs.

**FollowUpSuggestions**: The planner of the prompts that the panel offers.

- *Type*: In-process .NET static class (`FollowUpSuggestions`) with the lists `StarterPrompts` and `RefinementPrompts`
  and the constant `ExportPrompt`, and an internal static helper (`ProjectDrawingProbe`).
- *Role*: Provider to the chat panel; consumer of `IImageSourceFilter` of the `Project` subsystem (through the probe).
- *Contract*: `Plan(hasDrawing, hasPictureShown, busy)` gives the prompts in the order to show them: none while busy,
  the starter prompts for a project without a drawing, otherwise the refinements and, only while a picture is shown, the
  export. `ProjectDrawingProbe.HasDrawing(folder, filter)` tells whether a project folder holds a drawing.
- *Constraints*: No state and no thread of its own; `Plan` is a pure function and gives a new list each time; the
  probe lists names only, to a depth of four folders, and stops at the first drawing.

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
rows are dropped when the composer's project changes or closes, because a message belongs to the project it was sent in,
and when the panel is disposed. What is kept for the next time is the words only, by the history store, on the user's
computer, outside the project (see the next paragraph); the panel itself writes nothing.

**The history is on this computer, never in the project.** A project folder may be copied or put under source control,
and the words of a conversation are the user's own, so the history of a project is kept below
`%LOCALAPPDATA%\CanvasPilot\history\{key}\`, in `history.json` (version 1, the newest 200 rows, at most 2 MB, words
only: no pictures, no payloads, no tool results) with `project.json` next to it, which names the project and its file
for the person who finds the folder. The key is the first 16 bytes of the SHA-256 hash of the full, normalized path of
the project file (lower case where the file system ignores case), as 32 hexadecimal characters. **A project that is
moved or renamed has another key and a fresh history; the old folder stays where it was.** The store refuses a root that
is the project folder or inside it, so a configuration mistake cannot put the history into the project. The panel saves
when a turn has ended (completed, stopped, or failed), shows the saved rows as text when the project is opened (a
restored message has its words, the labels of its marks and attachments, and lines for the notes and paths, but no
picture and no "View payload"), and gives the saved words to the copilot as `RestoredHistory`, which the `Agent`
subsystem turns into one digest at the start of the first message of a new provider session, in memory only.
`ClearHistoryCommand`, which the item "Clear chat history" in the menu of more actions of the header runs, deletes the
folder of the project, empties the transcript, and resets the copilot's conversation,
with no question first. The audit of what else is written into a project folder is in the design of the
`ChatHistoryStore`.

**The copilot's turn is its own unit.** `ChatTurnRunner` reads the events that `IAgentService` streams for a turn and
keeps the rows that show them, in the order in which things happened: the text of the copilot in a reply that grows (a
reply ends when the copilot calls a tool, and the text after the tool is a new reply), one line for each tool call that
says what was done (for example that a file was created or replaced), and notices and errors. It adds the text of a
reply at most once every 50 milliseconds, so many small pieces do not keep the interface thread busy. `Stop` keeps what
was written and marks the reply as stopped; a failure keeps it and adds an error with what to do, and the offer to open
the settings when they can help. The runner never throws for what the copilot does, and the panel only follows the
runner: while a turn runs it blocks the composer (`ComposerViewModel.BlockSending`) so that the next message waits,
shows the busy row (three pulsing dots, what the copilot is doing, how long, and the Stop button), and the composer
stays editable. The chat knows the copilot only through the contract of the `Agent` subsystem, so no provider, tool, or
library reaches the user interface and the logic is verified with a scripted copilot and a manual clock.

**A long turn never looks dead, and nothing is made up.** A drawing can take the model minutes, during which a window
that says nothing looks as if it had stopped. The runner therefore keeps, for the busy row, the words of what the
copilot is doing (working, thinking, the size of the response, the file that the model is writing with the bytes so far,
the tool that runs, the wait for the model, the answer), the time since the turn started, and, after 30 seconds without
any sign from the copilot while no tool runs, a hint that says that large drawings can take a few minutes, that some
models show nothing until the drawing is complete, and that Stop cancels the turn but the provider may still count the
request. The words follow only what the provider really reports; the elapsed time, ticked once a second by a timer of
the runner's own clock (so the panel has no timer and a test moves it by moving the clock), is the one sign of life that
is true for every model, also for one that is silent for the whole wait; and the hint is the honest explanation of that
silence and not a promise. The progress of a file that the model is writing is one running row of the transcript
(`Writing x.svg... 9.8 KB`) that the later reports and then the start and the end of the call go on to update, so the
transcript shows a row that grows and not a row for each report. The panel announces each of these as it changes and
shows nothing of them when no turn runs.

**Stop is seen at once.** A provider needs a moment to end what it was doing, and a Stop that shows nothing looks like a
button that does not work. From the press until the turn has ended the runner says that it is stopping ("Stopping...",
whatever the copilot still reports, with no hint), the panel shows that in the busy row and disables the Stop button, so
a second press does nothing; the turn then ends as stopped, with the text kept.

**What a turn cost.** A model can count many premium requests for one message, and a stopped turn is not given back. The
runner sums the tokens that the provider reported for the model calls of a turn, keeps the largest model multiplier (a
message is billed once whatever the number of model calls), and when the turn has ended puts one quiet caption under the
last reply, for example `12.4k in, 7.2k out, 400 reasoning, model x15 premium, 3:25`, with only the figures that were
reported (a provider that reports tokens only has no multiplier in it) and, for a turn that was stopped, the note that
the request may still count. The caption is not part of the text and is not saved with the history.

**What a reply looks like.** While a reply streams its text is plain and selectable, which is the cheapest thing to
grow. When the part of the answer is whole, the reply is complete and its text is read once with `KnowledgeMarkdown` and
drawn with `MarkdownBlocksPanel`, the controls of the `Knowledge` subsystem, so that lists, headings, and code
look as the model meant. A reply that was stopped or failed stays plain with a label, because its text may end in the
middle of a construct. This is the only use of the `Knowledge` subsystem by `Chat`.

**A project change ends the turn.** A turn belongs to the project it was started in. When the project changes the panel
stops the turn, empties the transcript at once, and drops the rows that the old turn still adds until it has ended, so
the answer of one project never appears in another.

**The banner of the readiness.** `RefreshReadinessAsync` asks the copilot whether the provider that the settings choose
can be used and shows the one sentence of the answer, which already says what to do (for example that Copilot is not
signed in and how to sign in), in a banner that is hidden when everything is ready; a warning that does not block (a
small context window) is shown but does not block sending. The panel does not know the settings: the view asks when it
is shown, the banner's "Check again" asks, and the composition root asks when the settings change. A check that fails is
a sentence in the banner. Opening the settings is a request (`OpenSettingsRequested`) that the banner and the error rows
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
service; the accessible name of the talk button, which shows only an icon (`Hold to talk`, `Starting... keep holding`,
`Listening... let go to finish`, `Finishing...`), follows the composer's dictation state, the button turns red and
pulses while it listens, and the reasons appear in the composer's status line, which is part of the
panel.

**Writing the message.** The message is written in a card that holds the composer's editing view and, under it, the
attach button, the talk button with a key cap that names the talk key, the hint "Enter to send", and the Send button.
Enter on its own sends when the composer
can send, and every other key, Shift+Enter included, goes to the editing view, so a line break is written. The attach
button asks the `IAttachmentPicker` that the host gave the panel for files, starting in the project folder, and gives
what it returns to the composer as a drop, so the rules of the intake apply to a picked file as to a dropped one; the
panel decides nothing about the files. All buttons without text have an accessible name and a tooltip. **The talk key is
named from one source.** The key cap, the tooltip of the talk button, and the tip of the empty conversation take the key
from the `PushToTalkSetting` that the composition root shares with the window, the status bar, and the welcome screen
(`F9` unless the user chose another key on the Speech page), and change when the setting does, so no hint names a key
that does not work.

**Beginning and going on, with prompts that fit the project.** While the transcript is empty the panel shows a question,
example prompts, and a tip instead of a blank list. Choosing a prompt writes it into the message at the caret and puts
the focus there, and does not send it. The prompts are not a fixed list: `FollowUpSuggestions` plans them from what the
project has, and the panel asks it again whenever one of the facts changes. A project with no drawing is offered ways to
start (a flowchart of a process, a title card, a chart of a small table), because "Add a legend" means nothing to it; a
project with a drawing is offered ways to refine it (a larger title, a legend, the style kit), and "Export as PNG" only
while a picture is shown, because the export acts on that picture; and nothing is offered while the copilot works. A
project has a drawing when a picture is shown, when the copilot wrote one in this session, or when its folder holds one
(a short, bounded search that leaves out the attachments, the style kits, and hidden folders); the panel looks again
when the project is opened, when the picture changes, and when a turn ends. The empty conversation shows these as its
examples, and, once there is a conversation and while the copilot is idle, they show above the message and work the same
way. The header has a button that raises `CollapseRequested`; the panel does not hide itself, because the column and the
way back belong to the workspace that holds it.

**Rows that belong together.** The panel tells each row of the transcript whether it opens a group (the first row, and
the first row of the copilot after a message or a notice), so that the name and the avatar of the copilot are shown
once for a run of replies and tool lines, and the first row of a restored conversation carries the divider "Earlier".
A sent message is a bubble at the right, at most 85 percent of the width, whose "View payload" button shows only while
the pointer is over the message or the focus is in it; a restored message is the same bubble at 70 percent opacity.
While a turn runs the panel shows three pulsing dots, the status of the turn (what the copilot is doing and for how
long, and the hint when the wait is long), and the Stop button where there used to be a progress bar.

**Marks are joined here.** The annotation layer and the composer do not know each other, because the composer already
uses the annotation subsystem and a dependency back would be a cycle. The panel is the one place that knows both: when
the layer reports a mark, it adds the chip with `ComposerViewModel.AddMark`, one undo step at the caret; after every
change of the composer it tells the layer which mark chips the message holds and whether marks can be made. Undoing the
chip therefore hides the mark and redoing shows it, with no undo of the marks' own. When the layer asks for all marks to
be cleared, the panel has the composer delete the chips of all marks, each as its own step, and the layer then shows
none. When the user asks from a mark's chip to change its note, the composer raises `MarkNoteEditRequested` and the
panel has the layer open the note card for that mark; the changed note reaches the chip through the mark registry. When
the layer cannot open the card (no picture is shown, or a shape is being drawn), the panel adds an information notice to
the transcript that names the mark and says what to do. The chips of a sent message in the transcript offer no change of
the note, because a sent message cannot be changed.

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

The dependencies of the units come from the SysML2 model (`sysml2tools query dependencies`) and are split into four
views. The hub suppliers `RollingFileLogger` and `PathHelpers`, which many units use, are left out of the first.

`ChatPanel` depends on `ComposerViewModel` and `ComposerEditor` (`Composer`), `MarkRegistry` and `AnnotationOverlay`
(`Annotation`), `MarkImageComposer` and `MessagePayload` (`Payload`), `DictationService` and the setting of the talk key
(`Speech`), `AgentContract`
(`Agent`, for the readiness report), `KnowledgeMarkdown` (`Knowledge`, for the Markdown of a reply), `PayloadInspector`,
`ChatTurnRunner`, `ChatHistoryStore`, and `FollowUpSuggestions`.

![Chat panel dependencies](ChatSubsystemPanelView.svg)

`PayloadInspector` depends on `MessagePayload` and `PayloadDescriber` (`Payload`).

![Payload inspector dependencies](ChatSubsystemInspectorView.svg)

`ChatTurnRunner` depends on the rows of `ChatPanel`, on `AgentContract` (`IAgentService`, the request, and the events of
the `Agent` subsystem, which include the progress and usage events), on `ComposerViewModel` (the sent message), and on
`MessagePayload`.

![Chat turn dependencies](ChatSubsystemTurnView.svg)

`FollowUpSuggestions` depends only on the filter of drawings of `ProjectService` (`Project`), which its probe uses; the
panel depends on it.

`ChatHistoryStore` depends on the project information types of `ProjectService` (`Project`), and on the hub suppliers
`RollingFileLogger` and `PathHelpers`; the panel depends on the store and on `ProjectService`. The view shows the
`ProjectService` edges on purpose, although the project service is a hub that the other views leave out, because the
key of the history comes from the project.

![Chat history dependencies](ChatSubsystemHistoryView.svg)

### Future Direction: Undo a Copilot Turn

This is planned and is not implemented: today nothing restores the files that a turn of the copilot changed (see "Files
the Copilot Writes" in the user guide), and the Edit menu undoes only the message being written.

The design that is intended is a per-session snapshot of the files before each turn. Before a turn starts, the files
that the turn may write are recorded in a snapshot that is kept outside the project folder, with the other data of the
application, so that the project gets no history file or backup of its own; the snapshots belong to the session and are
cleared when the project is closed. Undoing a turn restores the files of its snapshot. The provider session cannot be
rewound, because the Copilot runtime keeps its own history of the conversation, so the copilot would still believe that
its changes are in place. The next message therefore carries a note that tells the copilot which files were reverted and
that their contents are those from before the turn.
