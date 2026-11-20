### PayloadBuilder

![Payload Structure](PayloadSubsystemView.svg)

#### Purpose

`PayloadBuilder` assembles the `MessagePayload` of a message that is sent from what the composer held, the marks, and
the attachments in the project. It is the one place where the order of the message, the references, the pictures of the
marks, and the parts of the source under them come together.

#### Data Model

- The builder holds the `IMarkImageComposer` that draws the marks and a `TimeProvider` for the time of building, and no
  other state.
- Inputs: a `ComposerSnapshot`, the list of all `Mark` values of the draft (a copy that cannot change), and the
  `StagedAttachment` values, one for each attachment chip in the order of the chips.
- Output: a `MessagePayload`.

#### Key Methods

**Build(message, marks, attachments)**:

1. *Check the attachments.* The attachment chips of the snapshot and the staged attachments must be the same chips in
   the same order, or `ArgumentException` is thrown, because the references of the text would point at other things.
2. *Walk the message once.* For each segment, a text segment adds its text and a `PayloadTextPart` with its origin; an
   attachment chip adds `[Attachment n: name]`, numbering from one, and a `PayloadAttachmentPart`; a mark chip finds its
   mark by identity (an unknown mark, or a chip with another number than the mark, throws `ArgumentException`) and adds
   `[Mark n]` and a `PayloadMarkPart`. The marks are remembered in the order of their first chip, once each.
3. *Group the marks by picture.* The marks are grouped by the key of their picture (path and pixel fingerprint) in the
   order of the first mark of each group, so two objects of the same picture are one picture; the marks of a group are
   put in the order of their numbers.
4. *Make the images.* For each group the composer draws the marks and cuts the areas; the number of areas must equal the
   number of marks or `InvalidOperationException` is thrown. For each mark `MarkSourceResolver.Resolve` finds the parts
   of the source under it, and a `PayloadMark` is made with the geometry as the mark holds it, the note, the area, and
   the parts or the reason they are not available. A `PayloadImage` is made with the facts of the picture, the annotated
   picture, and the marks.
5. *Describe the attachments* with their index, kind, name, original path, project-relative path, size, and number of
   files.

**BuildAsync(message, marks, attachments, cancellationToken)**: validates the arguments at once and runs `Build` on a
thread-pool thread; a token that is canceled before the work starts cancels the task.

#### Error Handling

Missing arguments throw `ArgumentNullException`. Attachments that are not those of the chips, and chips of marks that
were not made or that show another number, throw `ArgumentException`. A composer that fails to draw, or returns the
wrong number of areas, gives `InvalidOperationException`. In every case nothing is built and the inputs are unchanged,
so the chat can keep the draft and show the failure. The unit performs no I/O and logs nothing.

#### Dependencies

- **ComposerDocument** unit of `Composer`: `ComposerSnapshot`, the segments, and the chips.
- **AttachmentStore** unit of `Composer`: `StagedAttachment`.
- **MarkRegistry** and **MarkSourceResolver** units of `Annotation`: the marks and the parts of the source under them.
- **MarkImageComposer** unit: draws the marks and cuts the areas.
- **MessagePayload** unit: the values it makes.
- **ImageRenderService** unit of `Preview`: the facts of the pictures.

#### Callers

- The chat panel of a later step builds the payload when the user sends a message, after it took the snapshot of the
  composer and put the attachments into the project.
