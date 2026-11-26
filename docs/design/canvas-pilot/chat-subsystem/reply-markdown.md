### ReplyMarkdown

![Chat Structure](ChatSubsystemView.svg)

#### Purpose

`ReplyMarkdown` reads the Markdown text of a completed reply of the copilot into blocks and runs of text, and draws
those blocks with its own control, `MarkdownBlocksPanel`, which the chat panel uses for the replies of the copilot. It
reads headings, paragraphs, bulleted and numbered list items, fenced blocks of code, and bold, italic, and code text;
every other construct, such as a link, a table, or a quote, is kept as the text that was written. Reading only these
constructs keeps the unit small and free of a Markdown library, which would add a third-party item with its own
documents and tests. The reader holds no state and does not depend on the user interface; the control that draws the
blocks does.

#### Data Model

- The blocks are the closed hierarchy `MarkdownBlock` with `HeadingBlock` (a level from 1 to 6 and runs),
  `ParagraphBlock` (runs), `ListItemBlock` (a marker and runs), and `CodeBlock` (a language that may be missing, and the
  text of the lines).
- A run is a `MarkdownSpan` of text and a `MarkdownSpanStyle`: `Normal`, `Bold`, `Italic`, or `Code`. The text of a run
  has no markers. A block never has an empty run or two neighbors of the same style.
- The reader holds no state.
- `MarkdownBlocksPanel` is a vertical stack (6 pixels apart) with the property `Blocks`; it is made again from the
  blocks whenever they change, which is when a reply is complete, and draws nothing for `null`. It draws a text block
  for each block, so the text can be selected and copied as the text of any page can:
  - a heading is semibold, in a size that falls with its level (20, then 16, then 14 pixels for the levels 3 to 6), with
    a gap above it (`ReplyMarkdownHeading`);
  - a paragraph is wrapped text with a run for each style: bold, italic, and code, whose run is in the fixed-width font
    on a gray shade that lets the theme show through (`ReplyMarkdownText`);
  - a list item is a grid with its marker (a dot for `-`, `*`, and `+`, and the number as written otherwise) and its
    text (`ReplyMarkdownItem`), indented by 12 pixels;
  - a block of code is its lines in the fixed-width font that scrolls sideways, in a border of 8 pixel corners and 10
    pixels of padding whose background and outline are the tokens `CpSurfaceRaised` and `CpBorderSubtle` through dynamic
    resources, so that the block follows a change of the theme variant (`ReplyMarkdownCode`).
  Nothing in the panel can run or open anything.

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

**Drawing (`MarkdownBlocksPanel`)**: When `Blocks` changes, the panel removes what it drew and draws each block in
order as described in the data model. Runs become inline runs of the text block: bold and italic runs get the weight
and the style, and a code run gets the fixed-width font and the gray shade. A block that is a list item is a grid of
the marker and the text; a code block is a border around a sideways scrolling, fixed-width, unwrapped text. The panel
does not keep the blocks it drew, so the same blocks drawn again give the same controls.

#### Error Handling

A missing text is an `ArgumentNullException`. Any text can be read: nothing is rejected, and a marker that has no end is
text. A test checks, on a text full of constructs that the reader does not know, that no letter or digit of the source
is lost, so such a construct shows up as text and not as a missing piece. The one part of the source that is not shown
is the words after the language on the line that opens a fenced block, which are not part of the code.
The reader performs no I/O and logs nothing, and is safe for concurrent use. The panel draws a block that it does not
know as nothing, which cannot happen while the hierarchy of blocks is closed, and it is used on the thread of the user
interface only.

#### Dependencies

- **Avalonia** (OTS) - only for `MarkdownBlocksPanel`: the controls, the inline runs, and the dynamic resources of the
  design tokens of `ThemeResources`. The reader has none.

#### Callers

- The chat panel draws the completed replies of the copilot with the panel, over the blocks that `Parse` reads from the
  text of the reply.
