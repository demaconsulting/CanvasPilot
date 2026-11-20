---
title: Making one picture the user can use
summary: Prefer one composite SVG at the top level that embeds the pieces from subfolders.
tags:
  - composition
  - authoring
aliases:
  - deliverable
  - composite
see-also:
  - svg/composition
  - svg/embedding
  - svg/framed-figure
  - canvaspilot/project-layout
examples:
  - name: composite
    kind: snippet
    language: xml
    file: composite.svg
    description: 'A framed composite that embeds a diagram and a chart from the assets folder (a snippet: it needs those files, so it is not rendered alone).'
---

The user exports the picture they see, copies it, or drags it into an email or a
presentation. So make the thing they see one finished picture.

- **One composite at the top level**: Write a composite SVG file in the root of
  the project. It embeds diagrams, charts, and models with `<image href="..."/>`;
  see [[svg/composition]], [[svg/embedding]], and [[svg/framed-figure]]. Print a
  complete one with [[#composite]].
- **Keep the pieces separate**: Put each piece in its own file in a subfolder, for
  example `assets/flow.mmd` and `assets/sales.vl.json`, so that each stays easy to
  edit and the composite is redrawn when a piece changes.
- **Name it for what it shows**: Use a name such as `order-flow.svg`, not
  `diagram2.svg`.
- **No stray parts at the top level**: A file in the root of the project is listed
  for the user, so leave a file there only when it is something to look at.
