<!-- IMPORTANT: All links in this file must be absolute URLs.
     This file is distributed in packages and relative links will not resolve. -->

# ![CanvasPilot: AI-assisted drawing creation with CanvasNet. Type or speak a request; an AI copilot composes SVG, charts, pages, and images into a picture ready to share. Shown with the sample request 'Draw our release flow' and the note 'A Windows desktop app'. A typed request or dictation, dropped-in files, marks on a picture, and many source formats flow into the CanvasPilot AI copilot, and arrows lead out to a standalone picture composed of a flowchart, a chart, and nested pictures, a PNG file, an email, and a presentation slide][hero]

[![GitHub forks][badge-forks]][link-forks]
[![GitHub stars][badge-stars]][link-stars]
[![GitHub contributors][badge-contributors]][link-contributors]
[![License][badge-license]][link-license]
[![Build][badge-build]][link-build]
[![Quality Gate][badge-quality]][link-quality]
[![Security][badge-security]][link-security]

## What is CanvasPilot?

CanvasPilot is a Windows desktop app for creating pictures with an AI copilot. You describe a picture by typing or
speaking, and the copilot builds it with the [CanvasNet][link-canvasnet] library, compositing SVG, Mermaid, PlantUML,
Vega-Lite, OpenSCAD, PDF, PowerPoint, and Visio pages, and raster images (even images nested in images) into a
standalone picture for email, presentations, and other media. There is no Markdown or PDF document pipeline to set up.

The workflow runs from a project folder to a picture you can export, copy, or drag into another program. When the
picture is not right, you mark the problems on it or ask for changes, and go around again.

![Workflow of CanvasPilot in five steps: open a project, describe the picture by typing or speaking and drop in files,
the planned AI copilot drafts it with CanvasNet, a live preview shows the result, and you export or share the PNG; a
return arrow from the preview to the description step is for marking problems or asking for changes][workflow]

## Features

Available now:

- **Projects**: A project is a folder; create, open, save, and close projects, and return to recent ones
- **Project explorer**: Lists the image files of the open project and follows changes made by other tools
- **Live preview**: Draws the selected file with CanvasNet and redraws it when the file changes, with error codes,
  hints, and diagnostics explained below the picture
- **Export and share**: Export the picture as a PNG file, copy the image, or drag the picture into other applications
- **Chat composer**: Write a message by typing or by push-to-talk dictation (hold F8), drag and drop or paste files,
  folders, and images as inline chips, and undo and redo in fine-grained steps
- **Marking problems**: Mark the rendered picture with rectangles, ellipses, freehand strokes, and arrows, numbered
  and with notes; the marks are embedded in the message with crops and the source elements under each mark
- **Drawing knowledge**: Browse CanvasNet's built-in cards and CanvasPilot's own project guide from the Help menu
- **Style kits**: Reusable palettes, fonts, size presets, and example images from the program, your folder, the project,
  or a team folder; export at preset sizes and check a picture against the style
- **Settings**: Choose the AI provider, and the speech devices and models, with model downloads
- **Windows installer**: One MSI package for 64-bit Windows

Planned:

- **AI copilot chat**: GitHub Copilot or Ollama writes, renders, inspects, and fixes drawings in a self-checking loop
- **Safe by default**: The copilot can use only CanvasPilot's own tools, confined to the project folder, with an
  opt-in unrestricted mode
- **Spoken replies**: Text-to-speech for the copilot's answers
- **Variations**: Compare alternative drafts side by side
- **Export profiles and batch export**: Export many pictures at once, in the sizes and formats each use needs
- **Linux and macOS builds**

## Getting started

CanvasPilot needs 64-bit Windows. Nothing else is required to render pictures.

1. Download `CanvasPilot-{version}-win-x64.msi` from the [releases page][link-releases], run it, and start
   CanvasPilot from the Start Menu.
2. Choose an AI provider in **Tools > Settings... > AI Provider**:
   - **GitHub Copilot**: Install the GitHub Copilot CLI (version 1.0.113 or later), run `copilot`, and sign in with
     `/login`. CanvasPilot uses your installed, signed-in CLI.
   - **Ollama**: Enter the endpoint (default `http://localhost:11434`) and the model of your self-hosted Ollama server.
     No GitHub account is needed.
3. Optionally set up speech in **Tools > Settings... > Speech**: pick your microphone and speakers, and download the
   speech models.
4. Choose **File > New Project...**, then start describing a picture.

## Learn more

- [User Guide][link-guide]: Installation, projects, the chat composer, marking pictures, and settings
- [CanvasNet][link-canvasnet]: The library that draws the pictures
- [Speech][link-speech]: The library behind dictation and spoken replies
- [Releases][link-releases]: Installer packages and release notes
- [Contributing][link-contributing]: Reporting bugs, suggesting features, and submitting pull requests
- [Continuous Compliance][link-continuous-compliance]: The methodology this project follows

Copyright (c) DEMA Consulting. Licensed under the MIT License. See [LICENSE][link-license] for details.

<!-- Image References -->
[hero]: https://raw.githubusercontent.com/demaconsulting/CanvasPilot/main/docs/images/hero.png
[workflow]: https://raw.githubusercontent.com/demaconsulting/CanvasPilot/main/docs/images/workflow.png

<!-- Badge References -->
[badge-forks]: https://img.shields.io/github/forks/demaconsulting/CanvasPilot?style=plastic
[badge-stars]: https://img.shields.io/github/stars/demaconsulting/CanvasPilot?style=plastic
[badge-contributors]: https://img.shields.io/github/contributors/demaconsulting/CanvasPilot?style=plastic
[badge-license]: https://img.shields.io/github/license/demaconsulting/CanvasPilot?style=plastic
[badge-build]: https://img.shields.io/github/actions/workflow/status/demaconsulting/CanvasPilot/build_on_push.yaml?style=plastic
[badge-quality]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_CanvasPilot&metric=alert_status
[badge-security]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_CanvasPilot&metric=security_rating

<!-- Link References -->
[link-forks]: https://github.com/demaconsulting/CanvasPilot/network/members
[link-stars]: https://github.com/demaconsulting/CanvasPilot/stargazers
[link-contributors]: https://github.com/demaconsulting/CanvasPilot/graphs/contributors
[link-license]: https://github.com/demaconsulting/CanvasPilot/blob/main/LICENSE
[link-build]: https://github.com/demaconsulting/CanvasPilot/actions/workflows/build_on_push.yaml
[link-quality]: https://sonarcloud.io/dashboard?id=demaconsulting_CanvasPilot
[link-security]: https://sonarcloud.io/dashboard?id=demaconsulting_CanvasPilot
[link-releases]: https://github.com/demaconsulting/CanvasPilot/releases
[link-contributing]: https://github.com/demaconsulting/CanvasPilot/blob/main/CONTRIBUTING.md
[link-guide]: https://github.com/demaconsulting/CanvasPilot/tree/main/docs/user_guide
[link-canvasnet]: https://github.com/demaconsulting/CanvasNet
[link-speech]: https://github.com/demaconsulting/Speech
[link-continuous-compliance]: https://github.com/demaconsulting/ContinuousCompliance
