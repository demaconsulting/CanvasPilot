### StyleKitDocument

![Style Structure](StyleSubsystemView.svg)

#### Purpose

`StyleKitDocument` holds the values of a style kit and the format of the file that a kit is kept in. It exists so that
the rest of the subsystem, and every place that shows or edits a kit, share one definition of what a kit is, one set of
rules for what a valid kit is, and one way to write it.

#### Data Model

- `StyleKit`: `Id`, `Name`, `Description`, `Palette` (role or name to `#RRGGBB`, in file order), `Fonts`, `StrokeWidth`,
  `CornerRadius`, `Spacing`, `Mermaid` (optional), `VegaLiteConfigJson` (optional, compact JSON text of the `config`
  object), `Guidance`, `SizePresets`, and `Examples`.
- `StyleFonts` (three CSS `font-family` lists: heading, body, and monospace), `StyleMermaid` (a theme and theme
  variables), `StyleGuidanceText` (notes, things to do, things to avoid), `StyleSizePreset` (name, width and height of
  which one may be missing, resolution, and transparency), and `StyleExample` (relative path, full path, and caption).
- `StyleIssue` (the path of the property, a message, and whether it is an error) and `StyleKitParseResult` (the kit,
  which is absent when there is an error, and every issue).
- `StyleKitLocation`: `Project`, `User`, `Shared`, and `BuiltIn`, in the order of precedence, highest first.
- The format, version 1, has these top-level properties in this order: `formatVersion`, `id`, `name`, `description`,
  `palette`, `fonts`, `stroke` (with `width`), `cornerRadius`, `spacing`, `mermaid` (with `theme` and `themeVariables`),
  `vegaLite` (with `config`), `guidance` (with `notes`, `do`, and `dont`), `sizePresets`, and `examples`. Only
  `formatVersion`, `id`, `name`, and `palette` are required; the others default to a plain kit (fonts `sans-serif` and
  `monospace`, stroke width 1, corner radius 0, spacing 8, no Mermaid or Vega-Lite part, no advice, no sizes).
- The palette has the required roles `background`, `text`, and `primary`, the usual roles `secondary`, `accent`,
  `surface`, and `muted`, and any other named colors, 24 entries at most. A color is `#RRGGBB` only, so a color with an
  alpha channel, a short form, and a color name are refused, and it is held in upper case.
- The `examples` property holds captions only, keyed by `examples/{file name}`. The examples of a kit are the files of
  its `examples` folder.

#### Key Methods

**Parse(json, kitFolder)**: reads the text of a style file. It refuses a text over 256 KiB before it is parsed, parses
it with a depth limit of 16, and checks `formatVersion` first, because a file of another format may mean something else:
a missing or non-numeric version, a version below 1, and a version above 1 (made by a newer version) are each the only
error. It then reads each property, with the plain default for what is left out and an error for what has the wrong
shape, warns about each top-level property that it does not know, and applies the rules of the values to the kit that it
read. A message about a value that could not be read at all is not repeated by the rules. With a folder, the files of
its `examples` folder become the examples and a caption for a missing file is a warning; with an empty folder each
caption is an example without a full path.

**ReadFolder(kitFolder)**: checks that the folder and the file exist and that the file is not over the limit before it
is read, reads it, and calls `Parse`. A folder whose name differs from the id of the kit is a warning, and the id of the
file is used.

**Validate(kit)**: applies the rules of the values to a kit that is built in memory, with the same messages that `Parse`
gives: the pattern of the id; the length of every text; the palette (number of entries, the pattern of a name, the form
of a color, the required roles); the font lists (a family list that does not parse is an error; a first family that is
not a generic keyword, a common alias, or a bundled Liberation family is a warning); the ranges of the stroke width (0
to 32), the corner radius (0 to 64), and the spacing (0 to 128); the Mermaid theme (one of `default`, `base`, `dark`,
`forest`, `neutral`) and the number, names, and values of its variables; the Vega-Lite configuration (a JSON object of
at most 8 KiB as compact JSON); at most 10 entries of 1 to 160 characters in each list of advice; the size presets (at
most 16, unique names ignoring case, sizes of 1 to 8192 pixels, at least one of width and height, a resolution of 24 to
1200); and the examples (at most 24, and a path that is one file directly in the `examples` folder).

**Write(kit)**: writes the canonical text: the property order above, two-space indentation, line feeds, a final line
feed, colors in upper case, and the parts that a kit may leave out left out. Unknown properties and captions for missing
files are not written. The result of `Write` of a kit that was read from a canonical file is that file.

**StyleColors**: `TryParse` and `TryNormalize` read the one accepted color form; `Contrast` is the WCAG contrast ratio
of two colors, which the editor shows as a hint and the tests of the kits that ship with the program use as a check.

#### Error Handling

Reading never throws for a bad file or folder: a missing folder, a missing or oversize file, a file that cannot be read,
invalid JSON, a text nested too deep, and each wrong value are issues in the result. A missing argument is an
`ArgumentNullException`, and `StyleColors.Normalize` and `Contrast` throw a `FormatException` for text that is not a
color. The unit logs nothing; the units that read kits from the places on disk log the issues.

**Limits.** The limits are the constants of `StyleKitSerializer`: the file (256 KiB), the depth (16), the palette (24),
the theme variables (40) and their values (60 characters), the size presets (16), the examples (24, each at most 16
MiB), the entries of each list of advice (10), and the Vega-Lite configuration (8 KiB). Example files beyond the limits
are ignored with a warning and not refused, so that a folder with a stray large file still gives a usable kit. An
example file that is a link, or that is hidden, is ignored.

**Example paths.** The path of an example is used to build a file name below the folder of the kit, so a path that is
rooted, has a drive, uses a backslash, has a `.` or `..` segment, goes into a sub-folder, or has a character that a file
name cannot have is an error, both as a caption and as an example in memory. The characters that a file name cannot have
are the same on every platform, whatever the platform that reads the kit would accept: the control characters, the
delete character, the slashes, and `: * ? " < > |`. A kit is shared between computers, so a name such as `a|b.png` that
Linux would accept is refused there too, instead of being written on one computer and failing on another.

#### Dependencies

- **CanvasNet** (OTS): `GenericFontFamilies.SplitFamilyList` and `GenericFontFamilies.TryClassify` classify the first
  family of a font list.
- The base class library only: `System.Text.Json` for reading and writing, and file and directory access for the folder.

#### Callers

- The catalog of kits, the archive of kits, the guidance writer, the checker of pictures, the tools for an assistant,
  and the editor of kits of later steps.
