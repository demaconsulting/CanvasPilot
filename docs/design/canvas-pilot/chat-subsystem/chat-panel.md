### ChatPanel

![Chat Structure](ChatSubsystemView.svg)

#### Purpose

`ChatPanel` is the panel of the conversation. It shows the messages that were sent in the open project, hosts the
editing control of the message that is being composed, offers the button that sends it and the button that listens while
it is held, and joins the marks that are drawn on the picture to the message. It exists so that the rules of the
composer, the dictation service, and the annotation layer stay where they are tested without a window, while the user
sees one panel in which they work together. It can be placed in any column of a window and knows nothing of the window
around it.

#### Data Model

The unit is made of these parts, all in the user interface layer:

- `ChatPanelViewModel`: the view model of the panel. It holds the composer, the annotation layer, the optional dictation
  service, the transcript (`Transcript`, an observable collection of `TranscriptEntryViewModel`), `Thumbnails` (the
  provider of chip pictures for the message being written), and `InspectorLauncher`. It exposes `CanSend`, `CanTalk`,
  `IsTalking`, `TalkText`, `HasTranscript`, `SendCommand` (the composer's command), `BeginPushToTalkAsync`,
  `EndPushToTalkAsync`, and `PrepareDictationAsync`. It implements `IPushToTalkTarget`.
- `ChatPanelView` (a `UserControl`): the transcript list, an empty-transcript hint, the composer's editing view bound to
  the composer, the talk button, and the Send button. Its code handles the hold of the talk button.
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

**State of the panel.** The view model holds the folder of the project the transcript belongs to, so that it can tell
that the project changed. The view holds one flag, whether the talk button is held, and the view model it follows.

#### Key Methods

**Joining the layer to the composer.** The constructor, and then every `ComposerViewModel.Changed`, sets
`IsDrawingAllowed` of the layer to `CanEdit` of the composer and calls `SetActiveMarkIds` with the composer's mark chip
identities. The panel subscribes to `MarkAdded` of the layer and calls `ComposerViewModel.AddMark`, which makes one edit
at the caret. Nothing else about marks is known here.

**Following the composer.** The panel forwards `CanSend`, `CanEdit` and `IsSending` (as `CanTalk` and `CanSend`),
`IsDictating`, and `DictationState` (as `TalkText`) as property changes of its own. A change of `ProjectFolder` that
differs from the folder the transcript belongs to empties the transcript. `MessageSent` appends an entry with the
message and the folder of the project.

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
without a payload, does nothing. A failed or cancelled task of the talk button is the composer's to report.

#### Dependencies

- **ComposerViewModel**, **ComposerEditorView**, **ChipView**, **IChipThumbnailProvider**,
  **ImageChipThumbnailProvider**, **SentMessage**, and the chips and segments of **ComposerDocument**: the message, its
  editing, and what is sent.
- **MarkRegistry**, **Mark**, and **AnnotationOverlayViewModel**: the marks of the draft and the layer that draws them.
- **MarkImageComposer** (through `IMarkImageComposer`), **MessagePayload**, and **PayloadInspector**: the pictures of
  marks and the payload that "View payload" shows.
- **DictationService** (through `IDictationService`): prepared ahead of the first press.
- **RollingFileLogger**: failures of preparing dictation.
- **PathHelpers**: the path of an attached picture in the project.
- **Avalonia** and **CommunityToolkit.Mvvm** (OTS): controls, pointer and key events, bitmaps, observable properties,
  and commands.

#### Callers

- The composition root (`ApplicationComposition`) builds the panel over the composer, the registry, the layer, the mark
  picture composer, and the dictation service, holds it as `Chat`, and disposes it.
- The main window places the view in the chat column of its workspace and sets `InspectorLauncher` to itself; the talk
  key of the window calls `BeginPushToTalkAsync` and `EndPushToTalkAsync` on the panel.
