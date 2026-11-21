### KnowledgeMarkdown

![Knowledge Structure](KnowledgeSubsystemView.svg)

#### Purpose

`KnowledgeMarkdown` reads the Markdown of a knowledge card into blocks and runs of text that a window can draw with its
own controls. The cards use only a few constructs, so the unit is small and has no dependency on a Markdown library; a
library would add a third-party item with its own documents and tests and give nothing the cards need.

#### Data Model

- The blocks are the closed hierarchy `MarkdownBlock` with `HeadingBlock` (a level from 1 to 6 and runs),
  `ParagraphBlock` (runs), `ListItemBlock` (a marker and runs), and `CodeBlock` (a language that may be missing, and the
  text of the lines).
- A run is a `MarkdownSpan` of text and a `MarkdownSpanStyle`: `Normal`, `Bold`, `Italic`, or `Code`. The text of a run
  has no markers. A block never has an empty run or two neighbors of the same style.
- The unit holds no state.

#### Key Methods

**Parse(markdown)**: reads the text line by line, where a line break is a line feed, a carriage return, or both.

- A blank line ends the paragraph or list item that is being read.
- A line that starts with one to six `#` and then a space or the end of the line, with at most three spaces before it,
  is a heading.
- A line of backticks (three or more) opens a fenced block; the first word after the fence is its language, and a line
  that has a backtick after the fence is text and not a fence. The block ends at a line that has only backticks, at
  least as many as the opening fence, so a longer fence can hold a shorter one; a block that is not closed ends with the
  text. The lines are kept as they are, except that the spaces that came before the opening fence are taken off each,
  and nothing inside is read as another block or as a run.
- A line that starts, after any spaces, with `-`, `*`, or `+`, or with a number and `.` or `)`, and then a space and
  some text, starts a list item whose marker is kept as written. An item has no nesting level; an item that is indented
  is an item of its own.
- Any other line continues the item or paragraph before it; the lines are trimmed and joined with a space.

Inside a heading, paragraph, or item, the text is read into runs: text between backticks (a run of backticks is closed
by a run of the same length, and one space on each side of the code is not part of it) is `Code` and is not read
further; text between `**` is `Bold` and text between `*` is `Italic`, where the marker that opens is not followed by
white space and the marker that closes is not preceded by white space and is found outside code. Code inside bold or
italic text is code, and italic inside bold text is italic. A marker that has no end is kept as text.

#### Error Handling

A missing text is an `ArgumentNullException`. Any text can be read: nothing is rejected, and a marker that has no end is
text. The tests check, on every card and every example description of the catalog of the application, that no letter or
digit of the source is lost, so a construct that the reader does not know shows up as text and not as a missing piece.
The unit performs no I/O and logs nothing, and is safe for concurrent use.

#### Dependencies

None: the unit reads text and makes its own values.

#### Callers

- The window that shows the cards of a later step draws the blocks with its own controls.
