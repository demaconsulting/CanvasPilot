### MessagePayload

![Payload Structure](PayloadSubsystemView.svg)

#### Purpose

`MessagePayload` and its parts are the contract between the chat and the assistant that is added later: the message that
was sent, in plain immutable values, with a version. They hold what the assistant receives and nothing that belongs to
the user interface, the composer, or the registry of marks.

#### Data Model

- `MessagePayload`: `Version` (the constant `ContractVersion`, 1), `Text`, `Segments`, `Attachments`, `Images`,
  `CreatedUtc`.
- `PayloadSegment` (closed): `PayloadTextPart(Text, Origin)`, `PayloadAttachmentPart(Index)`, `PayloadMarkPart(Number)`.
- `PayloadAttachment(Index, Kind, Name, OriginalPath, ProjectRelativePath, SizeBytes, FileCount)`: the index is
  one-based; the project-relative path has forward slashes.
- `PayloadImage`: `SourcePath`, `SourceFileHash` (or null), `ContentHash`, `Format`, `Width`, `Height`, `AnnotatedPng`,
  `Marks`.
- `PayloadMark`: `Number`, `Kind`, `Points`, `BoundingBox` (picture pixels, origin at the top-left corner), `Note`,
  `CropPng`, `CropBox` (picture pixels), `SourceElements`, `SourceElementsAvailable`, `SourceElementsUnavailableReason`,
  `SourceElementsTruncated`.
- Invariants: lists are `ImmutableArray` copies; the byte data is copied into arrays that the object owns and is exposed
  as `ReadOnlyMemory<byte>`; a mark has a number of at least one, at least two points, and a non-empty area image; an
  image has a non-blank path and fingerprint, a size of at least one pixel, and a non-empty picture.

#### Key Methods

The classes are values with constructors only. The constructors of `MessagePayload`, `PayloadImage`, and `PayloadMark`
check their arguments and copy what they are given. `PayloadAttachment` and the segments are records, equal when their
members are.

#### Error Handling

A constructor throws `ArgumentNullException` for a missing text, list, or format, `ArgumentOutOfRangeException` for a
number or size below one, and `ArgumentException` for a blank path or fingerprint, an empty picture, or fewer than two
points. A value that was built never throws. The unit performs no I/O and logs nothing.

#### Dependencies

- **Composer subsystem** (`ComposerDocument` unit): `TextOrigin`; (`AttachmentStore` unit): `AttachmentKind`.
- **Annotation subsystem** (`MarkRegistry` and `MarkSourceResolver` units): `MarkKind` and `SourceElementReference`.
- **Preview subsystem** (`ImageRenderService` unit): `RenderPoint` and `RenderBox`.
- **.NET BCL**: immutable arrays.

#### Callers

- `PayloadBuilder` makes the values, `PayloadDescriber` reads them, and the chat and the inspector of later steps hold
  and show them.
