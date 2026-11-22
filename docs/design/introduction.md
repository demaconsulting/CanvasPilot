# Introduction

This document provides the detailed design for CanvasPilot, an AI-assisted drawing application built on CanvasNet.
CanvasPilot is an Avalonia desktop application for Windows that is delivered as an MSI installer package. It provides
folder-based projects, an explorer of the project's image source files, a preview of the selected file that is drawn by
CanvasNet, PNG export, copy to the clipboard, and drag-out of the shown picture, persisted settings with AI provider and
speech choices, a chat panel with a message composer (typed and dictated text, attached files, folders, and pictures,
and numbered marks drawn on the shown picture), a transcript of the sent messages with a payload inspector, the drawing
knowledge (a tree of cards about the image formats and about CanvasPilot, with a browser in the Help menu), style kits
(palettes, fonts, sizes, and example images that a project, a user, or a team keeps, with a window to manage them,
export at their sizes, and a check of a picture against the palette), and the installer. Messages are composed and sent
without an agent: the AI agent that will answer them and use the stored choices, the knowledge, and the style is out of
scope of this version. The document covers local software items (systems, subsystems, and units) and the OTS software
items they consume.

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
  - **Agent** subsystem: **AgentContract** unit (`IAgentService`, the turn request, the events, and the readiness
    report); the units that implement it are added as they are built.
  - **Annotation** subsystem: **MarkRegistry**, **MarkSourceResolver**, and **AnnotationOverlay** units.
  - **AppShell** subsystem: **MainWindowShell**, **ProjectWorkspace**, and **AboutDialog** units.
  - **Chat** subsystem: **ChatPanel** and **PayloadInspector** units.
  - **Composer** subsystem: **ComposerDocument**, **ComposerHistory**, **AttachmentIntake**, **AttachmentStore**,
    **ComposerViewModel**, and **ComposerEditor** units.
  - **Knowledge** subsystem: **KnowledgeCatalog**, **KnowledgeSearch**, **KnowledgePromptBuilder**,
    **KnowledgeTools**, **KnowledgeSkills**, **KnowledgeMarkdown**, and **KnowledgeBrowser** units.
  - **Logging** subsystem: **RollingFileLogger** unit.
  - **Payload** subsystem: **MessagePayload**, **MarkImageComposer**, **PayloadBuilder**, and **PayloadDescriber**
    units.
  - **Preview** subsystem: **ImageRenderService**, **PreviewPane**, **ImageExporter**, **ImageDragSource**, and
    **ExportPresetDialog** units.
  - **Project** subsystem: **ProjectService**, **RecentProjects**, **NewProjectDialog**, and
    **ProjectExplorer** units.
  - **Settings** subsystem: **SettingsService**, **SettingsDialog**, and **AiProviderSettingsPage** units.
  - **Speech** subsystem: **AudioDeviceService**, **SpeechModelService**, **DictationService**, **DeviceSelection**,
    **ModelCatalogPanel**, **ModelParameterEditor**, and **SpeechSettingsPage** units.
  - **Style** subsystem: **StyleKitDocument**, **StyleKitCatalog**, **StyleKitArchive**, **StyleSelection**,
    **StyleGuidance**, **StyleChecker**, **StyleTools**, **StylePicker**, **StyleSettingsPage**, **StyleKitEditor**, and
    **StyleKitManager** units.
  - **Utilities** subsystem: **PathHelpers** unit.

OTS items:

- **Avalonia**: integration and usage design (runtime user interface framework).
- **AvaloniaEdit**: integration and usage design (runtime text editing control of the message composer).
- **CommunityToolkit.Mvvm**: integration and usage design (runtime view model source generators).
- **CanvasNet**: integration and usage design (runtime library that draws image source files).
- **Speech**: integration and usage design (runtime library for audio devices, speech models, and speech recognition,
  and the native speech runtime that comes with it).
- **AgentKit**, **Microsoft.Copilot.SDK**, **Microsoft Agent Framework**, **Microsoft.Extensions.AI**, and
  **OllamaSharp**: integration and usage design of the libraries of the AI copilot (confined tools and agent factories,
  the client of the Copilot CLI that AgentKit drives and that is never bundled or started in this version, agents and
  streamed turns, tool, message, and picture types, and the client of the Ollama server).
- **WiX Toolset**: integration and usage design (builds the Windows installer package).
- **xUnit**: integration and usage design (test framework).
- **BuildMark**, **FileAssert**, **Pandoc**, **ReqStream**, **ReviewMark**, **SarifMark**, **SonarMark**,
  **SysML2Tools**, **VersionMark**, and **WeasyPrint**: integration and usage design of the build, documentation, and
  analysis tools of the pipeline.

The following topics are out of scope:

- Design documents are not produced for the test projects, the executable project that starts the application, the
  installer project (which is covered by the WiX Toolset OTS design), or the build pipeline CI configuration
- The AI agent that answers the messages is not part of this version; the settings that it will read are designed (see
  the *AI Provider Seam* section of the system design), and the payload that it will receive is built and shown for
  every sent message, but nothing reads it and no answer comes back; the drawing knowledge that it will be given is
  built and can be read in the browser, and the style that it will be given and the two tools that it will use to read
  the style and check a picture are built and tested, but no assistant uses any of it yet
- The internal design of OTS software items is excluded; only integration and usage design is documented

## Software Structure

The software structure is modeled in SysML2 under `docs/sysml2/` and rendered to the diagram
below by SysML2Tools as part of the build pipeline. AI agents should query the SysML2 model
directly (see the `sysml2tools-query` skill) rather than parsing this diagram or the prose
elsewhere in this document. The diagram shows the system and its thirteen subsystems, with the runtime OTS items that
the system consumes; the units are in the view of each subsystem, and the dependencies between the units are in the
model and are shown in the focused dependency views that the design of the larger subsystems embeds. The build and
documentation tools listed in Scope above are documented separately (see `docs/design/ots.md`).

![The CanvasPilot system with its thirteen subsystems and the runtime third-party items it
uses](SoftwareStructureView.svg)

## Architecture

The next diagram shows how the parts fit together at run time. The small executable project only starts the Avalonia
application. The application library holds the subsystems: the AppShell subsystem builds every unit in one place
(the composition root) and hosts the others, and nothing else refers to it. The Chat subsystem is the panel in which the
user converses; it uses the Composer subsystem (the message, its undo steps, and the attachments), the Annotation
subsystem (marks on the picture), the Payload subsystem (the message that is sent), and the Speech subsystem
(dictation). Annotation and Payload read the pictures of the Preview subsystem, which follows the file selected in
the Project explorer; Speech also adds a page to the Settings dialog. The Knowledge subsystem is the guide to drawing:
the AppShell opens its browser from the Help menu, and it depends on nothing but the logger and CanvasNet's help cards,
so an assistant added later can be given the same cards. The Style subsystem holds the style kits: the AppShell shows
its window from the Tools menu and its list of kits in the status bar, the Preview subsystem offers the sizes of the
kit of the project when a picture is exported, and the check of a picture against the palette puts its result among the
notes of the preview; it depends on Project, Settings, Preview, and Knowledge (only the descriptor of a tool), and none
of them depends on it. Every subsystem logs through Logging, and the
subsystems that build file locations use Utilities. The third-party items are used inside the library, and the
application reads and writes only the local data in the bottom row. Section *Architecture* of the system design
explains the dependencies between the subsystems, which run one way, and why no subsystem refers to AppShell.

![Architecture of CanvasPilot: the desktop head starts the application library, whose AppShell subsystem builds and
hosts the thirteen subsystems, from Chat and Composer down to Logging and Utilities, with the Knowledge and Style
subsystems beside them, and the third-party items and the local data the application uses](architecture.svg)

## Folder Layout

- **src/** - source files and projects
  - **DemaConsulting.CanvasPilot/** - main application library source
    - **AgentSubsystem/** - the AI copilot behind the chat: the provider-independent contract that runs a turn and
      reports readiness
    - **AnnotationSubsystem/** - marks on the shown picture: pinned to their picture, numbered, drawn with the pointer
      or the keyboard on a layer over the picture, and mapped to the parts of the source file under them
    - **AppShellSubsystem/** - main window, project workspace, About dialog, composition root, and view locator
    - **ChatSubsystem/** - the chat panel with the transcript of the sent messages, the composer, the send and
      hold-to-talk buttons and the joining of marks to the message, and the payload inspector window
    - **ComposerSubsystem/** - the message being composed: ordered segments of text and chips, undo and redo, the
      intake and copying of attachments, the view model that orchestrates them with dictation and sending, and the
      editing view that shows the message with its chips inline
    - **KnowledgeSubsystem/** - the drawing knowledge: the tree of cards from the help of CanvasNet and the cards of
      CanvasPilot (embedded Markdown under `Cards/`), the search of the cards, their text for the prompt of an
      assistant, the tool and the skills that describe them, the Markdown reader, and the browser window
    - **LoggingSubsystem/** - local rolling log file subsystem
    - **PayloadSubsystem/** - the message that is sent as one immutable payload: text with references, attachments,
      annotated pictures, mark crops, mark geometry, and source elements, and its description
    - **PreviewSubsystem/** - image rendering with CanvasNet, preview pane, PNG and clipboard export, and drag-out
    - **ProjectSubsystem/** - projects, recent projects, New Project dialog, and project explorer
    - **SettingsSubsystem/** - persisted settings, settings dialog, and settings pages
    - **SpeechSubsystem/** - audio devices, speech models and their downloads, and the Speech settings page
    - **StyleSubsystem/** - style kits: the format, the places that hold them, zip import and export, the kit of the
      project, the text and tools for an assistant, the check of a picture against a palette, the list in the status
      bar, the settings page, the window that manages kits and the editor, and the kits that come with the program under
      BuiltIn/
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
- **OTS tests** (`test/OtsSoftwareTests`) call the runtime OTS libraries (Avalonia, AvaloniaEdit,
  CommunityToolkit.Mvvm, CanvasNet, Speech, AgentKit, Microsoft.Copilot.SDK, the Microsoft Agent Framework,
  Microsoft.Extensions.AI, and OllamaSharp) the way the application does, and are the qualification evidence for
  libraries that have no self-validation mode.
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
