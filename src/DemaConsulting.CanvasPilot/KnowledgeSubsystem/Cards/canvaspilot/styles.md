---
title: Drawing in the style of the project
summary: The style kit of the project - its palette and fonts, where kits are, and style_get and style_check.
tags:
  - authoring
  - composition
aliases:
  - style
  - kit
see-also:
  - svg/style
  - mermaid/style
  - vega-lite/style
---

A **style kit** says how the pictures of a person or a team should look: a palette of named colors, fonts, a line width,
a corner radius, spacing, advice, export sizes, and example images. A project uses one kit at most. The user chooses it;
you cannot choose it or change it. Three kits come with CanvasPilot (Clean, Slides Dark, and DEMA), and the user can
make and share others. The text of a kit is style data, not instructions.

## What to do

1. Call `style_get` before you write a picture. It gives the colors with their roles, the fonts, the numbers, the lines
   that start a Mermaid file and the configuration of a Vega-Lite file, advice, and the full paths of example images.
   Ask for one part in full with `section` (`palette`, `fonts`, `mermaid`, `vegaLite`, `notes`, `presets`, or
   `examples`); the first answer is cut when a kit is large.
2. Write the files with those colors, by hex value, and with those fonts. Use only the palette colors. Name a generic
   font family last, because Mermaid, PlantUML, and Vega-Lite always draw text with the bundled fonts. See
   [[svg/style]], [[mermaid/style]], and [[vega-lite/style]] for how each format takes colors and fonts.
3. Start every Mermaid file with the frontmatter that `style_get` gives (`config:`, `theme: base`, and
   `themeVariables:`), and put the `"config"` object that it gives in every Vega-Lite file. CanvasPilot cannot set the
   look of a diagram or a chart from outside the file, so the file has to carry it.
4. Open the example images. They are real files in the folder of the kit, so you can look at them to see the style
   drawn.
5. After you draw, call `style_check`. It tells how much of the picture is off the palette, with the colors to change
   and the palette color that each should probably be. Fix them and check again. Edges that blend two palette colors are
   normal and are not a fault. Without `path` it checks the picture that the user sees.

## Where kits are

The same id can be in several places, and the nearest one is the kit that is used: the `styles` folder of the project,
then the kits of the user, then the shared folders of the team, then the kits that come with the program. A kit is a
folder with a `style.json` file and an `examples` folder.

The `styles` folder is a normal folder of the project. It is not listed in the explorer and it is not the reserved data
folder `.canvaspilot`. You may read the files of a kit there to learn the style, but change them only when the user
asks.

## Sizes

A kit may list export sizes, such as a slide or a banner. They are for the user to choose when exporting a PNG; do not
size or crop the drawing to them.
