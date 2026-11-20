# CanvasPilot

[![GitHub forks][badge-forks]][link-forks]
[![GitHub stars][badge-stars]][link-stars]
[![GitHub contributors][badge-contributors]][link-contributors]
[![License][badge-license]][link-license]
[![Build][badge-build]][link-build]
[![Quality Gate][badge-quality]][link-quality]
[![Security][badge-security]][link-security]

## Overview

CanvasPilot is an AI-powered desktop application that helps users create drawings and diagrams with the CanvasNet
library — composing SVG, Mermaid, Vega-Lite, OpenSCAD, and other image sources into standalone images for use in
email, presentations, and other media. It is planned to support conversational interaction by text or speech, using
GitHub Copilot SDK or Ollama as the language model backend.

**Status: Increment 1 - AI chat not yet available.** This version is the application shell: you can manage projects,
browse and preview their image files, export pictures, and store your AI provider and speech preferences. The
assistant that will use those preferences is not part of this version.

![Workflow of CanvasPilot: create or open a project, add image sources, preview them, and export or copy the
picture][workflow]

## Features

- **Project Shell**: A project is a folder with a `{name}.canvaspilot` file and your image source files; create,
  open, save, and close projects, and return to recent projects from the File menu
- **Explorer and Preview**: List the image files of the open project, with the list following changes made by other
  tools, and see the picture of the selected file as drawn by CanvasNet, with notes and errors explained
- **Export and Copy**: Export the picture as a PNG file or copy it to the clipboard for email and presentations
- **Settings**: AI provider preferences (GitHub Copilot or Ollama, model, context window, reasoning level, and an
  opt-in for unrestricted agent tools that is off by default) and speech preferences (audio devices and speech
  models with downloads)
- **Windows Installer**: One per-machine MSI package for 64-bit Windows that includes everything needed to run
- **Local and Private**: Settings and logs are stored on your computer only; there is no telemetry
- **Comprehensive CI/CD**: GitHub Actions workflows with quality checks, builds, and tests on Windows, Linux, and macOS
- **Linting Enforcement**: LintMark, TextLint, and yamllint enforced on every CI run
- **Continuous Compliance**: Compliance evidence generated automatically on every CI run, following
  the [Continuous Compliance][link-continuous-compliance] methodology
- **SonarCloud Integration**: Quality gate and security analysis on every build
- **Documentation Generation**: Automated build notes, user guide, design and verification documents, code quality
  reports, requirements, justifications, and trace matrix
- **Requirements Traceability**: Requirements linked to passing tests with auto-generated trace matrix

## Installation

Download `CanvasPilot-{version}-win-x64.msi` from the
[releases page][link-releases], run it, and start CanvasPilot from the Start Menu. The package is for 64-bit Windows,
installs for all users, and replaces any installed version. See the [User Guide][link-guide] for details.

## Usage

1. Choose **File > New Project...** and name your project, or **File > Open Project...** to open a
   `.canvaspilot` file.
2. Put image source files (SVG, Mermaid, Vega-Lite, OpenSCAD, and others) in the project folder.
3. Select a file in the explorer to see its picture, and read the notes and errors below it.
4. Choose **Image > Export as PNG...** or **Image > Copy Image**.
5. Choose **Tools > Settings...** for the AI provider and speech preferences.

Settings are kept in `%APPDATA%\CanvasPilot\settings.json` and logs in `%LOCALAPPDATA%\CanvasPilot\logs`.

## Documentation

Generated documentation includes:

- **Build Notes**: Release information and changes
- **User Guide**: Comprehensive usage documentation
- **Software Design and Verification Design**: Architecture and how each requirement is verified
- **Code Quality Report**: CodeQL and SonarCloud analysis results
- **Requirements**: Functional and non-functional requirements
- **Requirements Justifications**: Detailed requirement rationale
- **Trace Matrix**: Requirements to test traceability

## Building

```pwsh
pwsh ./build.ps1
```

The build needs the .NET 10 SDK. The MSI installer is built by the Windows job of the CI pipeline from the published
application; see [CONTRIBUTING.md][link-contributing] for the development setup.

## User Guide

The CanvasPilot User Guide is available on the
[CanvasPilot releases page][link-releases].

## Contributing

See [CONTRIBUTING.md][link-contributing] for
guidelines on reporting bugs, suggesting features, and submitting pull requests.

## License

Copyright (c) DEMA Consulting. Licensed under the MIT License. See [LICENSE][link-license] for details.

By contributing to this project, you agree that your contributions will be licensed under the MIT License.

## Support

- [Report a bug or request a feature](https://github.com/demaconsulting/CanvasPilot/issues)
- [Ask a question or start a discussion](https://github.com/demaconsulting/CanvasPilot/discussions)

<!-- Badge References -->
[workflow]: https://raw.githubusercontent.com/demaconsulting/CanvasPilot/main/docs/images/workflow.png
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
[link-guide]: https://github.com/demaconsulting/CanvasPilot/blob/main/docs/user_guide/introduction.md
[link-continuous-compliance]: https://github.com/demaconsulting/ContinuousCompliance
