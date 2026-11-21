---
title: Where files go in a project
summary: Which files the user sees, where supporting files go, and the reserved folders.
tags:
  - authoring
  - file-access
aliases:
  - layout
  - folders
see-also:
  - svg/embedding
  - canvaspilot/deliverables
  - canvaspilot/styles
---

- **Files the user sees**: The explorer lists only the image source files in the
  root of the project folder. These are the files CanvasNet draws, for example
  `.svg`, `.mmd`, `.puml`, `.vl.json`, and `.scad` files. Subfolders, the project
  file, the data folder, and hidden files are not listed.
- **Supporting files**: Put data, pictures, models, and shared fragments in
  subfolders such as `assets/` or `data/`, and link them by a relative path, for
  example `<image href="assets/flow.mmd"/>`; see [[svg/embedding]].
- **Attachments**: When the user attaches files to a message, CanvasPilot copies
  them into the `attachments` folder of the project when the message is sent.
  Treat them as inputs to read, not as files to change.
- **Reserved**: The data folder named `.canvaspilot` belongs to the application;
  never read or write anything in it.
- **Style kits**: The folder `styles/` holds the style kits of the project, one
  folder for each kit. It is not listed in the explorer and it is not the
  reserved data folder. Read a kit there to learn the style, and change it only
  when the user asks; see [[canvaspilot/styles]].
- **File access**: CanvasPilot draws with file access limited to the project
  folder. A link to a file outside the folder is not followed: the picture is
  drawn without it and the warning `image-access-denied` is reported. Keep every
  file that a picture needs inside the project.
