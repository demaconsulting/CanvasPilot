## Chat

### Verification Approach

The `Chat` subsystem is verified by integration tests defined in `ChatSubsystemTests.cs`. The tests use the real units
around the panel: the real `ImageRenderService` draws a Mermaid diagram in a temporary project, the real
`AnnotationOverlayViewModel` and `MarkRegistry` make a mark around one of its nodes, the real `ComposerViewModel` takes
typed text, a dropped file, and the mark, the real `PayloadBuilder` and `MarkImageComposer` build the payload, and the
real `ChatPanelViewModel` and `PayloadInspectorViewModel` show the result. The only substitute is the dictation service,
because speech needs a model and a microphone; it is the hand-written fake that the composer tests use, with a session
that says words and ends with a text. The tests check the observable result: what the transcript holds, which files are
in the project, what the payload and the inspector hold, and that the draft is empty or kept. The views are verified by
the headless UI tests described in the unit verification documents, and the system test that composes the whole
application is described in the system verification.

### Test Environment

A temporary folder per test is the project and holds the file that is dropped from outside it. No network, display,
microphone, speech model, or special permission is needed.

**Checks by hand before a release** (they need a person, a real microphone, and a real model; they are not evidence for
a requirement):

1. *Real dictation.* With a speech model downloaded and chosen on the Speech page and a microphone connected, place the
   panel in the window (a later step) or run the panel in the test harness, hold the talk button, say a sentence, and
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

### Test Scenarios

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
