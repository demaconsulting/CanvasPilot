## Chat

### Verification Approach

The `Chat` subsystem is verified by integration tests defined in `ChatSubsystemTests.cs`. The tests use the real units
around the panel: the real `ImageRenderService` draws a Mermaid diagram in a temporary project, the real
`AnnotationOverlayViewModel` and `MarkRegistry` make a mark around one of its nodes, the real `ComposerViewModel` takes
typed text, a dropped file, and the mark, the real `PayloadBuilder` and `MarkImageComposer` build the payload, and the
real `ChatPanelViewModel` and `PayloadInspectorViewModel` show the result. The substitutes are the dictation service,
because speech needs a model and a microphone (the hand-written fake that the composer tests use, with a session that
says words and ends with a text), and, for the tests of the copilot's turn and of the readiness, the copilot itself (a
scripted implementation of `IAgentService` that streams the events that the test gives it, when it gives them, and
reports the readiness that the test chooses) and the clock of the turn runner (a manual one), because a real model is
not deterministic; the real `ChatTurnRunner`, the real panel, and the real composer are used. The history test uses the
real project service and the real `ChatHistoryStore` over a temporary folder that stands for the user's computer. The
tests check the observable result: what the transcript holds, which files are in the project, what the payload and the
inspector hold, and that the draft is empty or kept. The views are verified by the headless UI tests described in the
unit verification documents, and the system test that composes the whole application is described in the system
verification.

### Test Environment

A temporary folder per test is the project and holds the file that is dropped from outside it. No network, display,
microphone, speech model, or special permission is needed.

**Checks by hand before a release** (they need a person, a real microphone, and a real model; they are not evidence for
a requirement):

1. *Real dictation.* With a speech model downloaded and chosen on the Speech page and a microphone connected, open a
   project in the installed application, hold the talk button or the F8 key, say a sentence, and
   let go: the words appear while holding, one dictated block is added at the caret on release, one Ctrl+Z removes it,
   and the sent message shows the words with the dictated background.
2. *Windows microphone privacy.* Turn off "Let desktop apps access your microphone" in the Windows privacy settings and
   hold the talk button: the panel says that the microphone cannot be used and what to change, nothing is added, and the
   application keeps running; turn the setting on again and the same hold dictates.
3. *No model, no microphone, no native files.* With no model chosen, with the model removed, with the microphone
   unplugged, and (in a build with the native files removed) with the speech files missing, hold the talk button: each
   case shows its own reason, and none of them stops the application.
4. *Pointer and keyboard.* Hold the talk button, move the pointer far from the button, and release: listening ends. Hold
   it and press Alt+Tab: listening ends. Focus the button with Tab and hold the space key: it listens until the key is
   released.
5. *Looks.* The transcript and the panel are readable in the light and the dark theme, long messages wrap, chips in a
   sent message show their pictures, the newest message is in view after sending, and the inspector's text is selectable
   and its pictures are in proportion.
6. *The copilot panel.* With Copilot chosen in the settings on a machine that is not signed in for CanvasPilot, the
   banner above the transcript says how to sign in (`gh auth login` or a token variable); "Open Settings" opens the
   settings and "Check again" repeats the check, and on a signed-in machine the banner is hidden and the answer streams
   (the live Copilot runs are in check 10 of *Manual Verification* in the system verification). With Ollama chosen
   and the server stopped, the banner says what to do and a sent message ends with an error row that offers "Open
   Settings"; with Ollama running (when it is installed), the answer streams, "Stop" ends it and keeps the text marked
   as stopped, and the finished answer is shown with its formatting.
7. *The history on this computer.* After a conversation, close the project and open it again, and then close and start
   the application: the earlier messages appear as text (no pictures); `%LOCALAPPDATA%\CanvasPilot\history\{key}\`
   holds `history.json` and `project.json`, and the project folder holds no history file; "Clear chat history" empties
   the panel and removes the folder; a project that is moved or renamed starts with an empty history. (The composition
   root gives the panel the store; the live run is the Ollama item of *Manual Verification* in the system verification.)

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- A message with typed words, a mark around a node of a drawn diagram, and a dropped file, sent through the panel, is
  the only entry of the transcript with the words and the two chips in the order they were written and a statement of
  one mark and one attachment; the composer, the registry, and the layer are empty; the file is in the project's
  `attachments` folder.
- The payload of that message has the text with the places of the mark and the attachment, the attachment's path in the
  project, one marked picture whose fingerprint is that of the drawn picture, and a mark that has the node under it; the
  inspector for it shows the same text as the payload describer and two pictures of the sizes of the payload's.
- A message that cannot be sent because an attachment has gone leaves the draft with its text and chip, adds nothing to
  the transcript, and says which file is the problem.
- Words held to talk are shown while they are recognized, are added as one block at the caret on release, and are kept
  as dictated words, apart from the typed words, in the payload of the sent message.
- A message that is sent is given to the copilot with its payload; while the turn runs the composer cannot send but can
  be written to; the answer streams into rows after the message (text, a line for the tool that created a file, text),
  and the composer can send again when the turn has ended.
- Stopping a turn keeps the text written so far, marked as stopped, and frees the composer; a failure ends with an error
  notice with the message, a remedy, and the offer of the settings when they can help, which raises a request to open
  them.
- The banner shows the sentence of the readiness of the provider (the sign-in sentence of GitHub Copilot included), a
  warning does not block sending, and the banner is empty when the provider is ready.
- After a full scripted turn in a project made by the real project service, the words of the conversation are saved
  below the history root of the user's computer in the folder of the key of the project file, with the sidecar, and the
  project folder holds exactly the project file, its empty data folder, and the file that the copilot created; a second
  panel in the same project shows the conversation as text and gives the copilot the saved words with the next message;
  Clear deletes the history and resets the copilot; and through all of it no history file is in the project.

### Test Scenarios

**ChatSubsystem_ConverseWithCopilot_SentMessageGetsStreamedAnswerAndToolRows_ComposerWaitsMeanwhile**: The real
`ChatPanelViewModel` over the real composer (and the real payload builder) and a scripted copilot sends "Draw a box".
The copilot gets the payload of the entry. While the turn runs the panel is busy, the composer cannot send, and text
that is typed meanwhile is kept. The copilot streams "I will draw it.", calls `project_write`, the file `box.svg` is
reported as created, and it streams "Done." and completes. The rows are the entry, a reply, a tool row whose sentence is
"Created" and the path in code format, and a complete reply, and the composer can send. This scenario is tested by
`ChatSubsystem_ConverseWithCopilot_SentMessageGetsStreamedAnswerAndToolRows_ComposerWaitsMeanwhile`.

**ChatSubsystem_StopCopilotTurn_KeepsTextFreesTheComposerAndFailureHasRemedy**: The copilot streams a part of a sentence
and the turn is stopped with the panel's Stop command: the reply keeps the text and is stopped, and the composer, with
the text typed meanwhile, can send. The next message is sent and the provider fails with a missing model: the last row
is an error notice with the message, a remedy, and the offer of the settings, the composer is free, and choosing the
offer raises the request to open the settings. This scenario is tested by
`ChatSubsystem_StopCopilotTurn_KeepsTextFreesTheComposerAndFailureHasRemedy`.

**ChatSubsystem_ReportProviderReadiness_CopilotNotSignedIn_BannerSaysWhatToDoAndSettingsAreRequested**: The scripted
copilot reports that GitHub Copilot is not signed in. After the panel checks, the banner has the sign-in sentence of
`CopilotRuntimeStarter` ("Copilot is not signed in for CanvasPilot. Sign in with `gh auth login` ...") and choosing the
settings raises the request. A warning about a small context window is shown and does not block sending, and when the
copilot reports that it is ready the banner is empty. This scenario is tested by
`ChatSubsystem_ReportProviderReadiness_CopilotNotSignedIn_BannerSaysWhatToDoAndSettingsAreRequested`.

**ChatSubsystem_KeepChatHistoryOnThisComputer_TurnSavedRestoredAndClearedOutsideTheProject**: A project is made by the
real project service, and a panel with the real history store over a folder outside the project sends "Draw a box"; the
copilot creates `box.svg` and answers. The folder of the key of the project file below the history root holds
`history.json` and `project.json`; the project folder holds `.canvaspilot` (empty), the project file, and `box.svg` and
nothing else; the saved rows are a message, a reply, and a tool line. A second panel made in the same project (the
application started again) shows a restored message, a reply, and a tool row, lists no sent message, and gives the
copilot the saved words as restored history with the next message, and the project folder is as it was. Clear chat
history deletes the folder of the history, resets the copilot once, leaves the project folder as it was, and no
`history.json` exists anywhere in the project. This scenario is tested by
`ChatSubsystem_KeepChatHistoryOnThisComputer_TurnSavedRestoredAndClearedOutsideTheProject`.

**ChatSubsystem_SendMessageWithMarkAndAttachment_TranscriptAndPayloadMatchDraft**: A diagram is drawn. The user types
"Make this ", draws a rectangle around the node `Start`, types " like ", drops a file from outside the project, and
sends. The transcript has one entry whose items are the text, the mark chip, the text, and the file chip, with the
summary `1 mark, 1 attachment`; the draft, the registry, and the layer are empty; the file is in `attachments`. The
payload text is `Make this [Mark 1] like [Attachment 1: notes.txt]`, with the attachment path `attachments/notes.txt`
and one image that has the picture's fingerprint and one mark with the node `Start` as a source element; the inspector's
description is the describer's text, and its two pictures have the byte counts of the annotated picture and of the area
around the mark. This scenario is tested by
`ChatSubsystem_SendMessageWithMarkAndAttachment_TranscriptAndPayloadMatchDraft`.

**ChatSubsystem_SendMessage_AttachmentCannotBeStaged_KeepsDraftAndTranscriptUnchanged**: A file is dropped into a
message and deleted before the message is sent. Sending adds nothing to the transcript, the draft keeps its typed text
and the chip, and the status text names the file. This scenario is tested by
`ChatSubsystem_SendMessage_AttachmentCannotBeStaged_KeepsDraftAndTranscriptUnchanged`.

**ChatSubsystem_HoldToTalk_DictatedWords_AreKeptAsDictatedInTheSentPayload**: After typed text, the talk button is held,
the recognizer hears "turn the", and the button is released with the final text "turn the arrow red"; the message is
sent. The live words were "turn the"; the payload text is `Please turn the arrow red`, and its segments are the typed
text "Please " and the dictated text "turn the arrow red", in that order. This scenario is tested by
`ChatSubsystem_HoldToTalk_DictatedWords_AreKeptAsDictatedInTheSentPayload`.
