### PayloadInspector

![Chat Structure](ChatSubsystemView.svg)

#### Purpose

`PayloadInspector` is the window that shows the user exactly what the assistant would receive for a message that was
sent: the message with the places of its marks and attachments in the order the user placed them, the attachments, and
for each marked picture its identity, its marks with geometry, note, area, and source elements, together with the
pictures of the payload. The user can check that the message means what was intended. The unit exists as its own item
because it is a separate window with its own rules (read only, shows the payload itself, releases pictures), and because
the main window shows it, on behalf of the chat panel, through a seam.

#### Data Model

- `PayloadInspectorViewModel`: holds the `Payload`, the `Description` (the string that `PayloadDescriber.Describe`
  returns for it, made once), the `Pictures`, `HasPictures`, `Title` (`Payload`), and `CloseCommand`, which raises
  `CloseRequested`. It implements `IDisposable`.
- `PayloadPictureViewModel`: one picture, with a `Caption`, the PNG bytes of the payload (`ByteCount` says how many),
  and `Bitmap`, which is decoded the first time it is read and released by `Dispose`. Bytes that cannot be decoded give
  no bitmap and no exception.
- `PayloadInspectorView` (a `Window`): the description in a read-only multi-line text box in a monospaced font, a
  splitter, the list of pictures with their captions (or the statement that there are none), and a Close button.
- `IPayloadInspectorLauncher`: the seam by which a panel asks for the window; `IDialogService` of the shell extends it
  and the main window implements it by showing the window as a dialog it owns.

The pictures are listed in the order of the payload: for each marked file, its annotated picture, captioned with the
file name, the format, the size in pixels, and the numbers of its marks, then the area around each of its marks,
captioned with the number of the mark and the area in pixels.

#### Key Methods

**Making the pictures.** The constructor walks `Payload.Images` once, adds the annotated picture of each, and then the
cut-out of each of its marks, in order, with their captions. The bytes are not copied or changed; the bitmap of a
picture is made only when the window asks for it, so that the view model needs no graphics platform until a window shows
it.

**Closing.** `CloseCommand` raises `CloseRequested`; the window closes itself. On closing, whatever way, the window
disposes its view model, which disposes the pictures, and stops listening. `Dispose` is safe to call more than once.

#### Error Handling

Programming errors throw: a null payload or caption (`ArgumentNullException`). A picture that cannot be decoded keeps
its caption and shows no picture. The window cannot fail the application: it shows values that already exist.

#### Dependencies

- **MessagePayload** and its parts, and **PayloadDescriber**: what is shown and the text that describes it.
- **Avalonia** and **CommunityToolkit.Mvvm** (OTS): the window, controls, bitmaps, and commands.

#### Callers

- `TranscriptEntryViewModel` of the chat panel makes a view model for the payload of a message and gives it to the
  launcher.
- The main window shows the view through `IPayloadInspectorLauncher.ShowPayloadInspectorAsync` as a dialog it owns.
