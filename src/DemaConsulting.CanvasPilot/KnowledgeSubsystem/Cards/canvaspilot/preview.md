---
title: What the user sees of a drawing
summary: How a file is shown, how errors and warnings appear, and what export and copy use.
tags:
  - diagnostics
  - authoring
aliases:
  - viewer
see-also:
  - errors/codes
  - errors/diagnostics
  - canvaspilot/marks
---

- **The picture**: The selected file is drawn by CanvasNet (by default scaled down to fit the pane, never scaled up) and
  drawn again when the file changes. So a file you write is shown to the user as soon as it is saved.
- **Errors**: When a file cannot be drawn, the user sees the CanvasNet error code
  and its hint instead of a picture. Fix the cause of the code; do not work around
  it. See [[errors/codes]] for what each code means.
- **Warnings and diagnostics**: Notes such as `image-access-denied` are listed
  under the picture. A warning means the picture was drawn without something, so
  treat it as a defect to fix; see [[errors/diagnostics]].
- **Export, copy, and drag**: Saving a PNG, copying the picture, and dragging it
  out all use the picture the user sees, so what you draw is exactly what they
  take.
