---
title: Reading marks and attachments in a message
summary: How the user points at a picture, and what the message tells you about each mark.
tags:
  - authoring
aliases:
  - mark
  - payload
see-also:
  - positions/geometry
  - canvaspilot/preview
examples:
  - name: mark-payload
    kind: snippet
    language: json
    file: mark-payload.json
    description: 'An excerpt of a message with one mark: the text, the picture facts, and the mark with its geometry and the parts of the source file under it.'
---

The user can draw marks on a picture and attach files. In the text of the message
a mark appears as `[Mark n]` and an attachment as `[Attachment n: name]`, at the
place where the user put it. Read the text with its references, and answer mark
by mark using the numbers.

## What a message holds

- **Pictures**: For each picture that has marks, the message gives the file
  (`sourcePath`), fingerprints of the file and of the pixels, the format, the
  size, and the picture with every mark drawn and numbered on it.
- **Marks**: Each mark has a `number`, a `kind` (`rectangle`, `ellipse`, `arrow`,
  or `freehand`), its `points` and `boundingBox` in the pixels of the picture
  (from the top-left corner), the `note` the user wrote, and an image of the area
  around it (`crop`).
- **Source elements**: `sourceElements` names the parts of the source file under
  the mark: `available` says whether the picture can tell, `unavailableReason`
  says why not, `truncated` says that the list was cut short, and `elements` has
  for each part its `kind`, `id`, `text`, source `line`, and `box`.

## How to use them

- Use `sourceElements` first. They name the node, mark, or line under the mark,
  which is the part of the file to change. For an arrow, the part under its head
  comes first.
- When the picture cannot tell (`available` is false, as for an SVG file or a
  photograph), use the geometry and what you see in the picture; see
  [[positions/geometry]] for how positions relate to drawn elements.
- Change only what a mark or the text asks for.
- The picture shows one version of the file, identified by `sourceFileHash`. If the
  file has changed since, read the file again before you change it.

Print an excerpt with [[#mark-payload]].
