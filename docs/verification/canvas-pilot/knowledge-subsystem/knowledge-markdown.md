### KnowledgeMarkdown

#### Verification Approach

`KnowledgeMarkdown` is verified with unit tests in `KnowledgeMarkdownTests.cs` for the reader and with headless Avalonia
UI tests in `KnowledgeBrowserViewTests.cs` for the control that draws the blocks (`MarkdownBlocksPanel`). Each construct
of the dialect, and the cases at its edges, are checked on short texts whose blocks and runs are written out as a line
each, so the exact result can be compared. One test reads the body of every card and the description of every example
of the catalog of the application and checks that the letters and digits of the source are the letters and digits of
the blocks, so no text is lost or added, and that the headings, paragraphs, items, code blocks, bold, and code text
that the cards use occur in it. The UI tests put the blocks that the reader made of a small text into the real panel,
shown in a headless window, and read the controls that it drew. Nothing is substituted in the unit itself. The colors
of the code block in the two themes are checked by eye before a release.

#### Test Environment

The standard test environment for the reader. The panel is tested under the Avalonia headless platform with the Skia
renderer and the application's own `App`, serialized with test parallelization disabled; no display is required.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Headings of six levels are read with their text; seven marks, no space, and four spaces of indent are text.
- Lines of a paragraph are joined with a space, blank lines separate paragraphs, and every kind of line break is
  handled; an empty text has no block and a missing text is refused.
- Bulleted and numbered items keep their markers, a line after an item continues it, and a heading, a fence, or a blank
  line ends it.
- Lines that only look like items are paragraphs.
- Fenced code keeps its lines, language, blank lines, and the text that looks like other blocks; a fence that is not
  closed runs to the end, a longer fence holds a shorter one, and the indent of the fence is taken off.
- Bold, italic, and code runs are read, nested and merged as described, and markers without an end are text.
- Every card and example description of the catalog of the application is read with every letter and digit kept.
- The panel draws a heading in semibold type, a paragraph with its bold, italic, and code runs in those styles, a dot
  for a bullet and the number as written for a numbered item, and a block of code with its lines as one selectable
  block; it draws the new blocks when they change and nothing when they are removed; and a card that is chosen in the
  window has a control for each block of its body.

#### Test Scenarios

**KnowledgeMarkdown_Parse_Headings_AreReadByLevel**: The six levels of heading, a heading with runs, the lines that look
like headings but are not, and a mark alone. This scenario is tested by
`KnowledgeMarkdown_Parse_Headings_AreReadByLevel`.

**KnowledgeMarkdown_Parse_Paragraphs_JoinLinesAndSplitAtBlankLines**: Lines joined with a space, paragraphs split at
blank lines, line feeds, carriage returns, and both, empty and blank text, and a missing text. This scenario is tested
by `KnowledgeMarkdown_Parse_Paragraphs_JoinLinesAndSplitAtBlankLines`.

**KnowledgeMarkdown_Parse_Lists_ReadMarkersAndContinuations**: Three kinds of bullet and two kinds of number, an item
that is continued with and without an indent, a list that starts in a paragraph, an item that a heading ends, and an
indented item. This scenario is tested by `KnowledgeMarkdown_Parse_Lists_ReadMarkersAndContinuations`.

**KnowledgeMarkdown_Parse_LinesThatOnlyLookLikeListItems_AreParagraphs**: A marker alone, a marker with no space, a
number with no dot, and bold or italic text at the start of a line are paragraphs. This scenario is tested by
`KnowledgeMarkdown_Parse_LinesThatOnlyLookLikeListItems_AreParagraphs`.

**KnowledgeMarkdown_Parse_FencedCode_IsKeptVerbatim**: A block with a language and blank lines, without a language, with
text that looks like a heading, an item, and bold, an empty block, a block that is not closed and has more after its
language, a longer fence that holds a shorter one, an indented block in an item, text around a block, and a line with
backticks after the fence. This scenario is tested by `KnowledgeMarkdown_Parse_FencedCode_IsKeptVerbatim`.

**KnowledgeMarkdown_Parse_Spans_ReadBoldItalicAndCode**: Bold, italic, code, a mix, code in bold text, markers in code,
a backtick in code, and markers without an end or next to white space. This scenario is tested by
`KnowledgeMarkdown_Parse_Spans_ReadBoldItalicAndCode`.

**KnowledgeMarkdown_Parse_WholeCard_KeepsOrderAndMergesRuns**: A card with a paragraph, a heading, two numbered items, a
block of code, and a bullet is read in order, and runs that end up in the same style are one run. This scenario is
tested by `KnowledgeMarkdown_Parse_WholeCard_KeepsOrderAndMergesRuns`.

**KnowledgeMarkdown_Parse_RealCatalog_ReadsEveryCardWithoutLosingText**: Every card body and example description of the
catalog of the application is read, with the letters and digits of the source equal to those of the blocks, and with
headings, paragraphs, bulleted and numbered items, blocks of code with a language, bold text, and code text among the
result. This scenario is tested by `KnowledgeMarkdown_Parse_RealCatalog_ReadsEveryCardWithoutLosingText`.

**KnowledgeBrowserView_MarkdownBlocksPanel_DrawsEveryKindOfBlock**: A heading, a paragraph with bold, italic, and code
runs, a bullet, a number, and a block of code are read from a small text and drawn by the panel; five blocks are
drawn, the heading is semibold, the bold and italic runs carry their weight and style, the code run a shade, the
markers are a dot and `3.`, and the code is one block with its lines; the panel follows a change of the blocks and an
empty list. This scenario is tested by `KnowledgeBrowserView_MarkdownBlocksPanel_DrawsEveryKindOfBlock`.

**KnowledgeBrowserView_NodeChosen_ShowsTitleSummaryTagsAndBodyDrawnFromMarkdown**: Choosing a node in the tree of the
window shows the title, summary, tags, and other names, and a body that has a control for each block of the card, with
runs of code text and headings. This scenario is tested by
`KnowledgeBrowserView_NodeChosen_ShowsTitleSummaryTagsAndBodyDrawnFromMarkdown`.
