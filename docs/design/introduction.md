# Introduction

This document provides the detailed design for CanvasPilot, an AI-assisted drawing application built on CanvasNet.
CanvasPilot is an Avalonia desktop application for Windows that is delivered as an MSI installer package. Increment 1,
described here, is the application shell: folder-based projects, an explorer of the project's image source files, a
preview of the selected file that is drawn by CanvasNet, PNG export and copy to the clipboard, persisted settings with
AI provider and speech choices, and the installer. The AI chat that will use the stored choices is not part of this
increment. The document covers local software items (systems, subsystems, and units) and the OTS software items they
consume.

## Purpose

The purpose of this document is to describe the internal design of each software unit that
comprises CanvasPilot. It captures data models, algorithms, key methods, and
inter-unit interactions at a level of detail sufficient for formal code review, compliance
verification, and future maintenance. A reviewer should be able to understand how each item
satisfies its requirements without reading source code. The document does not restate
requirements; it explains how they are realized.

## Scope

Local items:

- **CanvasPilot**: system, subsystem, and unit design for all local components:
  - **AppShell** subsystem: **MainWindowShell**, **ProjectWorkspace**, and **AboutDialog** units.
  - **Logging** subsystem: **RollingFileLogger** unit.
  - **Preview** subsystem: **ImageRenderService**, **PreviewPane**, and **ImageExporter** units.
  - **Project** subsystem: **ProjectService**, **RecentProjects**, **NewProjectDialog**, and
    **ProjectExplorer** units.
  - **Settings** subsystem: **SettingsService**, **SettingsDialog**, and **AiProviderSettingsPage** units.
  - **Speech** subsystem: **AudioDeviceService**, **SpeechModelService**, **DeviceSelection**,
    **ModelCatalogPanel**, **ModelParameterEditor**, and **SpeechSettingsPage** units.
  - **Utilities** subsystem: **PathHelpers** unit.

OTS items:

- **Avalonia**: integration and usage design (runtime user interface framework).
- **CommunityToolkit.Mvvm**: integration and usage design (runtime view model source generators).
- **CanvasNet**: integration and usage design (runtime library that draws image source files).
- **Speech**: integration and usage design (runtime library for audio devices and speech models).
- **WiX Toolset**: integration and usage design (builds the Windows installer package).
- **xUnit**: integration and usage design (test framework).
- **BuildMark**, **FileAssert**, **Pandoc**, **ReqStream**, **ReviewMark**, **SarifMark**, **SonarMark**,
  **SysML2Tools**, **VersionMark**, and **WeasyPrint**: integration and usage design of the build, documentation, and
  analysis tools of the pipeline.

The following topics are out of scope:

- Design documents are not produced for the test projects, the executable project that starts the application, the
  installer project (which is covered by the WiX Toolset OTS design), or the build pipeline CI configuration
- The AI chat, the agent, and the speech engines are not part of this increment; the settings that they will read are
  designed (see the *AI Provider Seam* section of the system design)
- The internal design of OTS software items is excluded; only integration and usage design is documented

## Software Structure

The software structure is modeled in SysML2 under `docs/sysml2/` and rendered to the diagram
below by SysML2Tools as part of the build pipeline. AI agents should query the SysML2 model
directly (see the `sysml2tools-query` skill) rather than parsing this diagram or the prose
elsewhere in this document. The diagram shows the system, its seven subsystems with their units, and the runtime OTS
items that the system consumes; the dependencies between the units are in the model and are shown in the view of each
subsystem. The build and documentation tools listed in Scope above are documented separately (see
`docs/design/ots.md`).

![The CanvasPilot system with its seven subsystems and their units, and the runtime third-party items it
uses](SoftwareStructureView.svg)

## Architecture

The next diagram shows how the parts fit together at run time. The small executable project only starts the Avalonia
application. The application library holds the subsystems: the AppShell subsystem builds every unit in one place (the
composition root) and hosts the others; Preview follows the file selected in the Project explorer, and Speech adds a
page to the Settings dialog, as the arrows between neighbors show; every subsystem logs through Logging, and the
subsystems that build file locations use Utilities. The third-party items are used inside the library, and the
application reads and writes only the local data in the bottom row. Section *Architecture* of the system design
explains the dependencies between the subsystems, including the one between AppShell and Preview.

![Architecture of CanvasPilot: the desktop head starts the application library, whose AppShell subsystem builds and
hosts the Project, Preview, Settings, and Speech subsystems above Logging and Utilities, with the third-party items
and the local data the application uses](architecture.svg)

## Folder Layout

- **src/** - source files and projects
  - **DemaConsulting.CanvasPilot/** - main application library source
    - **AppShellSubsystem/** - main window, project workspace, About dialog, composition root, and view locator
    - **LoggingSubsystem/** - local rolling log file subsystem
    - **PreviewSubsystem/** - image rendering with CanvasNet, preview pane, and PNG and clipboard export
    - **ProjectSubsystem/** - projects, recent projects, New Project dialog, and project explorer
    - **SettingsSubsystem/** - persisted settings, settings dialog, and settings pages
    - **SpeechSubsystem/** - audio devices, speech models and their downloads, and the Speech settings page
    - **UtilitiesSubsystem/** - shared utilities subsystem
  - **DemaConsulting.CanvasPilot.Desktop/** - thin executable project (`CanvasPilot.exe`) that starts the Avalonia
    application
  - **DemaConsulting.CanvasPilot.Installer/** - WiX project that builds the MSI package; it is not part of the solution
- **test/** - test projects (see *Test Strategy*)
  - **DemaConsulting.CanvasPilot.Tests/** - unit, subsystem, and system tests
  - **DemaConsulting.CanvasPilot.UiTests/** - headless user interface tests
  - **OtsSoftwareTests/** - tests of the runtime OTS libraries
- **docs/** - requirements, design, verification, SysML2 model, user guide, images, and the definitions of the
  generated documents
- **.reviewmark.yaml**, **requirements.yaml**, **.fileassert.yaml** - the review, requirements, and document assertion
  configuration

## Test Strategy

Verification is layered, and each layer is run by `build.ps1` and by the build pipeline:

- **Unit tests** (`test/DemaConsulting.CanvasPilot.Tests/{Subsystem}/{Unit}Tests.cs`) test one unit at a time with
  hand-written or substitute collaborators, including boundary and error cases.
- **Subsystem tests** (`{Subsystem}Tests.cs` in the same folders) test the units of one subsystem together on
  temporary folders.
- **System tests** (`CanvasPilotTests.cs`) compose the real units as the application does, without a window, and test
  the system requirements, including startup on each platform.
- **User interface tests** (`test/DemaConsulting.CanvasPilot.UiTests`) show the real views and view models in the
  Avalonia headless platform, so that menus, dialogs, and bindings are tested without a display.
- **OTS tests** (`test/OtsSoftwareTests`) call the runtime OTS libraries (Avalonia, CommunityToolkit.Mvvm, CanvasNet,
  and Speech) the way the application does, and are the qualification evidence for libraries that have no
  self-validation mode.
- **Document and package assertions** (FileAssert, `.fileassert.yaml`) check the generated documents, the rendered
  diagrams, and the MSI package.

End-to-end tests that drive the installed application through its user interface (with Appium) are deferred to a later
increment. The behavior of the installed package, the operating system file pickers and clipboard, and real model
downloads are checked by hand before each release; the verification design says which checks these are.

## Document Conventions

Throughout this document:

- Class names, method names, property names, and file names appear in `monospace` font.
- The word **shall** denotes a design constraint that the implementation must satisfy.
- Section headings within each unit chapter follow a consistent structure: overview, data model,
  methods/algorithms, and interactions with other units.
- Text tables are used in preference to diagrams, which may not render in all PDF viewers.

## Companion Artifact Structure

Local software items have corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- SysML2 model: `docs/sysml2/model/{system}/.../{item}.sysml` (kebab-case)
- Source code: `src/{System}/{Subsystem}Subsystem/{Item}.cs` (PascalCase for C#; views also have `.axaml` files)
- Tests: `test/{System}.Tests/{Subsystem}Subsystem/{Item}Tests.cs` (PascalCase for C#); user interface tests of the
  views are in `test/{System}.UiTests/`

Subsystem folders carry the suffix `Subsystem` in source and tests, while the documents, requirements, and review-set
identifiers omit the word (for example `app-shell-subsystem.md` and `CanvasPilot-AppShell`).

OTS items have integration/usage design documentation parallel to system folders:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml`
- Design: `docs/design/ots/{ots-name}.md`
- Verification: `docs/verification/ots/{ots-name}.md`
- Tests of runtime libraries: `test/OtsSoftwareTests/{OtsName}Tests.cs`

Review-sets: defined in `.reviewmark.yaml`

## References

- CanvasPilot User Guide
- CanvasPilot Repository (<https://github.com/demaconsulting/CanvasPilot>)
