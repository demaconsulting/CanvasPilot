---
title: Working in a CanvasPilot project
summary: Start here - where files go, what the user sees, and how to read a message with marks.
tags:
  - authoring
  - file-access
aliases:
  - project
children:
  - project-layout
  - deliverables
  - marks
  - preview
  - styles
see-also:
  - svg/composition
  - composition
---

CanvasPilot is the application the user draws in. A **project** is one folder on
the user's computer. It holds a project file named after the project, with the
extension `.canvaspilot`, a hidden data folder named `.canvaspilot` that belongs
to the application, and the drawing files. You help the user by writing and
changing files in the project folder, in the formats CanvasNet draws.

## How to work

- Read the message of the user. It may hold text, marks drawn on a picture, and
  attached files; see [[canvaspilot/marks]] for how to read them.
- Change the files of the project. Write only inside the project folder; see
  [[canvaspilot/project-layout]] for where things go.
- The user sees the selected file drawn next to the chat and redrawn when the
  file changes; see [[canvaspilot/preview]] for what that shows, including errors.
- Keep one finished picture for the user to use; see [[canvaspilot/deliverables]].
- Draw in the style of the project when it has one; see [[canvaspilot/styles]].

## Cards below

Each card below describes one part of the work. The CanvasNet cards describe how
to write each format: start with [[svg]], [[mermaid]], [[plantuml]], [[scad]],
or [[vega-lite]], and with [[composition]] for pictures that combine several
formats.

## What you cannot run

Some CanvasNet cards mention command-line tools, Pandoc, or the options and
classes of a host program (such as `SourceOptions`). You cannot run or call
those: CanvasPilot draws the files itself. Use what the cards say about the
formats, links, and fragments, and use your own tools instead: `canvasnet_help`
for cards and examples, `style_get` and `style_check` for the style of the
project, and the file tools to read and write the project files.
