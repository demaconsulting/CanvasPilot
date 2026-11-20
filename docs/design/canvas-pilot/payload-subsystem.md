## Payload

![Payload Structure](PayloadSubsystemView.svg)

### Overview

The `Payload` subsystem builds, from the message that the user sends, the one value that the assistant of a later
increment receives: the `MessagePayload`. The payload holds the text of the message with the places of its marks and
attachments, the same message as parts in order that keep typed text apart from dictated text, the attachments with
where the user had them and where they are in the project, and, for each picture that marks were drawn on, the whole
picture with the marks drawn and numbered on it, an image of the area around each mark, the geometry and the note of
each mark in the pixels of the picture, and the parts of the source file under each mark. The subsystem also describes a
payload as text, so that the user can see exactly what is sent. It has no dependency on the user interface; the chat
panel and the inspector that show the result are added later.

The subsystem contains four units: `MessagePayload`, the immutable values of the contract, `MarkImageComposer`, which
draws the marks and cuts the areas, `PayloadBuilder`, which assembles the payload, and `PayloadDescriber`, which writes
it as text. It depends on `Composer` (the snapshot of the message, the origin of text, and the staged attachments),
`Annotation` (the marks and the parts of the source under them), and `Preview` (the picture, its facts, and the
render service that encodes it), and uses CanvasNet to draw the marks.

### Interfaces

**PayloadBuilder**: The payload of a message.

- *Type*: In-process .NET class.
- *Role*: Provider.
- *Contract*: `Build` takes the `ComposerSnapshot` of the message, the marks that were made, and the `StagedAttachment`
  values that `AttachmentStore.StageForSend` returned, and returns the `MessagePayload`. `BuildAsync` does the same on a
  thread-pool thread. It also implements `IMessageBuilder`, which `Composer` owns: the same build returned as the
  `SentMessage` that `ComposerViewModel` keeps, so that `Composer` does not depend on this subsystem.
- *Constraints*: Reads only values that cannot change, so it needs no lock on the draft. Throws `ArgumentException` for
  attachments that are not those of the chips and for a chip of a mark that was not made, and
  `InvalidOperationException` when the marks cannot be drawn.

**MarkImageComposer and IMarkImageComposer**: The pictures of the marks.

- *Type*: In-process .NET interface and class.
- *Role*: Provider.
- *Contract*: `Compose` takes a picture and the marks that were drawn on it and returns a `MarkedPictureImages`: the PNG
  of the whole picture with every mark drawn and numbered, and a `MarkCrop` (PNG and area) for each mark in the order of
  the numbers.
- *Constraints*: Stateless and safe for concurrent use. A mark of another picture is refused.

**PayloadDescriber**: The payload as text.

- *Type*: In-process .NET static class.
- *Role*: Provider.
- *Contract*: `Describe` returns the payload as JSON text with a fixed order, indentation, line feeds, and number
  format, with each image replaced by its size and SHA-256 fingerprint.
- *Constraints*: Stateless and safe for concurrent use.

**MessagePayload**: The contract.

- *Type*: Immutable .NET classes and records.
- *Role*: Data.
- *Contract*: `MessagePayload` (version, time, text, segments, attachments, images), `PayloadSegment`
  (`PayloadTextPart`, `PayloadAttachmentPart`, `PayloadMarkPart`), `PayloadAttachment`, `PayloadImage`, and
  `PayloadMark`.
- *Constraints*: The values copy the lists and the bytes they are given. The version of the contract is 1.

### Design

**The payload is a contract.** The payload is the input of the assistant that is added in a later increment, so it is
made of plain, immutable values with a version, not of the objects of the user interface or of the composer. A reader
that is added later depends on this subsystem and on nothing of the chat. A change that a reader would notice changes
the version.

**What the payload holds.** The text is the message as one string with `[Mark n]` and `[Attachment n: name]` at the
places of the marks and attachments, so a reader can simply read it. The segments are the same message with its
structure: runs of text with their origin, and references to the attachments and marks. There is one `PayloadImage` for
each distinct picture that marks of the message were drawn on, recognized by its file and the fingerprint of its pixels,
in the order of the first mark that refers to it, so a message with marks on two versions of a file holds two images,
each with its own fingerprints and marks. An image holds the facts of the picture (file, SHA-256 of the file and of the
pixels, format, size), the annotated picture, and its marks in the order of their numbers. A `PayloadMark` holds the
number, the shape, the points and bounding box in the pixels of that picture, the note, the cut-out around the mark and
the area it shows, and the parts of the source under the mark, or that the picture cannot tell and why. A message
without marks has no image and the composer is never asked to draw.

**Only active marks.** A mark is part of the message exactly while its chip is in the snapshot. The builder is given all
the marks that were made, uses those whose chips are in the message, and ignores the others, together with the pictures
that only they were drawn on. A chip whose mark is not in the list, or that shows another number than its mark, is an
error, because the text would name a mark that cannot be shown.

**Drawing the marks.** `MarkImageComposer` makes an SVG document in memory that embeds the picture the user saw (the PNG
that the render service makes of its pixels) and draws every mark over it, so no file is written and the pixels away
from the marks are the pixels that were shown. Each mark is drawn as a line of its shape (rectangle, ellipse, arrow with
a head, or polyline) in a color that belongs to its number, with a wider white line under it so that it can be seen on
any background; the six colors repeat after six marks. A badge, a filled circle in the color of the mark with a white
edge and the number in white, is placed at the top-left corner of the bounding box of the mark and moved inside the
picture. The line width and the badge radius grow with the picture. CanvasNet draws the document to a surface of the
size of the picture, which is encoded as the annotated picture; if CanvasNet reports a problem, or draws another size,
the build fails instead of sending a picture that is not what the user saw.

**The area around a mark.** The area holds the bounding box of the mark and its badge, with a margin of 16 pixels, is at
least 64 pixels wide and high (grown around its center), and is moved inside the picture; where the picture is smaller
than 64 pixels the area is the whole picture. It is cut from the annotated picture, so the other marks that fall in the
area are in it, and its position and size in picture pixels are part of the payload.

**Immutability.** The values copy the lists and the byte arrays they are given and expose them as immutable arrays and
read-only memory, so a draft that goes on changing, a registry that is cleared, or a buffer that is reused cannot change
a payload that has been built. The builder reads only the snapshot, the copy of the marks list, and the staged
attachments, so it can run on a background thread while the user interface goes on.

**Describing a payload.** `PayloadDescriber` writes the payload as JSON with a fixed order of properties, two-space
indentation, line feeds, round-trip numbers, and the time in the round-trip format of the invariant culture, so that the
same payload always gives the same text. Images are not in the text: each is replaced by its size in bytes and its
SHA-256 fingerprint, and the images can be shown beside the text. Missing values are written as `null` and a picture
that cannot say what is under a mark is written with `available` false and the reason. Quotation marks, backslashes, and
control characters are escaped as JSON requires; other characters are written as they are, because the text is shown to
a person and is never put in a web page.
