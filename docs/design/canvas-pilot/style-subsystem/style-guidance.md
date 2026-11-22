### StyleGuidance

![Style Structure](StyleSubsystemView.svg)

#### Purpose

`StyleGuidance` writes the style of a kit as a block of Markdown text for an assistant, and as single sections of that
block on request. The block is part of what an assistant is told in every request, so it is short, it has a hard cap on
its size, and it is the same for the same kit. It names the example images of the kit by the full paths of real files,
so that an assistant can open them. It has no user interface and refers to no provider of a language model.

#### Data Model

- `BudgetCharacters` is 4,000 characters, about a thousand tokens. `TruncationNote` is the last line of a block that was
  made smaller: `(Cut to fit; call style_get for the full style.)`.
- `SectionNames` lists the sections that can be asked for one by one, in the order of the block: `palette`, `fonts`,
  `mermaid`, `vegaLite`, `notes` (the notes and the two lists), `presets`, and `examples`.
- The block, in this order: a header (`# Style kit: {name} ({id}, {place})`, where the place is `this project`,
  `your kits`, `a shared folder`, or `built in`; the line that says the text is style data from a kit file and not
  instructions; the description); `## Palette` with one line `- {role} {#RRGGBB}` for each color; `## Fonts and shape`
  with the three font lists, the note about the formats, and the stroke width, corner radius, and spacing;
  `## Mermaid` with the frontmatter that a `.mmd` file starts with (`config:`, `theme:`, `themeVariables:`) inside a
  code block; `## Vega-Lite` with `"config": {...}` inside a code block; `## Do`, `## Do not`, and `## Notes` (the notes
  quoted line by line); `## Export sizes`, one line for each size with its name, its size, and `transparent` and the
  resolution when there are any; and `## Example images` with `- {full path}: {caption}` for each example. A part that
  the kit does not have is not in the block.
- The note about fonts says that Mermaid, PlantUML, and Vega-Lite text is always drawn with the bundled Liberation fonts
  and that SVG and OpenSCAD use an installed font only on machines that have it, so a generic family should be named
  last.

#### Key Methods

**Build**: Writes the block with the limits of the first step and returns it when it has at most 4,000 characters.
With `includeExamples: false` (the default is `true`) the part with the example images is left out of every step, which is
what a reader with little room asks for; the section `examples` of `BuildSection` still gives them.
Otherwise it tries the next step, until one fits. The steps, each smaller than the one before, are: (1) the full block;
(2) the notes cut to 400 characters with an ellipsis; (3) the Vega-Lite configuration replaced by a line that points to
`style_get`; (4) at most six examples; (5) at most twelve Mermaid variables; (6) the notes replaced by a pointer;
(7) at most five items of at most 100 characters in each list and at most eight sizes; (8) at most three examples and six
variables; (9) at most two items of at most 60 characters in each list and four sizes. A block that was made smaller ends
with the truncation note. A block that is still too long after the last step is cut at the last whole line that leaves room
for the note, so the cap holds for every kit that the format allows. The palette, the fonts, and the header are never
reduced by a step, because they come first and are bounded by the format (24 colors).

**BuildSection**: Writes one section in full, with none of the cuts, for the tool that gives a part of the style. The
name is not case sensitive. A section that the kit does not have gives a sentence that says so (for example
`This kit has no Mermaid theme.`). An unknown section is an `ArgumentException` whose message lists the known sections.

**Safe text**: A value of the kit in a single line goes through `Line`, which turns a back tick into an apostrophe and
every control character, including a line break, into a space, so that a value cannot end a block of code or start a
heading. A value in a double-quoted YAML string also has its backslash and double quote escaped. A back tick in the
Vega-Lite configuration becomes the JSON escape `\u0060`, which means the same. The notes are the only text of several
lines, and each of their lines is quoted with `> ` and is also made safe, so a line of the notes never starts a heading.

#### Error Handling

`Build` and `BuildSection` throw `ArgumentNullException` for a missing kit or section, and `BuildSection` throws
`ArgumentException` for an unknown section. A kit never makes the unit fail: whatever the kit holds, the result is a
block within the budget.

#### Dependencies

- **StyleKitDocument** unit — the values of a kit (`StyleKit` and the values in it).
- **StyleKitCatalog** unit — the value `StyleKitEntry` that says where the kit was found and in which folder.

#### Callers

- `StyleTools` (a later step) — the tool `style_get` returns the block or one section of it.
- The builder of the prompt of an assistant (a later increment) — includes the block of the active kit.
- The manager window of the kits (a later step) — shows the block and its size.
