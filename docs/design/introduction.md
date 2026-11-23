# Introduction

This document provides the detailed design for CanvasPilot, an AI-assisted drawing application built on CanvasNet.
CanvasPilot is an Avalonia desktop application for Windows that is delivered as an MSI installer package. It provides
folder-based projects, an explorer tree of the project's folders and image source files with small pictures, a preview
of the selected file that is drawn by CanvasNet on a stage with zoom, PNG export, copy to the clipboard, and drag-out of
the shown picture, a welcome screen, a title bar with the style and AI provider pills, a status bar, a window layout
that is remembered, a dark and a light theme with one set of design tokens and line icons, persisted settings with AI
provider and speech choices, a chat panel with a message composer (typed and dictated text, attached files, folders, and
pictures, and numbered marks drawn on the shown picture), a transcript of the sent messages with a payload inspector,
the drawing knowledge (a tree of cards about the image formats and about CanvasPilot, with a browser in the Help menu),
style kits (palettes, fonts, sizes, and example images that a project, a user, or a team keeps, with a window to manage
them, export at their sizes, and a check of a picture against the palette), and the installer. Messages are answered by
an AI copilot that runs through a local Ollama server (the provider that is covered by automated tests in this release;
live checks are pending) or through GitHub Copilot (which runs on the Copilot CLI that the user installed, signed in
with a token variable, with the sign-in of the Copilot CLI itself, or through the GitHub CLI). The copilot draws with
locked-down tools that are confined to the project, and the chat keeps the text of the conversation on the user's
computer, outside the project. The document covers local software items (systems, subsystems, and units) and the OTS
software items they consume.

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
    report), **CopilotCliLocator** unit (the installed Copilot CLI, with its process runner, file system, and
    environment seams), **CopilotRuntimeStarter** unit (the one Copilot runtime on the installed CLI, shared by the
    readiness check, the model list, and the conversations, with the supporting types of the starter and of the source
    of Copilot agents), **ProviderModelCatalog** unit (the models that the chosen provider offers, with what each can
    do, and the policy of what the application needs of a model), **AgentReadiness** unit (the check of whether the
    chosen AI provider can be used, with the probe of the Ollama server), **AgentToolRegistry** unit (the tools of the
    copilot for one project, confined to the project folder), **ProjectTools** unit (the tools that list, read, write,
    and draw the files of the project, with links refused and files replaced directly), **AgentInstructions** unit (the
    role prompt, the knowledge index, the project facts, and the style, in a full and a compact tier),
    **PayloadMessageMapper** unit (the message of the user as a model message with the marks described and the pictures
    as images), **AgentEventMapper** unit (the updates of an agent as the events of a turn, and the failures of a turn
    as sentences that say what to do), **AgentProviderFactory** unit (the agent of a conversation for Ollama or for
    GitHub Copilot, on the shared runtime of the starter), and **AgentService** unit (the conversation of the open
    project, one turn at a time, with the data of the Copilot runtime kept outside the project).
  - **Annotation** subsystem: **MarkRegistry**, **MarkSourceResolver**, and **AnnotationOverlay** units.
  - **AppShell** subsystem: **MainWindowShell**, **ProjectWorkspace**, **StatusBar**, **ProviderPill**, **Welcome**,
    **AboutDialog**, and **ThemeResources** units.
  - **Chat** subsystem: **ChatPanel**, **ChatTurnRunner**, **ChatHistoryStore**, and **PayloadInspector** units.
  - **Composer** subsystem: **ComposerDocument**, **ComposerHistory**, **AttachmentIntake**, **AttachmentStore**,
    **ComposerViewModel**, and **ComposerEditor** units.
  - **Knowledge** subsystem: **KnowledgeCatalog**, **KnowledgeSearch**, **KnowledgePromptBuilder**,
    **KnowledgeTools**, **KnowledgeSkills**, **KnowledgeMarkdown**, and **KnowledgeBrowser** units.
  - **Logging** subsystem: **RollingFileLogger** unit.
  - **Payload** subsystem: **MessagePayload**, **MarkImageComposer**, **PayloadBuilder**, and **PayloadDescriber**
    units.
  - **Preview** subsystem: **ImageRenderService**, **PreviewPane**, **ThumbnailProvider**, **ImageExporter**,
    **ImageDragSource**, and **ExportPresetDialog** units.
  - **Project** subsystem: **ProjectService**, **RecentProjects**, **NewProjectDialog**, and
    **ProjectExplorer** units.
  - **Settings** subsystem: **SettingsService**, **SettingsDialog**, **AiProviderSettingsPage**, and
    **AppearanceSettingsPage** units (the AI provider page lists the models of the provider and offers the reasoning
    levels that the chosen model has).
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
  the client of the Copilot CLI that AgentKit drives on the installed CLI and that is never bundled, agents and
  streamed turns, tool, message, and picture types, and the client of the Ollama server).
- **WiX Toolset**: integration and usage design (builds the Windows installer package).
- **xUnit**: integration and usage design (test framework).
- **BuildMark**, **FileAssert**, **Pandoc**, **ReqStream**, **ReviewMark**, **SarifMark**, **SonarMark**,
  **SysML2Tools**, **VersionMark**, and **WeasyPrint**: integration and usage design of the build, documentation, and
  analysis tools of the pipeline.

The following topics are out of scope:

- Design documents are not produced for the test projects, the executable project that starts the application, the
  installer project (which is covered by the WiX Toolset OTS design), or the build pipeline CI configuration
- A setting for the GitHub host (for a GitHub Enterprise Cloud account that signs in on a host of its own) and a switch
  that turns the sign-in of the Copilot CLI off are possible future options of the AI library that CanvasPilot does
  not use: the runtime signs in from the first source that applies and follows the host of that account (see the
  *Running the Installed Copilot CLI* section of the system design), and the application never bundles or downloads a
  CLI
- User-selectable tool packs of the AI library (neither its file and text tools package nor its visual tools package,
  which holds the image view tool and the diagram tools, is referenced in this release) and spoken replies of the
  copilot are future work; the copilot has only the fixed, locked-down set of tools that the *Agent* subsystem design
  describes, and a later change will compare the visual tools of the library with the help and render tools of
  CanvasPilot before any of them is adopted
- The internal design of OTS software items is excluded; only integration and usage design is documented

## Software Structure

The software structure is modeled in SysML2 under `docs/sysml2/` and rendered to the diagram
below by SysML2Tools as part of the build pipeline. AI agents should query the SysML2 model
directly (see the `sysml2tools-query` skill) rather than parsing this diagram or the prose
elsewhere in this document. The diagram shows the system and its fourteen subsystems, with the runtime OTS items that
the system consumes; the units are in the view of each subsystem, and the dependencies between the units are in the
model and are shown in the focused dependency views that the design of the larger subsystems embeds. The build and
documentation tools listed in Scope above are documented separately (see `docs/design/ots.md`).

![The CanvasPilot system with its fourteen subsystems and the runtime third-party items it
uses](SoftwareStructureView.svg)

## Architecture

The next diagram shows how the parts fit together at run time. The small executable project only starts the Avalonia
application. The application library holds the subsystems: the AppShell subsystem builds every unit in one place (the
composition root) and hosts the others, and nothing else refers to it. The Chat subsystem is the panel in which the user
converses; it uses the Composer subsystem (the message, its undo steps, and the attachments), the Annotation subsystem
(marks on the picture), the Payload subsystem (the message that is sent), and the Speech subsystem (dictation).
Annotation and Payload read the pictures of the Preview subsystem, which follows the file selected in the Project
explorer; Speech also adds a page to the Settings dialog. The Knowledge subsystem is the guide to drawing: the AppShell
opens its browser from the Help menu, and it depends on nothing but the logger and CanvasNet's help cards, so the
copilot can be given the same cards. The Style subsystem holds the style kits: the AppShell shows its window from the
Tools menu and its pill of kits in the title bar, the Preview subsystem offers the sizes of the kit of the project when
a picture is exported, and the check of a picture against the palette puts its result among the notes of the preview; it
depends on Project, Settings, Preview, and Knowledge (only the descriptor of a tool), and none of them depends on it.
The Agent subsystem is the AI copilot: the Chat subsystem reaches it only through `IAgentService` and the events of a
turn, it uses Settings (the provider choices), Project (the open folder), Payload (the message to answer), Knowledge,
Style, and Preview (what its tools give the copilot), and the AI libraries, and it implements the connection check and
the model list that Settings declares, so Settings never refers to it. Every subsystem logs through Logging, and the
subsystems that build file locations use Utilities. The third-party items are used inside the library, and the
application reads and writes only the local data in the bottom row. Section *Architecture* of the system design explains
the dependencies between the subsystems, which run one way, and why no subsystem refers to AppShell.

![Architecture of CanvasPilot: the desktop head starts the application library, whose AppShell subsystem builds and
hosts the other thirteen subsystems, from Chat, Composer, and Agent down to Logging and Utilities, with the Knowledge
and Style subsystems beside them, and the third-party items and the local data the application uses](architecture.svg)

## Folder Layout

- **src/** - source files and projects
  - **DemaConsulting.CanvasPilot/** - main application library source
    - **AgentSubsystem/** - the AI copilot behind the chat: the provider-independent contract that runs a turn and
      reports readiness, the check of the Copilot CLI and of the Ollama server, the one Copilot runtime on the installed
      CLI (`CopilotRuntimeStarter.cs`, with `ICopilotRuntimeStarter.cs` and `CopilotAgentSource.cs`), the models that
      the provider offers (`ProviderModelCatalog.cs`, with `ModelRequirements.cs` and `OllamaModelCatalogs.cs`), the
      tools that are confined to the project, the instructions, the mapping of messages and events, the agent of the
      chosen provider, and the service that keeps the conversation of the open project
    - **AnnotationSubsystem/** - marks on the shown picture: pinned to their picture, numbered, drawn with the pointer
      or the keyboard on a layer over the picture, and mapped to the parts of the source file under them
    - **AppShellSubsystem/** - main window with its title bar, menu, and status bar, the AI provider pill, the welcome
      screen with its recent project cards, project workspace with its saved window layout, About dialog, composition
      root, view locator, and the design tokens, vendored line icons, and control themes of the dark and light themes
      (`Styles/`)
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
    - **PreviewSubsystem/** - image rendering with CanvasNet, preview pane with its stage and zoom, the small pictures
      of the explorer, PNG and clipboard export, and drag-out
    - **ProjectSubsystem/** - projects, recent projects, New Project dialog, and the project explorer with its tree,
      header, drop zone, and thumbnails
    - **SettingsSubsystem/** - persisted settings, settings dialog, and settings pages
    - **SpeechSubsystem/** - audio devices, speech models and their downloads, and the Speech settings page
    - **StyleSubsystem/** - style kits: the format, the places that hold them, zip import and export, the kit of the
      project, the text and tools for an assistant, the check of a picture against a palette, the pill in the title bar,
      the settings page, the window that manages kits and the editor, and the kits that come with the program under
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
