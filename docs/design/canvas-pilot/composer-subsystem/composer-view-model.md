### ComposerViewModel

![Composer Structure](ComposerSubsystemView.svg)

#### Purpose

`ComposerViewModel` is the orchestration of the message that the user is composing, without a window. It owns the
`ComposerDocument`, the `ComposerHistory`, and the `AttachmentIntake` of the open project, receives what the editing
control reports, and turns each action of the user into exactly one undo step. It also connects the message to the marks
of the picture, to dictation, and to sending, so that every rule that the user can observe is verified without a
display: the editing control only reports and shows.

#### Data Model

- `Document` (read only): the `ComposerDocument` the control shows. Every change goes through the view model.
- `CaretOffset` and `SelectionLength`: where the next text, dictated block, drop, or paste goes; the selection starts at
  the caret. Values are kept inside the message. Undo and redo set the caret to the one the step stored.
- State for binding: `HasProject`, `ProjectFolder`, `CanEdit` (a project is open and nothing is being sent),
  `HasContent` (a word or a chip), `CanSend`, `CanUndo`, `CanRedo`, `IsSending`, `StatusText` and `HasStatus` (one
  sentence about the last action), `DictationState` (`Idle`, `Starting`, `Listening`, `Finishing`), `IsDictating`, and
  `LivePartialText` (the words recognized so far while the talk control is held), `MarkChipIds` (the marks that are part
  of the message).
- Commands: `UndoCommand`, `RedoCommand`, and `SendCommand`, whose `CanExecute` follows the state above.
- Events: `Changed` (once for each action that changed the message, the caret, or what can be undone),
  `UndoRedoStateChanged` (from `IUndoRedoTarget`), and `MessageSent` with a `SentMessage`.
- Collaborators, all given to the constructor: an `IMessageBuilder`, the `MarkRegistry`, an `IDictationService`, a
  function that makes the `IAttachmentStore` of a project folder (the `AttachmentStore` by default), and an optional
  logger.
- Supporting types of the unit: `IUndoRedoTarget` (`CanUndo`, `CanRedo`, `Undo`, `Redo`, `UndoRedoStateChanged`), which
  the Edit menu and the keys reach the focused part through; `IPushToTalkTarget` (`BeginPushToTalkAsync`,
  `EndPushToTalkAsync`), which the talk button and key reach; `IMessageBuilder` (`BuildAsync`), which the composer owns
  and the payload builder implements; and `SentMessage` (the snapshot, the marks whose chips are in it, the staged
  attachments, the payload as an object, and the time) with `MessageSentEventArgs`. All are immutable or contracts.
- Internal state: the attachment store and intake of the open project, counters of project changes and of dictations
  that were given up, the cancellation of a start that is under way, the session that is listening, and the context of
  the thread that pressed the talk button.

#### Key Methods

**One action, one step.** `ApplyTextChange(offset, removedLength, text, caretBefore)` makes a `ComposerEdit.TypeText`,
or a `ComposerEdit.Delete` when no text was inserted, and executes it through the history. `InsertTyped`, `Delete`,
`DeleteBackward`, and `DeleteForward` are conveniences for the caret and the selection: Backspace and Delete remove the
selection, or one position, as one step. `AddDictatedBlock` makes a `Dictate` edit at the caret, over the selection.
`HandleDrop(content, offset)` and `HandlePaste(content)` give the content to the intake and execute one `Drop` edit of
everything it accepted, at the place of the drop or at the caret over the selection; a paste of plain text is the same
path with a text segment. `AddMark(mark)` checks that the registry holds that very mark and executes an `AddMark` edit
at the caret; there is no way to make a mark from text, so dictated text that looks like a chip stays text.
`DeleteChip(offset)` executes a `DeleteChip` edit. An action that changes nothing is not a step (the history ignores
empty edits), and an action that is refused (no project, or a message being sent) changes nothing. After each executed
edit the view model sets the caret from the edit and raises its notifications once.

**Undo and redo.** `Undo` and `Redo` do nothing when the history has no step. Otherwise they ask the history, clear the
status text, set the caret to the returned offset, and notify once. `CanUndo` and `CanRedo` are false while no project
is open or a message is being sent, so the menu and the keys cannot change a message that is being sent.

**Dropped and pasted content.** The status text names the first three items that were left out and the first three that
were accepted with a warning, each with its reason, and counts the rest; it is empty when everything was accepted. The
text of the next action replaces it.

**Project.** `SetProject(folder)` drops the draft, the history, the marks, and the saved pictures of the old project,
gives a dictation in progress up, and makes the store and intake of the new folder through the factory; the saved
pictures that an earlier session left in the new project are removed. The same folder again changes nothing. A folder
that the factory refuses (`ArgumentException`, `IOException`, `UnauthorizedAccessException`, `NotSupportedException`)
leaves no project and says why. `ClearDraft` empties the draft, the history, the marks, and the saved pictures without
sending.

**Sending.** `SendAsync(token)` refuses, with a sentence, when there is no project, when a talk control is held, or when
there is nothing but white space; a second call while one runs returns false. It then sets `IsSending` (the draft cannot
be changed), takes the snapshot, and on a thread-pool thread has the store put the attachment chips into the project,
all or none. A refused staging stops the send with the sentence of the store, which names the attachment. Otherwise it
passes the snapshot, the marks whose chips are in the message (`MarkRegistry.ActiveMarks`), and the staged attachments
to the `IMessageBuilder`. If the project was changed in the meantime the message is not sent into another project. Only
now does it commit: `ComposerHistory.CompleteSend` empties the document and the history, the registry is cleared, the
caret goes to the start, saved pictures that no chip refers to are removed, and, after `IsSending` is reset and the
observers are told, `MessageSent` is raised with the sent message. A cancellation, a failure of the builder, or any
other failure leaves the draft, the history, and the marks as they were and puts the reason into the status text; the
same draft can be sent again (attachments that were already put into the project stay there, and a second staging gives
them new names).

**Dictation.** `BeginPushToTalkAsync` ignores a press while sending or while a dictation is starting, listening, or
finishing. It asks the service whether dictation can work and shows the message when it cannot. Otherwise it enters
`Starting`, remembers the synchronization context of the caller, and awaits `IDictationService.BeginAsync` with a
cancellation token. The outcome is one of: a session (enter `Listening` and subscribe to its partial text), no session
with a reason (show it, return to `Idle`), a cancellation (return to `Idle` silently), or an exception (log it, show a
sentence, return to `Idle`). `EndPushToTalkAsync` in `Starting` records the release and cancels the start; if the
session arrives anyway, `BeginPushToTalkAsync` ends it at once, so the words are not lost. In `Listening` it
unsubscribes, enters `Finishing`, awaits `session.EndAsync`, disposes the session, shows any problem the session
reported, and adds the final text as one dictated block; an empty text adds nothing and says that nothing was
recognized. A release in any other state does nothing. The recognizer raises `PartialTextChanged` on its own thread: the
handler copies the text and posts the change to the captured context (or runs it directly on that very thread or without
a context), and applies it only while the same dictation is listening, so words that arrive after the release are
dropped. `DiscardDictation` (project change, clearing the draft, disposal) bumps the counter, cancels a start, releases
a listening session in the background, and returns to `Idle`; a start or an end that was under way then adds nothing.

#### Error Handling

Programming errors throw: null arguments (`ArgumentNullException`), a mark that is not in the registry
(`ArgumentException`), an offset outside the message (`ArgumentOutOfRangeException`), and a position that is not a chip
passed to `DeleteChip` (`ArgumentException`). Everything that depends on the machine or the user is a status text and a
`false` result: no project, a refused drop, a failed staging or build, a canceled send, a dictation that cannot start or
that fails. File system failures when saved pictures are removed are logged and ignored, because the next cleanup finds
the files again. The view model never throws into the interface thread from the recognizer's events or from a background
send. It is owned by the interface thread and is not safe for concurrent use.

#### Dependencies

- **ComposerDocument** and **ComposerHistory**: the message and its undo steps; the `ComposerEdit` factories.
- **AttachmentIntake** and **AttachmentStore** (through `IAttachmentStore`): dropped and pasted content, saved pictures,
  and the copy of attachments into the project at send.
- **MarkRegistry** of `Annotation`: the marks that belong to the message, cleared at send.
- **DictationService** of `Speech` (through `IDictationService`): listening and the words.
- **IMessageBuilder**, owned here and implemented by **PayloadBuilder**: the sent message; the unit does not depend on
  the `Payload` subsystem.
- **RollingFileLogger**: diagnostics.

#### Callers

- The `ApplicationComposition` creates it, follows the open project with `SetProject`, and disposes it.
- The editing control and the chat panel call the editing methods, show its state, and handle
  `MessageSent`; the window finds it as the `IUndoRedoTarget` for the Edit menu and as the `IPushToTalkTarget` for the
  talk key, which a later step adds.
