# Introduction

This document provides the verification design for CanvasPilot, an AI-assisted drawing application built on CanvasNet.
The current build is an Avalonia desktop application for Windows, delivered as an MSI package, that contains the
AppShell, Annotation, Chat, Composer, Logging, Payload, Preview, Project, Settings, Speech, and Utilities subsystems;
the AI agent that will answer the messages is not part of this version and is added as a further subsystem when it is
introduced.

## Purpose

The purpose of this document is to describe how each requirement for the CanvasPilot is
verified. For every software item — system, subsystem, and unit — this document names the
verification approach, identifies the test scenarios (including boundary conditions and error
paths), describes what is mocked or stubbed, and maps each requirement to at least one named
test scenario. The document does not restate design; it explains how the design is proven correct.

## Scope

This document covers the verification design for the following software items:

**Local items:**

- **CanvasPilot** — system, subsystem, and unit verification:
  - **Annotation** subsystem
    - **MarkRegistry** — marks on the shown picture, pinned to their picture, numbered, with geometry in picture pixels
    - **MarkSourceResolver** — the parts of the source file under a mark
    - **AnnotationOverlay** — the layer on which the user draws numbered marks with an optional note, and its tool bar
  - **AppShell** subsystem
    - **MainWindowShell** — main window, menus, and dialog service
    - **ProjectWorkspace** — explorer and preview content area
    - **AboutDialog** — application identity window
  - **Chat** subsystem
    - **ChatPanel** — the transcript of the sent messages, the composer with its send and hold-to-talk buttons, and the
      joining of marks to the message
    - **PayloadInspector** — the window that shows exactly what the assistant would receive for a sent message
  - **Composer** subsystem
    - **ComposerDocument** — the message being composed as ordered segments of text and chips
    - **ComposerHistory** — undo and redo of the user's changes, one action at a time
    - **AttachmentIntake** — dropped and pasted content as chips and text, treated as data
    - **AttachmentStore** — pasted pictures in the draft folder and attachments copied into the project at send
    - **ComposerViewModel** — the message without a window: one undo step per action, dictation, drop and paste, and
      send
    - **ComposerEditor** — the editing view of the message: text, dictated text, and chips inline, with undo and redo
      keys, paste, and drop
  - **Logging** subsystem
    - **RollingFileLogger** — local rolling log file writer
  - **Payload** subsystem
    - **MessagePayload** — the immutable values of the message that is sent
    - **MarkImageComposer** — the picture with the marks drawn and numbered, and the area around each mark
    - **PayloadBuilder** — the payload from the message, the marks, and the attachments
    - **PayloadDescriber** — the payload as text
  - **Preview** subsystem
    - **ImageRenderService** — background drawing of image source files with CanvasNet and PNG encoding
    - **PreviewPane** — preview of the selected file with busy, error, and note display
    - **ImageExporter** — PNG file and clipboard export of the shown picture
    - **ImageDragSource** — the shown picture as a PNG file, ready to be dragged into another program
  - **Project** subsystem
    - **ProjectService** — project folders and versioned project files
    - **RecentProjects** — most-recently-used project list
    - **NewProjectDialog** — window to choose a project name and location
    - **ProjectExplorer** — list of the project's image source files
  - **Settings** subsystem
    - **SettingsService** — versioned per-user settings file
    - **SettingsDialog** — modal settings window hosting settings pages
    - **AiProviderSettingsPage** — AI provider settings page
  - **Speech** subsystem
    - **AudioDeviceService** — audio device listing and capture device creation through the Speech library
    - **SpeechModelService** — speech model listing, downloading, and engine loading through the Speech library
    - **DictationService** — dictation: speech held to talk into text, with the native runtime checked first
    - **DeviceSelection** — microphone and speaker pickers
    - **ModelCatalogPanel** — speech model lists with downloads
    - **ModelParameterEditor** — editors for the tunable settings of a model
    - **SpeechSettingsPage** — speech settings page
  - **Utilities** subsystem
    - **PathHelpers** — safe path combination utilities

**OTS items:**

- **Avalonia** — user interface framework
- **AvaloniaEdit** — text editing control of the message composer
- **BuildMark** — build-notes documentation tool
- **CanvasNet** — image source rendering library
- **CommunityToolkit.Mvvm** — view model source generators
- **FileAssert** — document assertion tool
- **Pandoc** — Markdown-to-HTML conversion tool
- **ReqStream** — requirements traceability tool
- **ReviewMark** — file review enforcement tool
- **SarifMark** — SARIF report conversion tool
- **SonarMark** — SonarCloud quality report tool
- **Speech** — audio device, speech model, and speech recognition library, with its native runtime
- **SysML2Tools** — architecture model validation and diagram rendering tool
- **VersionMark** — tool-version documentation tool
- **WeasyPrint** — HTML-to-PDF conversion tool
- **WiX Toolset** — Windows installer package build tool
- **xUnit** — unit-testing framework

The following topics are out of scope:

- Verification documents are not produced for the test projects themselves — they are the
  means of verification, not subjects of it
- Build pipeline CI configuration is excluded
- The AI agent is not part of this version, and so is not verified; the messages are composed and sent without an
  agent, and only the stored settings that the agent will read, and the payload that it will receive, are verified
- Appium end-to-end tests of the installed application are deferred to a later increment
- The internal implementation of OTS software items is excluded; only integration and usage
  are verified

## Folder Layout

The test folder structure mirrors the source subsystem breakdown:

```text
test/
├── DemaConsulting.CanvasPilot.Tests/  — unit, subsystem, and system tests
│   ├── AnnotationSubsystem/
│   ├── AppShellSubsystem/
│   ├── ChatSubsystem/
│   ├── ComposerSubsystem/
│   ├── LoggingSubsystem/
│   ├── PayloadSubsystem/
│   ├── PreviewSubsystem/
│   ├── ProjectSubsystem/
│   ├── SettingsSubsystem/
│   ├── SpeechSubsystem/
│   ├── UtilitiesSubsystem/
│   └── CanvasPilotTests.cs            — system tests
├── DemaConsulting.CanvasPilot.UiTests/ — headless user interface tests of the views
└── OtsSoftwareTests/                  — tests of the runtime OTS libraries
```

Headless user-interface tests for the views live in `test/DemaConsulting.CanvasPilot.UiTests/` and are referenced by the
unit verification documents of the items that own the views. The tests of the runtime OTS libraries (Avalonia,
AvaloniaEdit, CommunityToolkit.Mvvm, CanvasNet, and Speech) live in `test/OtsSoftwareTests/` and are referenced by the
OTS verification documents.

## Verification Layers

Each requirement is verified at the level of the item that owns it:

- **Unit tests** exercise one unit with hand-written or substitute collaborators, including boundary and error cases.
- **Subsystem tests** exercise the units of one subsystem together on temporary folders.
- **System tests** (`CanvasPilotTests.cs`) compose the real units without a window, and the platform requirements are
  proven by running the startup test on each supported platform and runtime.
- **User interface tests** run the real views in the Avalonia headless platform.
- **OTS tests and assertions** qualify the OTS items: the OTS tests of the runtime libraries, FileAssert assertions for
  generated documents, diagrams, the MSI package, and the native speech runtime in the published application, and the
  self-validation of the tools that have it.

Checks that cannot be automated are described in the verification document of the item they concern and are performed
before each release: the installed package (install, upgrade, shortcut, closing a running instance, and the native
speech files in the installed folder), the operating system file pickers, clipboard, and drag and drop (including
dragging the shown picture into other programs and pasting a screenshot), input method composition in the message
editor, a real speech model download, and dictating with a real microphone and a real model. The checklist is in the
system verification document. They are not evidence for a requirement.

## Companion Artifact Structure

In-house items have corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)

OTS items have parallel artifacts in:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml` (kebab-case)
- Verification: `docs/verification/ots/{ots-name}.md` (kebab-case)

Review-sets: defined in `.reviewmark.yaml`

## References

- CanvasPilot Software Design Document
- CanvasPilot releases (<https://github.com/demaconsulting/CanvasPilot/releases>)
