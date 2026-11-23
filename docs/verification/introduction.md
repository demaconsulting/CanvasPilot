# Introduction

This document provides the verification design for CanvasPilot, an AI-assisted drawing application built on CanvasNet.
The current build is an Avalonia desktop application for Windows, delivered as an MSI package, that contains the
Agent, Annotation, AppShell, Chat, Composer, Knowledge, Logging, Payload, Preview, Project, Settings, Speech, Style, and
Utilities subsystems; the AI copilot of the Agent subsystem answers the messages through Ollama or through the GitHub
Copilot CLI that the user installed.

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
  - **Agent** subsystem
    - **AgentContract** — the provider-independent contract of the AI copilot
    - **CopilotCliLocator** — finds the Copilot CLI that the user installed, tells it from editor shims, and checks its
      version
    - **CopilotRuntimeStarter** — starts the Copilot runtime on the installed CLI, shares that one runtime, and turns
      every failure to start into a status and a sentence
    - **AgentReadiness** — checks whether the chosen AI provider can be used (the Copilot CLI, credentials, and the
      runtime that starts; the Ollama server, model, and context window) and says what to do
    - **AgentToolRegistry** — builds the tools of the copilot for one project, confined to the project folder, from the
      drawing knowledge and style tool descriptions, the project packs, and the optional pack catalog
    - **ProjectTools** — the tools with which the copilot lists, reads, writes, and draws the files of the project:
      confined to the project folder, links refused, files replaced directly, pictures kept in memory
    - **AgentInstructions** — the instructions of the copilot: the role prompt, the knowledge index, the facts of the
      project, and the style, in a full and a compact tier, with measured sizes
    - **PayloadMessageMapper** — the message of the user as a model message: the words, the described attachments,
      pictures, and marks, and the pictures and close-ups as images in a stated order
    - **AgentEventMapper** — the updates of an agent as the provider-independent events of a turn, and the failures of a
      turn as sentences that say what to do
    - **AgentProviderFactory** — the agent of a conversation for Ollama or for GitHub Copilot, on the one runtime that
      the starter of the Copilot runtime shares, and what it owns and disposes
    - **AgentService** — the conversation of the open project: made when the first message is sent, one turn at a time,
      the data of the Copilot runtime kept outside the project, and a log of metadata only
  - **Annotation** subsystem
    - **MarkRegistry** — marks on the shown picture, pinned to their picture, numbered, with geometry in picture pixels
    - **MarkSourceResolver** — the parts of the source file under a mark
    - **AnnotationOverlay** — the layer on which the user draws numbered marks with an optional note, and its tool bar
  - **AppShell** subsystem
    - **MainWindowShell** — main window, title bar, menus, window layout, and dialog service
    - **ProjectWorkspace** — explorer, stage, and chat column with their splitters and layout
    - **StatusBar** — the bar at the bottom that reports the shown picture
    - **ProviderPill** — the pill in the title bar that tells which AI provider answers and whether it can be used
    - **Welcome** — the start screen with the recent project cards
    - **AboutDialog** — application identity window
    - **ThemeResources** — design tokens, icons, control themes, and palettes of the dark and light themes
  - **Chat** subsystem
    - **ChatPanel** — the transcript of the sent messages, the composer with its send and hold-to-talk buttons, and the
      joining of marks to the message
    - **ChatTurnRunner** — one turn of the conversation: the events of the AI copilot as rows of the transcript, stop,
      and failure
    - **ChatHistoryStore** — the words of the conversation of each project kept on this computer outside the project,
      restored when the project is opened and cleared on request
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
  - **Knowledge** subsystem
    - **KnowledgeCatalog** — the tree of cards from the help of CanvasNet and the cards of CanvasPilot
    - **KnowledgeSearch** — the cards that fit some words, best first
    - **KnowledgePromptBuilder** — the cards as text for the prompt of an assistant, within a size budget
    - **KnowledgeTools** — the tool description and function by which an assistant gets the cards
    - **KnowledgeSkills** — each card as a skill for an assistant
    - **KnowledgeMarkdown** — the Markdown of a card as blocks that a window draws, with no text lost
    - **KnowledgeBrowser** — the window in which the user reads, searches, and follows the cards and copies examples
  - **Logging** subsystem
    - **RollingFileLogger** — local rolling log file writer
  - **Payload** subsystem
    - **MessagePayload** — the immutable values of the message that is sent
    - **MarkImageComposer** — the picture with the marks drawn and numbered, and the area around each mark
    - **PayloadBuilder** — the payload from the message, the marks, and the attachments
    - **PayloadDescriber** — the payload as text
  - **Preview** subsystem
    - **ImageRenderService** — background drawing of image source files with CanvasNet and PNG encoding
    - **PreviewPane** — preview of the selected file on a stage with zoom, and busy, error, and note display
    - **ThumbnailProvider** — the small pictures of the explorer rows
    - **ImageExporter** — PNG file and clipboard export of the shown picture
    - **ImageDragSource** — the shown picture as a PNG file, ready to be dragged into another program
    - **ExportPresetDialog** — the dialog in which the user chooses the size to export at
  - **Project** subsystem
    - **ProjectService** — project folders and versioned project files
    - **RecentProjects** — most-recently-used project list
    - **NewProjectDialog** — window to choose a project name and location
    - **ProjectExplorer** — list of the project's image source files
  - **Settings** subsystem
    - **SettingsService** — versioned per-user settings file
    - **SettingsDialog** — modal settings window hosting settings pages
    - **AiProviderSettingsPage** — AI provider settings page
    - **AppearanceSettingsPage** — theme and explorer options page
  - **Speech** subsystem
    - **AudioDeviceService** — audio device listing and capture device creation through the Speech library
    - **SpeechModelService** — speech model listing, downloading, and engine loading through the Speech library
    - **DictationService** — dictation: speech held to talk into text, with the native runtime checked first
    - **DeviceSelection** — microphone and speaker pickers
    - **ModelCatalogPanel** — speech model lists with downloads
    - **ModelParameterEditor** — editors for the tunable settings of a model
    - **SpeechSettingsPage** — speech settings page
  - **Style** subsystem
    - **StyleKitDocument** — the values and the file format of a style kit, with a message for each wrong property
    - **StyleKitCatalog** — the kits in the program, user, project, and shared places, and the choice by precedence
    - **StyleKitArchive** — a kit as a zip file, exported, and imported as untrusted input
    - **StyleSelection** — the kit of the open project, the choice, the default kit, and the export sizes
    - **StyleGuidance** — the style of a kit as text of a known size for an assistant
    - **StyleChecker** — a picture measured against the palette of a kit, and the command of the Image menu
    - **StyleTools** — the tools style_get and style_check of an assistant
    - **StylePicker** — the choice of the kit of the project in the pill of the title bar
    - **StyleSettingsPage** — the settings page for shared kit folders and the default kit
    - **StyleKitEditor** — the editing of one kit of the user or of the project
    - **StyleKitManager** — the window that shows and manages the kits
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
- A live conversation with GitHub Copilot needs the installed CLI and a sign-in, so it is not part of the tests that run
  on every machine: the readiness checks, the starter, the messages, and the agent on a stand-in runtime are verified
  by the tests, and the live runs are opt-in tests and the manual checks of the system verification; the live
  Ollama checks are manual checks of the system verification, because Ollama is not part of the build machines
- User-selectable tool packs and spoken replies of the copilot are not part of this version
- Appium end-to-end tests of the installed application are deferred to a later increment
- The internal implementation of OTS software items is excluded; only integration and usage
  are verified

## Folder Layout

The test folder structure mirrors the source subsystem breakdown:

```text
test/
├── DemaConsulting.CanvasPilot.Tests/  — unit, subsystem, and system tests
│   ├── AgentSubsystem/
│   ├── AnnotationSubsystem/
│   ├── AppShellSubsystem/
│   ├── ChatSubsystem/
│   ├── ComposerSubsystem/
│   ├── KnowledgeSubsystem/
│   ├── LoggingSubsystem/
│   ├── PayloadSubsystem/
│   ├── PreviewSubsystem/
│   ├── ProjectSubsystem/
│   ├── SettingsSubsystem/
│   ├── SpeechSubsystem/
│   ├── StyleSubsystem/
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
editor, a real speech model download, dictating with a real microphone and a real model, and the look and use of the
window on a real display (the height of the system buttons against the title bar, dragging and double-clicking the empty
title bar, snap layouts, dragging a splitter with the mouse, Ctrl+wheel zoom and middle-button panning, the window on
a second monitor, a screen reader, and the display scales of 125 and 150 percent). The checklist is in the
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
