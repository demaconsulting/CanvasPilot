### ChatPanel

![Chat Structure](ChatSubsystemView.svg)

#### Purpose

`ChatPanel` is the panel of the conversation. It has a header (the title, a menu of more actions, and a button that
hides the panel), shows the conversation of the open project (the messages that were sent, the answers of the AI
copilot with
the tools it called, and the notices) and, while it is empty, the prompts that help to begin, and hosts, in a card, the
editing control of the message that is being composed with the button that attaches files, the button that listens
while it is held, and the button that sends it. It also offers the button that stops the copilot, says whether the
copilot can be used, and joins the marks that are drawn on the picture to the message. While the copilot works it shows
what the copilot is doing, for how long, and, when the wait is long, why that is no sign of a fault; when the turn has
ended it shows what the turn cost under the last reply; and it offers the prompts that fit what the project has (see
`FollowUpSuggestions`). It exists so that the rules of
the composer, the dictation service, the annotation layer, and the turn of the copilot stay where they are tested
without a window, while the user sees one panel in which they work together. It can be placed in any column of a
window and knows nothing of the window around it: it asks the window to hide the column and to show the file dialog
through a request and a seam.

#### Data Model

The unit is made of these parts, all in the user interface layer:

- `ChatPanelViewModel`: the view model of the panel. It holds the composer, the annotation layer, the optional dictation
  service, the optional AI copilot (`IAgentService`) with the `ChatTurnRunner` made for it, the transcript
  (`Transcript`, an observable collection of `TranscriptRowViewModel`; `SentEntries` lists its
  `TranscriptEntryViewModel` rows), `Thumbnails` (the provider of chip pictures for the message being written), and
  `InspectorLauncher`. It exposes `CanSend`, `CanTalk`, `IsTalking`, `TalkText`, `HasTranscript`, `SendCommand` (the
  composer's command), `BeginPushToTalkAsync`, `EndPushToTalkAsync`, and `PrepareDictationAsync`, and for the copilot
  `IsCopilotBusy`, `StopCommand` and `Stop`, `IsStopping`, the busy row (`BusyText`, `BusyElapsedText`, `BusyHintText`,
  and `HasBusyHint`), `OpenSettingsCommand` with the event `OpenSettingsRequested`, `RefreshReadinessAsync` with
  `CheckAgainCommand`, `HasReadinessIssue`, `ReadinessMessage`, and `WaitForTurnAsync`. It also exposes what the title
  bar needs to show the state of the provider (`HasCopilot`, `Readiness`, the latest report including the answer that
  everything is ready, and `IsCheckingReadiness`). It implements `IPushToTalkTarget`. The constructor takes the copilot,
  the clock, the history store, the project service, the talk key (a `PushToTalkSetting` of the Speech subsystem), and
  the filter of drawings (an `IImageSourceFilter`) as optional last arguments; without a copilot the panel only appends
  the entry of a sent message and shows nothing of the copilot, without a store or a project service nothing is saved or
  restored, without a talk key the hints name the default key, and without a filter the folder of the project is not
  searched for a drawing. For the history it exposes `ClearHistoryCommand` and the text `ClearedText` of the notice that
  says that the history was cleared. For the hints that name the talk key it exposes `TalkKeyText`, `TalkToolTip`, and
  `EmptyTipText`, which follow the setting. For the conversation pane it exposes the constant `EmptyTranscriptText`
  ("What would you like to draw?") and `EmptyTipFormat`, the list `Suggestions` (the prompts that are offered now, with
  `HasSuggestions`) and `ShowSuggestions` (a conversation exists and there are prompts), `UsePrompt`, `CollapseCommand`
  with the event `CollapseRequested`, and, for the attach button, the property `AttachmentPicker` (an
  `IAttachmentPicker` that the window sets) and the generated `AttachCommand`.
- `ChatPanelView` (a `UserControl`): a column of six rows. The header is 48 pixels high and holds a round gradient
  sparkle, the title "Conversation", the overflow button (`ChatOverflowButton`, whose `MenuFlyout` holds the
  `ChatClearHistoryButton` menu item "Clear chat history"), and the collapse button (`ChatCollapseButton`). Below it are
  the readiness banner (hidden unless `HasReadinessIssue`, on the warning tint with a warning icon, with the sentence,
  an "Open Settings" button, and a "Check again" button), the transcript list, the empty state, the row with the typing
  indicator, the row of the suggested prompts, and the composer card. The empty state (shown while `HasTranscript` is
  false, centered, at most 320 wide) holds a gradient sparkle, the title (`ChatTranscriptEmpty`), the example buttons
  (`ChatExamplePrompt`, one for each prompt of `Suggestions`, which are the ways to start in a project with no drawing,
  and otherwise the ways to refine one), and the tip, whose text is the bound `EmptyTipText`. The typing indicator
  (`ChatBusyIndicator`, visible only while `IsCopilotBusy`) holds three 6 pixel dots that pulse one after the other over
  1.2 seconds, a text block with the bound `BusyText` (`ChatBusyText`, trimmed with an ellipsis when it is too long)
  next to the bound `BusyElapsedText` (`ChatBusyElapsed`, in the tertiary color), under them the hint (`ChatBusyHint`,
  wrapping, visible only while `HasBusyHint`), and the pill Stop button (`ChatStopButton`). The prompts above the
  message (`ChatSuggestion`, pill buttons in a wrapping panel, one for each prompt of `Suggestions`) are visible when
  `ShowSuggestions` is true. The composer card is a raised surface with a radius of 14 and a strong border, which turns
  to the focus ring color with a soft glow while the editing view has the focus (the style
  `Border.composer:focus-within`). It holds the composer's editing view bound to the composer (at most 240 pixels high)
  and under it a row with the attach button (`ChatAttachButton`), the talk button (`ChatHoldToTalkButton`, an icon
  button, whose tooltip is the bound `TalkToolTip`) with a key cap that shows the bound `TalkKeyText` (the key of the
  setting, `F9` by default), the hint "Enter to send" (`ChatSendHint`), and the Send button (`ChatSendButton`, an icon
  button with the accent gradient, 36 by 32). Every button that shows only an icon has an `AutomationProperties.Name`
  and a tooltip; the name of the talk button is the bound `TalkText`, so that it follows the state. While `IsTalking`
  the talk button has the class `talking`, which makes it red on the danger tint and pulses its opacity once a second.
  Its code handles the hold of the talk button and the Enter key, asks for the readiness and prepares dictation when the
  panel is shown, writes a chosen prompt through the view model and puts the keyboard focus on the editing view, and
  scrolls to the end when a row is added and while a reply grows.
- `TranscriptEntryViewModel`: one sent message. It holds the `SentMessage`, its `Payload` when it is a `MessagePayload`,
  `Items` (the runs of text with their origin and the chips, in order), `Summary` (for example `2 marks, 1 attachment`),
  `SentText` (the time), `Thumbnails`, and `ViewPayloadCommand`. It owns the picture provider it made and releases it in
  `Dispose`. `TranscriptItem` is one run of text or one chip, with the chip to ask for the picture.
- `TranscriptEntryView` (a `UserControl`): the entry as a bubble at the right in the accent color (radius 14 with a 4
  pixel bottom-right corner, at most 85 percent of the width because the entry's grid gives it the right 0.85 of the
  width), holding the blocks of the message in order (`MessageBlocks`): a selectable text block
  (`TranscriptMessageText`) for each run of words and a wrapping row for each run of chips, which hold the composer's
  `ChipView` without its delete button. The bubble sizes to its content. Below the bubble, at the right, are the "View
  payload" text button (`TranscriptViewPayloadButton`) and the sent time (`TranscriptSentTime`); the button has opacity
  0 and takes opacity 1 only while the pointer is over the entry or the focus is inside it (the style
  `Grid.entry:pointerover` and `:focus-within`), so that it is out of the way but can be reached by the keyboard. Below
  them is the summary of what the message carried.
- `ChipThumbnailProvider`: implements the composer's `IChipThumbnailProvider`. It is given a function from a mark chip
  to the PNG bytes of the area around the mark, and the composer's `ImageChipThumbnailProvider` (by default) for picture
  files.
- `IPayloadInspectorLauncher`: the seam by which the panel asks for the inspector window.
- `TranscriptRowViewModel`: the base of every row of the transcript (an `ObservableObject` and `IDisposable` that tells
  its `Kind`: `User`, `Assistant`, `Tool`, or `Notice`, and `OpensGroup`, which says whether the row opens a group, set
  by the panel only). `TranscriptEntryViewModel` is the user row; the other three are
  added by the copilot's turn (see `ChatTurnRunner`) and are made, kept, and changed on the user interface thread. The
  view of each row is found by the application's view locator from the name of its view model:
  - `AssistantReplyViewModel` and `AssistantReplyView`: a reply of the copilot with `Text`, `HasText`, and `State`
    (`Streaming`, `Completed`, `Stopped`, `Failed`) with `IsStreaming`, `IsComplete`, and `StatusLabel` (`Stopped` or
    `Failed`, otherwise empty). `AppendText` adds a piece while it streams; `Complete`, `Stop`, and `Fail` end it once,
    and after that it never changes. A reply made from restored text starts in a given state. `Blocks` is the text read
    once by `KnowledgeMarkdown` when the reply is complete, and `null` otherwise. The view is two columns, 22 pixels for
    the avatar and the rest for the content. On a row that opens a group the avatar (a round gradient sparkle) and the
    name "Copilot" are shown; a row that does not open one has neither. The status label, `Stopped` or `Failed`, is a
    small pill chip (`AssistantReplyStatus` is its text). The view shows the text as plain selectable text at 13 pixels
    while `IsComplete` is false and draws `Blocks` with the Knowledge subsystem's `MarkdownBlocksPanel` once it is true,
    without a border; stopped and failed text stays plain, because it may end in the middle of a construct.
    `UsageCaption` and `HasUsageCaption` hold the line that says what the turn cost, which the turn runner puts on the
    last reply of a turn with `SetUsageCaption` (it is no part of the text, is not saved with the history, and can be
    set after the reply has ended, and setting the same caption again changes nothing); a reply without one has none.
    The view has a fourth row in the content column that shows it as a small, wrapping text in the secondary color
    (`UsageCaptionText`, automation id `AssistantReplyUsage`), visible only while `HasUsageCaption` is true.
  - `ToolActivityRowViewModel` and `ToolActivityRowView`: one tool call with `CallId`, `ToolName`, `ArgumentSummary`,
    `Summary` (one sentence), `DisplaySummary` (the sentence without markdown backticks, which the chip shows),
    `IsRunning`, `Succeeded`, `HasImage`, `HasFileReport`, and for the view `ShowsSuccess` (ended and worked),
    `ShowsFailure` (ended and did not work), and `ElapsedText`. `UpdateRunning` changes the sentence (and the
    description of the arguments, when one is given) of a call that has no result yet, which is how the progress of a
    file that the model is writing (`Writing x.svg... 9.8 KB`) grows on one row and how the start of the call then says
    what runs; it does nothing for a call that has ended. `Finish` ends it with the result, `ReportFile` says that a
    file was created or replaced (the report stays when the tool then finishes with success and is replaced by a refusal
    when it does not), and `Abandon` ends a call that has no result. The constructor of a call that starts takes an
    optional `TimeProvider`, which `ChatTurnRunner` passes its own clock to and which is the system clock when it is
    omitted; the row takes a time stamp when it is made and the elapsed time when `Finish` runs. `ElapsedText` is the
    seconds with one decimal, such as `0.8 s`, when the call took at least a tenth of a second, and empty for a call
    that is under way, that was restored or abandoned, or that was quicker. The view is a 24 pixel pill (the shared
    `ChipBorder` theme) under the same two-column layout as a reply, with the name row above it on the first row of a
    group: a check (`SuccessIcon`) when `ShowsSuccess`, a spinner (`RunningSpinner`, a refresh icon that turns once a
    second, `ToolActivityRunning`) while `IsRunning`, or a cross (`FailureIcon`) when `ShowsFailure`, then the summary
    (`ToolActivitySummary`), and `· 0.8 s` (`ToolActivityElapsed`) when `ElapsedText` is not empty. A failed call has
    the danger color on the outline and the sentence.
  - `NoticeRowViewModel` and `NoticeRowView`: an immutable notice with `Severity` (`Information` or `Error`), `Message`,
    `Remedy`, `CanOpenSettings`, and `OpenSettingsCommand`, which calls the action that the notice was made with. The
    view is a centered raised card with an info icon, or for an error a circle icon in the danger color on the danger
    tint; the two looks are the styles `Border.notice` and `Border.notice.error` and no value is set locally. It shows
    the message, the remedy when there is one, and an "Open Settings" pill button when the settings can help.
  - `RestoredEntryViewModel` and `RestoredEntryView`: a message of the user that was restored from the saved chat
    history (a `User` row). It is made from a `ChatHistoryRow` and holds `Text` (the words with the labels `[Mark 1]`
    and `[Attachment: name]` in their places, with a space put after a label that an earlier version saved glued to the
    next word, as in `[Mark 1]Chart`), `SentText` (date and time), `Summary` (for example
    `1 mark, 1 attachment`), and `DetailsText` (one line for each mark, with its note, and each attachment, with its
    project-relative path). It has no payload, no pictures, and no "View payload" command, because the history keeps
    words only, and its view says so in a note. It is not one of `SentEntries`, because it was not sent in this session.
    Its view is the bubble of a sent message at 70 percent opacity, and on a row that opens a group (the first row of
    the restored conversation) it has a divider with the word "Earlier" above the bubble.
- `ChatHistoryMapper` (internal, static, stateless): turns the rows of the transcript of a turn into `ChatHistoryRow`s
  (a sent message becomes its words with the labels of its chips in their places, the project-relative paths of its
  attachments, and the numbers and notes of its marks; a reply with text, how it ended; a tool call that has ended, its
  name, sentence, and outcome; a notice, a reply without text, and a call that has not ended are left out), turns a
  `ChatHistoryRow` into the row of the transcript that shows it, and turns saved rows into the `AgentHistoryTurn`s for a
  new provider session: the words of a message with its mark notes and attachments, the text of a reply with a remark
  when it was stopped or failed, and a tool call as the line `[Used the tool x: sentence]` of the assistant.

**State of the panel.** The view model holds the folder of the project the transcript belongs to, so that it can tell
that the project changed, the latest readiness report, a counter of the checks, and whether a check runs, the task of
the latest turn, and
the `TranscriptSink` through which the runner adds its rows. For the prompts it holds the list that is offered now and a
flag that says that the copilot wrote a drawing in the open project (set from any thread, cleared when the project
changes). With a history store it also holds the saved conversation
of the open project (the project and its saved rows) and a generation counter that is increased whenever the
conversation is dropped or cleared. The view holds one flag, whether the talk button is held,
and the view model it follows.

#### Key Methods

**Joining the layer to the composer.** The constructor, and then every `ComposerViewModel.Changed`, sets
`IsDrawingAllowed` of the layer to `CanEdit` of the composer and calls `SetActiveMarkIds` with the composer's mark chip
identities. The panel subscribes to `MarkAdded` of the layer and calls `ComposerViewModel.AddMark`, which makes one edit
that puts the chip in the row of chips above the words, and to `ClearMarksRequested` of the layer and calls
`ComposerViewModel.DeleteMarkChips`, which deletes the chips of all marks one step each; the `Changed` that each
deletion raises makes the layer show only the marks that are left. It also subscribes to
`ComposerViewModel.MarkNoteEditRequested`, raised when the user asks from a mark's chip to change its note, and calls
`AnnotationOverlayViewModel.BeginEditNote` with the mark, which opens the note card on the layer; the composer follows
the change through the registry, so the panel passes nothing back. The handlers are removed when the panel is disposed.
Nothing else about marks is known here.

**Prompts and collapse.** `UsePrompt` writes a prompt with `ComposerViewModel.InsertTyped` at the caret (over the
selection) when the composer `CanEdit`, and returns whether it did; it throws for a null prompt. The view calls it from
the `Click` of an example or a suggestion button, which carry the prompt as their content, and puts the keyboard focus
on the editing view when it returned true, so that the user can change the words and send them. The examples and the tip
live in the empty state, which is visible while `HasTranscript` is false, so the first message hides them.
`CollapseCommand` only raises `CollapseRequested`: the panel does not know whether it stands in a column; the workspace
hides the column and shows a button to bring it back.

**Which prompts are offered.** The panel does not hold a fixed list. `Suggestions` is the plan of `FollowUpSuggestions`
(a unit of its own, see its design) for three facts that the panel finds out: whether the copilot works
(`IsCopilotBusy`), whether a picture is shown (`HasPicture` of the annotation layer), and whether the project has a
drawing. The project has a drawing when a picture is shown, when the copilot wrote a drawing during this session of the
project (the panel follows `FileWritten` of `IAgentService` and notes a file that the filter accepts; the event can come
from any thread, so only a flag is set), or, when the panel was given a filter, when the folder of the project holds one
(`ProjectDrawingProbe`, which is looked at only when nothing cheaper answers and never while the copilot works). The
panel plans again, and announces `Suggestions` and `HasSuggestions` (and, through them, `ShowSuggestions`) only when the
plan differs from the one it offers, when: it is made; the project changes (after the transcript was emptied and the
saved conversation loaded, and with the flag of the written drawing cleared, because a drawing that the copilot wrote
belongs to the project it was written in); the annotation layer reports that `HasPicture` changed; and a turn starts or
ends (a turn that ends may have written the first drawing). Nothing is offered while the copilot works, so the prompts
go away when a turn starts and come back, refined, when it has ended. The empty conversation shows `Suggestions` as its
examples (the ways to start in an empty project, or the refinements in a project that has a drawing but whose
conversation is empty); `ShowSuggestions` is `HasTranscript && HasSuggestions`, and the view model announces it whenever
either changes, so the prompts above the message show only once a conversation exists and while the copilot is idle.

**Rows that open a group.** Whenever `Transcript` changes, the panel walks it once and sets `OpensGroup` of each row to
true for the first row and for a row of kind `Assistant` or `Tool` whose predecessor is a `User` or a `Notice` row, and
to false otherwise. A reply that follows a tool call, or a tool call that follows a reply, therefore does not repeat
the name of the copilot, and the first row of a restored conversation, which comes first in the list, gets the
divider that says it is from earlier. The walk is linear in the number of rows and runs on the interface thread, where
the rows are changed.

**Attaching files.** `AttachCommand` (an asynchronous command that the toolkit generates) does nothing while
`AttachmentPicker` is `null` or the composer cannot be edited. Otherwise it asks the picker for files with the folder
of the open project as the place to start, and, when it got some and the composer can still be edited, gives them to
`ComposerViewModel.HandleDrop` as the paths of a `DroppedContent`, so that the intake classifies them, refuses what is
unsafe, makes the chips, and names what it left out exactly as it does for a drop. A choice of no files adds nothing.
The workspace sets `AttachmentPicker` to the dialog service of the window when the panel is placed in it; the button
is enabled with `CanTalk`, which is `CanEdit` of the composer.

**The Enter key.** The view listens to the key-down event of the editing view in the tunnel phase, so that it sees
the key before the editing surface. When the key is Enter without any modifier and `SendCommand` can execute, it
handles the event and executes the command. In every other case it leaves the event alone, so Shift+Enter, Enter when
nothing can be sent, and every other key reach the editing surface, which writes a line break for Enter.

**Following the composer.** The panel forwards `CanSend`, `CanEdit` and `IsSending` (as `CanTalk` and `CanSend`),
`IsDictating`, and `DictationState` (as `TalkText`) as property changes of its own. A change of `ProjectFolder` that
differs from the folder the transcript belongs to empties the transcript and, when a turn runs, stops it first (see
below), and then shows the saved conversation of the new project, if there is one (see History). `MessageSent` appends
an entry with the message and the folder of the project and, when the panel has a copilot, starts the turn.

**The turn.** With a copilot the constructor makes a `ChatTurnRunner` that adds its rows to the transcript through a
`TranscriptSink` and calls `RaiseOpenSettings` from the error notices. `MessageSent` calls the runner and keeps the task
(`WaitForTurnAsync`); the runner reports a failure as a row and never throws, and the panel logs the exception that
would still escape. When `BusyChanged` of the runner fires, the panel blocks the composer with
`ComposerViewModel.CopilotWorkingText` while a turn runs and lifts the block when it ends, announces `IsCopilotBusy`,
and tells `StopCommand` to check again. `Stop` calls the runner's `Stop`. A change of project while a turn runs sets
`TranscriptSink.Discarding`, stops the turn, and empties the transcript, so that rows that the old turn still adds are
dropped and disposed; the flag is cleared when the turn has ended. Disposing the panel stops a running turn the same
way.

**The busy row.** While a turn runs the row with the typing indicator tells what the copilot is doing, and the panel
decides none of it: it follows the status that the runner keeps (see the design of the `ChatTurnRunner`). It subscribes
to `StatusChanged` of the runner and, whenever that is raised (and when the busy state changes), announces `BusyText`
(the phase: the working text at the start, then what the copilot reports, such as thinking, writing a named file with
the bytes so far, running a tool, or waiting for the model), `BusyElapsedText` (the time since the start as `m:ss`),
`BusyHintText` and `HasBusyHint` (the hint about a long wait), and `IsStopping`. The elapsed time is ticked by the
runner's own one-second timer, made from the clock that the panel was given (the system clock in the application, a
manual clock in the tests), and not by a timer of the view, so the panel has no timer of its own and a test moves the
time by advancing the clock. The hint appears when nothing was heard from the copilot for 30 seconds and no tool runs,
and says that large drawings can take minutes, that some models show nothing until the drawing is complete, and that
Stop cancels the turn but the provider may still count the request; any sign of life from the copilot hides it. When no
turn runs all four texts are empty and the row is hidden with the indicator.

**Stopping.** `StopCommand` can run while a turn runs and is not stopping (`IsCopilotBusy && !IsStopping`). Pressing it
calls the runner's `Stop`, which at once makes `IsStopping` true and the status "Stopping...", so the busy row says that
the Stop was heard and the Stop button is disabled until the turn has ended, which can be a moment later because a
provider needs time to end what it was doing; the panel then returns to idle as it does for any turn (the reply is
marked as stopped, and the composer can send).

**What the turn cost.** The panel does nothing for the caption of the usage: the runner puts it on the last reply of the
turn, or into an information notice when the turn made no reply, and the rows show it. It is not saved in the history
(`ChatHistoryMapper` saves the words of a reply only), so a restored conversation has none.

**The talk key in the hints.** The panel does not know which key is held to talk: it is given the `PushToTalkSetting`
that the composition root shares with the window, the status bar, and the welcome screen, and shows its text (`F9`
unless the user chose another key) in the key cap of the talk button (`TalkKeyText`), in the tooltip of the talk button
(`TalkToolTip`, "Hold this button, or hold {key}, speak, and let go to add what you said to the message"), and in the
tip of the empty conversation (`EmptyTipText`, from `EmptyTipFormat`, "Drop a picture, or hold {key} and speak").
When the setting changes the panel announces all three, so the hints change as soon as the user has chosen the key in
the settings. Without a setting the panel uses the default key and nothing changes.

**History.** The panel keeps the words of the conversation on this computer through `IChatHistoryStore`, and needs
`IProjectService` to know which project is open, because the key is made from the path of the project file. When a
project is opened (the composer's `ProjectFolder` changes, or the panel is made while a project is open) and the project
service has a project with that folder, the panel loads its saved rows, shows each as a row of the transcript
(`ChatHistoryMapper.ToTranscriptRow`), and keeps them as the saved conversation. When a turn has ended (completed,
stopped, or failed; not when it was refused) the panel maps the rows of the transcript from the row of the message to
the end into history rows (the rows that the turn made are dated with the end of the turn), appends them to the saved
conversation, keeps the newest within the limits of the store (`ChatHistoryStore.Cap`), and saves it. Without a copilot
the message is saved when it is sent. A turn that ends after the project changed or the history was cleared saves
nothing, which the generation counter decides, so a turn that was stopped by a project change is not saved at all. The
saved words go to the copilot with every turn as `AgentTurnRequest.RestoredHistory`, made from the saved conversation
as it was before the message: the Agent subsystem puts them once at the start of the first message of a new provider
session and ignores them for a session that has them, so a provider session that is made again after a change of the
settings gets the whole conversation, not only what was restored. The digest is built there and lives in memory only.
A save that fails (a full disk, a folder that is refused because it lies inside the project) is a notice in the
transcript that says why, which is not saved; the conversation goes on.

**Clear chat history.** `ClearHistoryCommand` raises the generation, stops a turn that runs and drops what it still
says (the same way as a change of project), empties the transcript, asks the store to delete the history of the open
project, empties the saved conversation of the panel, calls `IAgentService.ResetConversation` (which deletes the
provider session and cancels a turn), and adds a notice that says the history was cleared, or an error notice when the
history could not be deleted. It asks nothing first: `IDialogService` has no confirmation, and the button is named for
what it does and the menu is out of the way of the buttons that are used all the time. The item is the menu item of the
`MenuFlyout` of the overflow button, so tests find it through the items of the flyout and not through the visual tree
until the menu is open. The notice is not saved.

**Readiness.** `RefreshReadinessAsync` asks `IAgentService.GetReadinessAsync`, numbers the check, sets
`IsCheckingReadiness` while it runs (and clears it when the check ends or is cancelled, but only for the latest check),
and shows the answer only when no later check was started and the panel is not disposed: `Readiness` holds the latest
report, which includes the answer that everything is ready and is what the provider pill of the title bar shows,
`HasReadinessIssue` is true for every status other than `Ready` (the warning for a small context window included, which
does not block sending), and `ReadinessMessage` is the sentence of the report, which is the actionable sentence of the
Agent subsystem, such as the one that says that Copilot is not signed in for CanvasPilot and to run `copilot` and type
`/login` to sign in (or to use the GitHub CLI or a token variable instead). A check that throws is logged by the type of
the exception and shown as "The copilot could not be checked: ..."; cancellation shows nothing. The view calls it when
it is shown, `CheckAgainCommand` calls it from the banner, and whoever knows the settings (the composition root) calls
it when they change; the panel does not depend on the Settings subsystem. `OpenSettingsCommand` raises
`OpenSettingsRequested`, which the window shell turns into its own command that opens the settings.

**Hold to talk in the view.** `BeginHold` starts a hold, when none is held and `CanTalk` is true, by calling
`BeginPushToTalkAsync` and sets the flag; on the press it also captures the pointer to the button. `EndHold` clears the
flag and calls `EndPushToTalkAsync`. The press, the release, the loss of the pointer capture, the loss of the focus, the
space and enter keys, and the removal of the panel from the visual tree call them, so that each hold starts once and
ends once whichever way it ends. The button shows only an icon, so its name (`AutomationProperties.Name`) is bound to
`TalkText` and says what it does now (`Hold to talk`, `Starting... keep holding`, `Listening... let go to finish`,
`Finishing...`); the tooltip says how to use it. While `IsTalking` is true the button has the class `talking`, which
turns it red and makes it pulse; this is the only sign of the recording besides the live words above the message.
The calls return tasks that the view does not wait for; the composer never throws for a
state the user can cause, so nothing is lost by not observing them.

**Entries.** `TranscriptEntryViewModel` walks the snapshot's segments once: runs of text keep their origin; an
attachment chip is paired with the staged attachment at the same position in the order, and a picture that was copied is
asked for from `SafePathCombine(projectFolder, relativePath)`; a mark chip stands for itself. The summary counts the
mark chips and the attachment chips. `ViewPayloadCommand` can run only when the payload is a `MessagePayload`; it makes
a `PayloadInspectorViewModel` for it and gives that to the launcher the panel has at that moment, and does nothing when
there is none. The view lays the items out as blocks one below the other: consecutive chips share one `WrapPanel` row,
and consecutive runs of words are the runs of one selectable text block; the line feed that the composer puts after its
row of chips (see `AddLeadingChips`) is the break between a row and the block that follows it and is not shown again. A
chip is not put inside a line of words, because a text line with a fixed line height is only as high as its words and
cuts a taller inline chip off, which is what the bubble did before. A chip is a `ChipView` with its delete button
hidden, whose picture is asked for through a small adapter that passes the chip of the project copy to the provider.

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
check that fails is a sentence in the banner. A history that cannot be saved or deleted is a notice (the store reports
it as a result and never throws); a saved history that cannot be read is empty (the store sets the file aside). A failed
or cancelled task of the talk button is the composer's to report. An attach button without a picker, or with a picker
that returned no files, does nothing. The logger gets facts only (the type of an exception),
never the words of a message or an answer.

**Identifiers that a test needs.** The controls of the panel have these `AutomationProperties.AutomationId` values; a
list of every id of the unit is not kept here.

- New: `ChatOverflowButton`, `ChatCollapseButton`, `ChatAttachButton`, `ChatSendHint`, `ChatExamplePrompt` (one for each
  prompt of `Suggestions`), `ChatSuggestion` (the same prompts above the message), `ChatBusyElapsed`, `ChatBusyHint`,
  `AssistantReplyUsage`, `ToolActivityElapsed`, and `ComposerPlaceholder` (of the editing view).
- Changed: `ChatClearHistoryButton` is now a menu item inside the flyout of `ChatOverflowButton`; `ChatTranscriptEmpty`
  is the title text of the empty state, whose container is hidden with it, so tests check `IsEffectivelyVisible`;
  `ChatSendButton` and `ChatHoldToTalkButton` are icon buttons, so their names (`Send` and the talk text) and not their
  content carry the words; `ToolActivityRunning` is the spinner icon and no longer a progress bar;
  `AssistantReplyStatus` is the text of the status chip.
- Unchanged: the other ids of the transcript rows (`TranscriptEntry`, `Restored...`, `Notice...`, `AssistantReply...`,
  `ToolActivity...`), the banner, the busy indicator, and the Stop button.

**Known limitations**: The transcript has no day dividers and no "Jump to latest" chip. The typing dots, the spinner of
a tool chip, and the busy line do not stop when the system asks for reduced motion.

#### Dependencies

- **ComposerViewModel**, **ComposerEditorView**, **ChipView**, **IChipThumbnailProvider**,
  **ImageChipThumbnailProvider**, **SentMessage**, **IAttachmentPicker**, **DroppedContent**, and the chips and segments
  of **ComposerDocument**: the message, its editing, what is sent, the block of sending while the copilot works, the
  seam that picks files to attach, and the way the picked files enter the message.
- **MarkRegistry**, **Mark**, and **AnnotationOverlayViewModel**: the marks of the draft, the layer that draws them, and
  its request to clear them.
- **MarkImageComposer** (through `IMarkImageComposer`), **MessagePayload**, and **PayloadInspector**: the pictures of
  marks and the payload that "View payload" shows.
- **DictationService** (through `IDictationService`): prepared ahead of the first press.
- **PushToTalkSetting** (of the Speech subsystem): the key that is held to talk, which the hints show.
- **FollowUpSuggestions** and **ProjectDrawingProbe** (the unit of the same name): the plan of the prompts that are
  offered, and the search of the project folder for a drawing, with **IImageSourceFilter** of `ProjectService` to tell
  which files are drawings.
- **AgentService** (through `IAgentService`, `AgentReadinessReport`, and the events only): the readiness and the turns,
  by way of the **ChatTurnRunner**; the panel also follows `FileWritten` of the service, to know that the copilot wrote
  a drawing.
- **KnowledgeMarkdown** and **MarkdownBlocksPanel** of the Knowledge subsystem: the final text of a reply drawn as
  Markdown. This is the only use of the Knowledge subsystem.
- **ChatHistoryStore** (through `IChatHistoryStore`, `ChatHistoryRow`, and `ChatHistoryResult`): where the words of the
  conversation are kept on this computer.
- **ProjectService** (through `IProjectService` and `ProjectInfo`): which project is open and the path of its file.
- **RollingFileLogger**: failures of preparing dictation, of a check, and of a turn.
- **PathHelpers**: the path of an attached picture in the project.
- **Avalonia** and **CommunityToolkit.Mvvm** (OTS): controls, pointer and key events, bitmaps, observable properties,
  and commands. The rows of the copilot use CommunityToolkit.Mvvm only.
- **ThemeResources** (of the AppShell subsystem, by resource keys only): the colors, tints, radii, type sizes, shadows,
  gradient, icons, and the control themes (`IconButton`, `PillButton`, `ChipBorder`) that the views of the panel use.
  The views take every color from dynamic resources, so they follow a change of the theme.

#### Callers

- The composition root (`ApplicationComposition`) builds the panel over the composer, the registry, the layer, the mark
  picture composer, the dictation service, the copilot, the history store, the project service, the talk key of the
  window's shell, and the CanvasNet filter of drawings, holds it as `Chat`, and disposes it. It calls
  `RefreshReadinessAsync` when the settings change and handles `OpenSettingsRequested` with the shell's command that
  opens the settings.
- The main window places the view in the chat column of its workspace; the talk
  key of the window calls `BeginPushToTalkAsync` and `EndPushToTalkAsync` on the panel.
- The project workspace (AppShell) connects the panel when it has both the panel and the dialog service of the window:
  it sets `InspectorLauncher` and `AttachmentPicker` to that service (which the main window implements), and handles
  `CollapseRequested` by hiding the chat column and offering a way to bring it back.
- `ChatTurnRunner` makes and changes the rows of the copilot.
