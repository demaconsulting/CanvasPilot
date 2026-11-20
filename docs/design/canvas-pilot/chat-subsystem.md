## Chat

![Chat Structure](ChatSubsystemView.svg)

### Overview

The `Chat` subsystem is the panel in which the user converses with the assistant that a later increment adds: above, the
transcript of the messages that were sent in the open project; below, the composer's editing control; and under it the
button that sends and the button that listens while it is held. It also joins the layer on which the user marks the
picture to the message, so that every mark becomes a chip in it, and it opens the window that shows exactly what the
assistant would receive for a message. It holds no rule of editing, sending, dictation, marking, or payload building;
those belong to the `Composer`, `Speech`, `Annotation`, and `Payload` subsystems, and the `Chat` subsystem connects what
they do to what the user sees.

The subsystem contains two units: `ChatPanel`, the panel with the transcript, the buttons, and the joining of marks, and
`PayloadInspector`, the window that shows a payload. It depends on `Composer` (the view model of the message, the
editing view, and the chips), `Annotation` (the registry and the layer of marks), `Payload` (the payload, its describer,
and the composer of mark pictures), `Speech` (dictation, only to prepare it), and `Logging`, and on nothing else of the
application; in particular it does not depend on `AppShell`. `AppShell` depends on `Chat`: the composition root builds
the panel, and the main window shows the inspector. The panel is not placed in the main window yet; that, the Edit menu,
and the talk key are done by a later step.

### Interfaces

**ChatPanel**: The panel of the conversation.

- *Type*: In-process .NET view models (`ChatPanelViewModel`, `TranscriptEntryViewModel`) and Avalonia views that the
  view locator finds from them (`ChatPanelView`, `TranscriptEntryView`), and `ChipThumbnailProvider`, which implements
  the composer's `IChipThumbnailProvider`.
- *Role*: Provider to the host that places it, and consumer of the composer, the registry and the layer of marks, the
  mark picture composer, and dictation.
- *Contract*: The constructor takes the `ComposerViewModel`, the `MarkRegistry`, the `AnnotationOverlayViewModel`, an
  `IMarkImageComposer`, optionally the `IDictationService` and a logger, and joins the layer to the composer.
  `Transcript` holds one `TranscriptEntryViewModel` per sent message; `SendCommand`, `CanSend`, `CanTalk`, `IsTalking`,
  and `TalkText` are what the buttons bind to; `BeginPushToTalkAsync` and `EndPushToTalkAsync` implement
  `IPushToTalkTarget`, so that a talk key can be given the panel later; `PrepareDictationAsync` loads the speech engine
  ahead of time; `InspectorLauncher` is set by the host and is what "View payload" asks. `Dispose` stops following the
  composer and the layer.
- *Constraints*: Owned by the user interface thread; not safe for concurrent use. It does not own the composer, the
  registry, or the layer, which the composition root disposes.

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

**Only what was sent is in the transcript.** The panel adds an entry for each `ComposerViewModel.MessageSent`, with the
immutable `SentMessage`, and for nothing else, so a draft is never shown as a message and nothing in the transcript can
change. The entries are in memory only; they are dropped when the composer's project changes or closes, because a
message belongs to the project it was sent in, and when the panel is disposed. Nothing is written anywhere.

**Sending is the composer's decision.** `SendCommand` is the composer's own command. Whether a message can be sent is
decided where the draft is, in one place, and the button and the panel only follow it; when sending fails, the composer
keeps the draft and says why in its status text, which the composer's editing control shows inside the panel.

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
