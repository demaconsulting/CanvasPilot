### PayloadDescriber

![Payload Structure](PayloadSubsystemView.svg)

#### Purpose

`PayloadDescriber` writes a payload as text, so that the user can see exactly what the assistant receives and so that
two payloads can be compared. It lists everything the payload holds except the bytes of the images, which are shown
beside the text by whatever shows the payload.

#### Data Model

- The describer is a static class without state.
- The output is JSON with a fixed order of properties: `version`, `createdUtc`, `text`, `segments`, `attachments`,
  `images`. An image lists its facts, `annotatedPng`, and `marks`; a mark lists `number`, `kind`, `points`,
  `boundingBox`, `note`, `crop` (the area and the file), and `sourceElements` (`available`, `unavailableReason`,
  `truncated`, `elements`).
- A file that is not shown as text is `{ "bytes": n, "sha256": "..." }`.

#### Key Methods

**Describe(payload)**: writes the payload with a `Utf8JsonWriter` set to indented output with two spaces and line feeds,
so that the text is the same on every computer. Numbers use the shortest text that reads back to the same number and
does not depend on the culture, the time is written in the round-trip form, and enumerations are written in lower case.
Quotation marks, backslashes, and control characters are escaped as JSON requires, and other characters are written as
they are (the relaxed escaping of the writer), because the text is shown to a person in a read-only box and is never put
in a web page or script.

#### Error Handling

A missing payload throws `ArgumentNullException`. Nothing else can fail for a payload that was built. The unit performs
no I/O and logs nothing.

#### Dependencies

- **MessagePayload** unit: the values it reads.
- **.NET BCL**: `System.Text.Json` for writing, and SHA-256 for the fingerprints of the images.

#### Callers

- The payload inspector shows the text beside the images.
