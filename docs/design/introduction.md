# Introduction

This document provides the detailed design for CanvasPilot, an AI-assisted drawing application built on CanvasNet. The
current build is an Avalonia desktop application that contains the AppShell, Logging, Preview, Project, Settings,
Speech, and Utilities subsystems; further subsystems are added as they are introduced. It covers local software items
(systems, subsystems, and units) and the OTS software items they consume.

## Purpose

The purpose of this document is to describe the internal design of each software unit that
comprises the CanvasPilot. It captures data models, algorithms, key methods, and
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

- **BuildMark**: integration and usage design.
- **CanvasNet**: integration and usage design (runtime library).
- **FileAssert**: integration and usage design.
- **Pandoc**: integration and usage design.
- **ReqStream**: integration and usage design.
- **ReviewMark**: integration and usage design.
- **SarifMark**: integration and usage design.
- **SonarMark**: integration and usage design.
- **Speech**: integration and usage design (runtime library).
- **SysML2Tools**: integration and usage design.
- **VersionMark**: integration and usage design.
- **WeasyPrint**: integration and usage design.
- **xUnit**: integration and usage design.

The following topics are out of scope:

- Design documents are not produced for the test projects or build pipeline CI configuration
- The internal design of OTS software items is excluded; only integration and usage design is documented

## Software Structure

The software structure is modeled in SysML2 under `docs/sysml2/` and rendered to the diagram
below by SysML2Tools as part of the build pipeline. AI agents should query the SysML2 model
directly (see the `sysml2tools-query` skill) rather than parsing this diagram or the prose
elsewhere in this document. The model captures the shipped system's runtime composition, including
CanvasNet and Speech, the runtime OTS items modeled so far (the dependencies of the units that use them point to
them); the other OTS items
listed in Scope above are documented separately (see `docs/design/ots.md`) and are formalized in the model with
their own documents.

![Software Structure](SoftwareStructureView.svg)

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
  - **DemaConsulting.CanvasPilot.Desktop/** - thin desktop head that starts the Avalonia application

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
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)

OTS items have integration/usage design documentation parallel to system folders:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml`
- Design: `docs/design/ots/{ots-name}.md`
- Verification: `docs/verification/ots/{ots-name}.md`

Review-sets: defined in `.reviewmark.yaml`

## References

- CanvasPilot User Guide
- CanvasPilot Repository (<https://github.com/demaconsulting/CanvasPilot>)
