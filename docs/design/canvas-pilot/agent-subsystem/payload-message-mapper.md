### PayloadMessageMapper

![Agent Tools](AgentToolsView.svg)

#### Purpose

`PayloadMessageMapper` turns the message that the user sent, a `MessagePayload` of the `Payload` subsystem, into the one
message of the role user that a model is given, a `ChatMessage` of Microsoft.Extensions.AI. A model sees only text and
images, so the unit writes what the user marked and attached as words that use the same numbers as the text of the
message, and adds the pictures as PNG images. It is a static class, pure and deterministic: it reads nothing but its two
arguments, writes no file, logs nothing, and holds no state.

#### The Message

The message has these contents, in this order:

1. **The text.** The text of the payload (`[Mark n]` and `[Attachment n: name]` stand where the user put them), with
   Windows line breaks made line feeds and trailing white space removed. A message with no attachment and no picture is
   this text alone.
2. **The annotated pictures**, one `DataContent` of `image/png` for each picture of the payload, in the order of the
   pictures, named `picture-{n}.png`.
3. **The close-ups of the marks**, one `DataContent` of `image/png` for each mark whose close-up is sent, named
   `mark-{number}.png`, in the order of the numbers of the marks (a tie, which cannot happen in a payload that the
   builder made, is broken by the order of the pictures), at most 10.

The block of context follows the text after a blank line, starts with `[Message context]`, and has these parts, each
only when it has something to say:

```text
Attachments (paths are relative to the project folder):
- Attachment 1: file "brief.txt" at attachments/brief.txt
Pictures with marks (all positions are in picture pixels, with the origin at the top-left corner):
- Picture 1: art/box.mmd (mermaid, 800 x 600 px)
  - Mark 1: rectangle; note "make it blue"; box x=10, y=20, w=100, h=50; close-up x=0, y=0, w=164, h=114
    Source elements: node "Start" (id A, line 3, box x=12, y=22, w=80, h=30); edge (id A-B)
Images that follow this text, in order:
- Image 1: picture 1 with all its marks drawn on it
- Image 2: close-up of mark 1 (picture 1)
Close-ups left out to save room: 2 more marks (11, 12); their boxes are given above.
```

An attachment is `file`, `folder` (with the count of files that were copied, `(3 files)` or `(1 file)`), or `image`. A
mark is `rectangle`, `ellipse`, `arrow`, or `freehand`, with `no note` when it has none. The line of the source elements
says one of: the parts (at most five, in the order the payload has them, each as its kind, its text in quotes, and in
parentheses its identifier, line, and box when they are known), followed by `more elements are left out` when the
payload says that parts were left out or there were more than five; `none under the mark` when the picture can say and
found nothing; or `not available (reason)` when the picture cannot say which parts lie under the mark (for a plain SVG
the reason is `svg provides no element positions`).

#### Decisions

**Paths are relative or absent.** The payload holds the full path of the file of a picture and the place where the user
had an attachment. The text sent to a provider has neither: attachments and pictures are written by their path relative
to the project folder with forward slashes, a picture whose file is not inside the project is written by its file name
followed by `(outside the project)`, and the original place of an attachment is never written.

**One line for every supplied value.** Names, notes, texts of elements, and identifiers are written on one line: a run
of white space or control characters becomes one space, a double quote becomes a single quote (the values are written in
double quotes), and a value over 200 characters is cut with an ellipsis. A note that contains a line that looks like the
heading of the block therefore stays inside its own line.

**Whole pixels, no culture.** Boxes are rounded to whole pixels with halves away from zero and written with the
invariant culture; a number that rounds to negative zero is written `0`. The same payload gives the same text in every
culture.

**Close-ups are capped at 10.** Images are the costly part of a request. The close-ups of the first ten marks are sent;
the text names the others and says that their boxes are given in the block, so that the model can still tell where they
are. The annotated pictures are always sent, because they are the whole of what the user saw.

**What is not mapped.** The origin of the text (typed or dictated), the hashes of the pictures, the points of a mark,
and the sizes of the attachments are not written: the model does not need them, and each costs tokens.

#### Data Model

`PayloadMessageMapper` is a `static class` with the constants `ImageMediaType` (`image/png`), `MaxCrops` (10),
`MaxSourceElementsPerMark` (5), and `MaxFieldCharacters` (200). It has no instance and no supporting type.

#### Key Methods

**Map(payload, projectFolder)**: Returns the `ChatMessage` described above. `projectFolder` is the full path of the
project folder; the unit works on the spelling of the paths and does not touch the file system. It throws
`ArgumentNullException` for a missing argument and `ArgumentException` for a blank folder.

#### Error Handling

The unit has no failure mode of its own. A payload is already checked when it is built (a mark has at least two points
and a close-up, a picture has bytes and a size), and every value that the unit writes is made safe, so any payload gives
a message.

#### Security

The text is sent to a provider, so it holds only what the model needs to understand the message: the words of the user,
the relative paths of the files that the message refers to, and the geometry of the marks. It holds no path of the
user's computer outside the project, no hash, and no setting. A value that the user or a file supplies cannot add a line
or end its quotation (see the decision above). The images are the pictures that the user chose to send by marking them;
the unit adds no other image. The bytes of the images are the bytes that the payload owns and are not copied, and the
message does not change them.

#### Dependencies

`MessagePayload` and its parts (`PayloadAttachment`, `PayloadImage`, `PayloadMark`) of the Payload subsystem, which
expose the value types `MarkKind`, `SourceElementReference` (Annotation subsystem), `RenderBox` (Preview subsystem), and
`AttachmentKind` (Composer subsystem) that the unit reads through them; and Microsoft.Extensions.AI (`ChatMessage`,
`ChatRole`, `TextContent`, `DataContent`).

#### Callers

The `AgentService` unit maps the payload of each `AgentTurnRequest` with the folder of the open project and
gives the message to the agent of the provider.
