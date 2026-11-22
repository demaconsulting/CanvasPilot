# CanvasPilot

![CanvasPilot Structure](CanvasPilotView.svg)

## Architecture

CanvasPilot is a desktop application built on .NET and Avalonia. It is structured as one system. The
software items documented are fourteen subsystems, `Agent`, `Annotation`, `AppShell`, `Chat`, `Composer`, `Knowledge`,
`Logging`, `Payload`, `Preview`, `Project`, `Settings`, `Speech`, `Style`, and `Utilities`.

```{.mermaid scale=0.7}
flowchart TD
    subgraph Agent
        AgentContract
        CopilotCliLocator
        CopilotRuntimeStarter
        ProviderModelCatalog
        AgentReadiness
        AgentToolRegistry
        ProjectTools
        AgentInstructions
        PayloadMessageMapper
        AgentEventMapper
        AgentProviderFactory
        AgentService
    end
    subgraph Annotation
        MarkRegistry
        MarkSourceResolver
        AnnotationOverlay
    end
    subgraph AppShell
        MainWindowShell
        ProjectWorkspace
        AboutDialog
        ThemeResources
    end
    subgraph Chat
        ChatPanel
        ChatTurnRunner
        ChatHistoryStore
        PayloadInspector
    end
    subgraph Composer
        ComposerDocument
        ComposerHistory
        AttachmentIntake
        AttachmentStore
        ComposerViewModel
        ComposerEditor
    end
    subgraph Knowledge
        KnowledgeCatalog
        KnowledgeSearch
        KnowledgePromptBuilder
        KnowledgeTools
        KnowledgeSkills
        KnowledgeMarkdown
        KnowledgeBrowser
    end
    subgraph Logging
        RollingFileLogger
    end
    subgraph Payload
        MessagePayload
        MarkImageComposer
        PayloadBuilder
        PayloadDescriber
    end
    subgraph Preview
        ImageRenderService
        PreviewPane
        ImageExporter
        ImageDragSource
        ExportPresetDialog
    end
    subgraph Project
        ProjectService
        RecentProjects
        NewProjectDialog
        ProjectExplorer
    end
    subgraph Settings
        SettingsService
        SettingsDialog
        AiProviderSettingsPage
    end
    subgraph Speech
        AudioDeviceService
        SpeechModelService
        DictationService
        DeviceSelection
        ModelCatalogPanel
        ModelParameterEditor
        SpeechSettingsPage
    end
    subgraph Style
        StyleKitDocument
        StyleKitCatalog
        StyleKitArchive
        StyleSelection
        StyleGuidance
        StyleChecker
        StyleTools
        StylePicker
        StyleSettingsPage
        StyleKitEditor
        StyleKitManager
    end
    subgraph Utilities
        PathHelpers
    end
    RollingFileLogger --> PathHelpers
    ComposerHistory --> ComposerDocument
    AttachmentIntake --> ComposerDocument
    AttachmentIntake --> AttachmentStore
    AttachmentIntake --> RollingFileLogger
    AttachmentStore --> PathHelpers
    AttachmentStore --> RollingFileLogger
    AttachmentStore --> ProjectService
    ComposerViewModel --> ComposerDocument
    ComposerViewModel --> ComposerHistory
    ComposerViewModel --> AttachmentIntake
    ComposerViewModel --> AttachmentStore
    ComposerViewModel --> MarkRegistry
    ComposerViewModel --> DictationService
    ComposerViewModel --> RollingFileLogger
    ComposerEditor --> ComposerViewModel
    ComposerEditor --> AttachmentIntake
    ChatPanel --> ComposerViewModel
    ChatPanel --> ComposerEditor
    ChatPanel --> MarkRegistry
    ChatPanel --> AnnotationOverlay
    ChatPanel --> MarkImageComposer
    ChatPanel --> MessagePayload
    ChatPanel --> DictationService
    ChatPanel --> PayloadInspector
    ChatPanel --> ChatTurnRunner
    ChatPanel --> ChatHistoryStore
    ChatPanel --> ProjectService
    ChatPanel --> AgentContract
    ChatPanel --> KnowledgeMarkdown
    ChatPanel --> RollingFileLogger
    ChatPanel --> PathHelpers
    ChatTurnRunner --> ChatPanel
    ChatTurnRunner --> AgentContract
    ChatTurnRunner --> ComposerViewModel
    ChatTurnRunner --> MessagePayload
    ChatTurnRunner --> RollingFileLogger
    ChatHistoryStore --> ProjectService
    ChatHistoryStore --> RollingFileLogger
    ChatHistoryStore --> PathHelpers
    PayloadInspector --> PayloadDescriber
    PayloadInspector --> MessagePayload
    MainWindowShell --> PayloadInspector
    MainWindowShell --> ComposerViewModel
    MarkRegistry --> ImageRenderService
    AnnotationOverlay --> MarkRegistry
    AnnotationOverlay --> PreviewPane
    AnnotationOverlay --> RollingFileLogger
    MarkSourceResolver --> MarkRegistry
    MarkSourceResolver --> ImageRenderService
    MarkImageComposer --> ImageRenderService
    MarkImageComposer --> MarkRegistry
    PayloadBuilder --> ComposerDocument
    PayloadBuilder --> AttachmentStore
    PayloadBuilder --> MarkRegistry
    PayloadBuilder --> MarkSourceResolver
    PayloadBuilder --> MarkImageComposer
    PayloadBuilder --> MessagePayload
    PayloadBuilder --> ComposerViewModel
    PayloadBuilder --> ImageRenderService
    PayloadDescriber --> MessagePayload
    MessagePayload --> ComposerDocument
    MessagePayload --> MarkRegistry
    MessagePayload --> MarkSourceResolver
    MessagePayload --> ImageRenderService
    ImageRenderService --> RollingFileLogger
    PreviewPane --> ImageRenderService
    PreviewPane --> ProjectExplorer
    PreviewPane --> ProjectService
    PreviewPane --> ImageDragSource
    PreviewPane --> RollingFileLogger
    ImageExporter --> ImageRenderService
    ImageExporter --> RollingFileLogger
    ImageDragSource --> ImageRenderService
    ImageDragSource --> ImageExporter
    ImageDragSource --> PathHelpers
    ImageDragSource --> RollingFileLogger
    ProjectService --> PathHelpers
    ProjectService --> RollingFileLogger
    RecentProjects --> SettingsService
    NewProjectDialog --> ProjectService
    NewProjectDialog --> PathHelpers
    ProjectExplorer --> ProjectService
    ProjectExplorer --> PathHelpers
    ProjectExplorer --> RollingFileLogger
    SettingsService --> PathHelpers
    SettingsService --> RollingFileLogger
    SettingsDialog --> SettingsService
    AiProviderSettingsPage --> SettingsDialog
    AudioDeviceService --> RollingFileLogger
    SpeechModelService --> RollingFileLogger
    DictationService --> SettingsService
    DictationService --> SpeechModelService
    DictationService --> AudioDeviceService
    DictationService --> RollingFileLogger
    DeviceSelection --> AudioDeviceService
    ModelCatalogPanel --> SpeechModelService
    SpeechSettingsPage --> SettingsDialog
    SpeechSettingsPage --> DeviceSelection
    SpeechSettingsPage --> ModelCatalogPanel
    SpeechSettingsPage --> ModelParameterEditor
    MainWindowShell --> ProjectService
    MainWindowShell --> RecentProjects
    MainWindowShell --> NewProjectDialog
    MainWindowShell --> SettingsDialog
    MainWindowShell --> ProjectWorkspace
    MainWindowShell --> ImageExporter
    MainWindowShell --> AboutDialog
    MainWindowShell --> KnowledgeBrowser
    KnowledgeSearch --> KnowledgeCatalog
    KnowledgePromptBuilder --> KnowledgeCatalog
    KnowledgePromptBuilder --> KnowledgeSearch
    KnowledgeTools --> KnowledgeCatalog
    KnowledgeTools --> KnowledgeSearch
    KnowledgeTools --> KnowledgePromptBuilder
    KnowledgeSkills --> KnowledgeCatalog
    KnowledgeSkills --> KnowledgePromptBuilder
    KnowledgeBrowser --> KnowledgeCatalog
    KnowledgeBrowser --> KnowledgeSearch
    KnowledgeBrowser --> KnowledgeMarkdown
    KnowledgeBrowser --> RollingFileLogger
    MainWindowShell --> ExportPresetDialog
    MainWindowShell --> StyleChecker
    MainWindowShell --> StylePicker
    MainWindowShell --> StyleKitManager
    StyleKitCatalog --> StyleKitDocument
    StyleKitCatalog --> ProjectService
    StyleKitCatalog --> SettingsService
    StyleKitCatalog --> PathHelpers
    StyleKitCatalog --> RollingFileLogger
    StyleKitArchive --> StyleKitDocument
    StyleKitArchive --> PathHelpers
    StyleSelection --> StyleKitCatalog
    StyleSelection --> ProjectService
    StyleSelection --> SettingsService
    StyleSelection --> ImageExporter
    StyleGuidance --> StyleKitDocument
    StyleGuidance --> StyleKitCatalog
    StyleChecker --> StyleKitDocument
    StyleChecker --> StyleSelection
    StyleChecker --> ImageRenderService
    StyleChecker --> PreviewPane
    StyleTools --> StyleSelection
    StyleTools --> StyleKitCatalog
    StyleTools --> StyleGuidance
    StyleTools --> StyleChecker
    StyleTools --> ImageRenderService
    StyleTools --> KnowledgeTools
    StyleTools --> PathHelpers
    StylePicker --> StyleSelection
    StylePicker --> StyleKitCatalog
    StylePicker --> ProjectService
    StyleSettingsPage --> SettingsDialog
    StyleSettingsPage --> StyleKitCatalog
    StyleSettingsPage --> StylePicker
    StyleSettingsPage --> ProjectService
    StyleKitEditor --> StyleKitCatalog
    StyleKitEditor --> StyleKitDocument
    StyleKitEditor --> StylePicker
    StyleKitManager --> StyleKitCatalog
    StyleKitManager --> StyleSelection
    StyleKitManager --> StyleKitArchive
    StyleKitManager --> StyleKitEditor
    StyleKitManager --> StyleGuidance
    StyleKitManager --> StylePicker
    StyleKitManager --> ImageRenderService
    StyleKitManager --> ProjectService
    ProjectWorkspace --> ProjectExplorer
    ProjectWorkspace --> ChatPanel
    AgentReadiness --> CopilotCliLocator
    AgentReadiness --> CopilotRuntimeStarter
    CopilotRuntimeStarter --> AgentContract
    ProviderModelCatalog --> CopilotRuntimeStarter
    ProviderModelCatalog --> CopilotCliLocator
    ProviderModelCatalog --> AiProviderSettingsPage
    AgentReadiness --> ProviderModelCatalog
    AgentReadiness --> SettingsService
    AgentReadiness --> AiProviderSettingsPage
    AgentToolRegistry --> KnowledgeTools
    AgentToolRegistry --> ProjectService
    ProjectTools --> AgentToolRegistry
    ProjectTools --> ImageRenderService
    ProjectTools --> StyleSelection
    ProjectTools --> StyleKitCatalog
    ProjectTools --> ProjectService
    ProjectTools --> ProjectExplorer
    ProjectTools --> RollingFileLogger
    AgentInstructions --> KnowledgePromptBuilder
    AgentInstructions --> StyleGuidance
    AgentInstructions --> SettingsService
    PayloadMessageMapper --> MessagePayload
    AgentEventMapper --> AgentContract
    AgentEventMapper --> AgentReadiness
    AgentEventMapper --> SettingsService
    AgentEventMapper --> RollingFileLogger
    AgentProviderFactory --> AgentContract
    AgentProviderFactory --> CopilotRuntimeStarter
    AgentProviderFactory --> ProviderModelCatalog
    AgentProviderFactory --> CopilotCliLocator
    AgentProviderFactory --> SettingsService
    AgentProviderFactory --> RollingFileLogger
    AgentService --> AgentContract
    AgentService --> AgentToolRegistry
    AgentService --> AgentInstructions
    AgentService --> PayloadMessageMapper
    AgentService --> AgentEventMapper
    AgentService --> AgentReadiness
    AgentService --> AgentProviderFactory
    AgentService --> ProjectTools
    AgentService --> SettingsService
    AgentService --> ProjectService
    AgentService --> StyleSelection
    AgentService --> RollingFileLogger
```

`PathHelpers` provides safe path combination and has no dependencies on other local items.
`RollingFileLogger` is the application's only log writer. It uses `PathHelpers` to resolve the active
log file and archive file paths inside its log folder, so that a malformed file name can never place a
log file outside that folder.

`SettingsService` owns the persisted settings and is the only item that reads or writes the settings file.
`SettingsDialog` hosts settings pages through a page contract and saves them through the service;
`AiProviderSettingsPage` is the page for the AI provider choices and implements that contract. The dialog receives its
pages from the application, so the `Settings` subsystem does not depend on the subsystems that contribute pages. The AI
provider choices are stored by the service and read by the `Agent` subsystem: the readiness checks and the agent of a
conversation take them from the service and react to its `Changed` event, while `Settings` declares only the
`IProviderCheck` interface that the settings dialog uses to check a connection and the `IProviderModelSource` interface
that the page uses to list the models of the provider, and never refers to `Agent`.

`ProjectService` owns projects: a project is a folder holding a `{name}.canvaspilot` project file, a hidden
`.canvaspilot` folder for application data (the pictures that the user pastes into a message while it is a draft), and
the image source files, and, once a message with attachments is sent, an `attachments` folder. It is the only item that
writes project files. `RecentProjects` keeps the list of recently opened project files in the application settings
through `SettingsService`. `NewProjectDialog` collects and validates a project name and location, using
`ProjectService`, and `ProjectExplorer` lists, watches, selects, renames, and deletes the image source files in the root
of the open project's folder. The application supplies the folder picker, the image source filter, and the refresh
scheduler that these units use, so the `Project` subsystem does not depend on the main window.

`MainWindowShell` is the view model of the main window and the only item that talks to the user about projects and
settings: it runs the File, Edit, Image, Tools, and Help commands and shows its dialogs through an `IDialogService` that
the window implements. The Edit commands, Undo and Redo, act on the part of the window that has the focus (the message
editor), and the shell starts and stops the chat panel's dictation while the talk key (F8) is held. `ProjectWorkspace`
holds the project explorer, the preview pane, and, while a project is open, the chat column, and `AboutDialog`
identifies the application, and `ThemeResources` holds the design tokens of the dark and the light theme from which
every view takes its colors, spacing, and type. At startup the application builds all units in one place,
`ApplicationComposition`, which system tests also use without a window.

`AudioDeviceService` is the only item that uses the audio system and `SpeechModelService` the only item that uses
the speech model catalog and its model store, both through the DemaConsulting.Speech library; each returns a machine
without audio hardware, an unreadable model store, and a failed download as a result with an explanation, never as an
exception. `DeviceSelection`, `ModelCatalogPanel`, and `ModelParameterEditor` are the view models and views of the
device pickers, the model lists with their downloads, and the tunable settings of a model, and `SpeechSettingsPage`
composes them into the "Speech" page of the settings dialog and maps them to and from the stored speech settings. The
application adds the page to the dialog after the AI Provider page, so the `Settings` subsystem does not depend on
speech.

`DictationService` turns speech into text for the message. While the user holds a talk control it listens through the
microphone and the speech-to-text model that were stored by the Speech page, shows the words recognized so far, and,
when the control is released, returns the final text, which the chat adds to the message as one dictated block. It is
the only item that holds a loaded speech engine and creates recognition sessions: `SpeechModelService` loads the engine
from the stored choices, `AudioDeviceService` creates the capture device of the chosen microphone, and the engine is
loaded once and reused. Every state in which dictation cannot work, such as no model chosen, a model that is not
downloaded, no microphone, an engine that will not start, or native speech files that are missing, is a result with its
own explanation, never an exception. The native speech runtime arrives with the speech library's packages and is
shipped beside the application and in the installer; `DictationService` checks that it is there before it loads an
engine, because an engine started without it can crash the process, which no code can catch. The talk button of the chat
panel calls the service, and so does the talk key, F8, through the main window.

`ImageRenderService` is the only item that draws files: it calls the CanvasNet viewer library in the background,
confined to the project folder, and returns the pixels, notes, or a coded error. Each picture it returns also says
exactly which picture it is (the file, a fingerprint of the file, and a fingerprint of the pixels) and where the
elements of a diagram, chart, or model were drawn, so that something said about the picture later can be tied to what
the user saw. `PreviewPane` also provides a layer over the shown picture, laid out in the picture's own pixel
coordinates and scaled with it, which other items fill with content that refers to places in the picture, and a slot in
its top bar, not scaled, for the controls of that content. `PreviewPane`
follows the selected file of `ProjectExplorer`, shows what `ImageRenderService` returns, discards results of renders
that are no longer wanted, and renders again when files in the project change. `ImageExporter` saves the shown picture
as a PNG file or copies it to the clipboard for the Image menu: it first asks which size to export at (the picture as
it is shown, or a size that a source of sizes offers), and for a size it draws the file again at that size and
background, so the lines stay sharp, and writes the resolution into the file. `ExportPresetDialog` is the small dialog
in which the user chooses the size. `ImageDragSource` prepares the shown picture for
dragging it into another program, such as a presentation, a mail message, or a folder: it writes the picture as a PNG
file in a folder of its own, and the preview view offers that file and the picture's bitmap to the program the user
drops them on. The `Preview` subsystem owns the interfaces that this needs, `IImageExporter`, `IExportDialogs`,
`IExportPresetSource` (where the sizes come from), and `IRenderNoteSink` (where other parts put notes about the shown
picture), and does not refer to the main window or to the `Style` subsystem.

`ComposerDocument` holds the message that the user is composing for the chat as an ordered sequence of typed text,
dictated text, and chips for attached files, folders, and images and for marks on the picture, and is the single source
of truth about the message. `ComposerHistory` applies each change that the user makes to the message as one undo step,
so that a typed character, a dictated block, a drop or paste of any number of items, an added mark, and a deleted chip
are each one step, and clears the steps, together with the message, when the message is sent. Neither of these units
depends on the user interface, on another subsystem, or on the file system. `AttachmentIntake` turns what the user drops
or pastes into the message, files, folders, pictures, and text, into chips and text, and treats all of it strictly as
data: it only looks at what a path is, never opens, runs, or copies it, and leaves out paths that are not safe to refer
to. `AttachmentStore` is the only item that creates files for attachments, and only inside the project: it saves pasted
pictures in the project's private data folder while the message is a draft, removes the ones that no chip refers to,
and, when the message is sent, copies the attachments from outside the project into the project's `attachments` folder,
all or none, within limits of 500 files and 200 MB for one message, with safe names that never replace another file,
without following links. `ComposerViewModel` is the orchestration of the message without a window: it owns the document,
the history, and the intake, receives what the editing control reports and turns each action of the user into exactly
one undo step, offers undo and redo to the Edit menu and the keys, shows the words that dictation recognizes while a
talk control is held and adds them as one dictated block when it is released, names what a drop left out, allows editing
only while a project is open, and sends the message: it has the attachments put into the project, has the message built
through `IMessageBuilder`, and then empties the draft, the history, and the marks and gives the immutable sent message
to whoever shows the transcript. `ComposerEditor` is the view of the message: one editing surface, the AvaloniaEdit
control, in which text, dictated text, and chips stand in order, each chip as one placeholder character that is drawn as
a small control; it shows what the view model holds and reports everything the user does to it, takes undo, redo,
paste, copy, cut, and drops over from the control and handles dropped and pasted content as data, and keeps the control
equal to the message after every change. The `Payload` subsystem uses the `Composer` subsystem to read the snapshot of
the message and the staged
attachments and to implement `IMessageBuilder`, which `Composer` owns; the `Composer` subsystem uses `Annotation` (the
registry of marks) and `Speech` (dictation) and, to send a message, the `Payload` subsystem only through that interface.

`MarkRegistry` holds the marks that the user draws on the shown picture to say what they mean in a message. Each mark is
pinned to the exact `RenderedImage` it was drawn on, numbered in the order the marks are made, and described by its
shape, its geometry in the pixels of that picture in one normal form, and an optional note of one short line. A mark is
part of the message exactly while its chip is in the composer's text, so the registry answers which marks belong to the
message and which are shown on a given picture; the marks of an earlier version of a file are not shown on the new
picture but stay in the message. `MarkSourceResolver` maps a mark to the parts of the source file that lie under it,
from the element positions and anchors that the renderer reported with the picture, and says that the picture cannot
tell, with the reason, when it reported none. Neither unit depends on the user interface or on the file system; the
`Annotation` subsystem depends only on the picture and coordinate types of `Preview` and on the logger.
`AnnotationOverlay` is the layer on which the user draws the marks: its tool bar offers a rectangle, an ellipse, a
freehand, and an arrow tool; the pointer or the keyboard draws the chosen shape in the pixels of the picture, in the
overlay slot of the preview pane, which is laid out in those pixels at any zoom; a finished shape asks for an optional
note, and then the overlay makes the mark in `MarkRegistry`, pinned to the picture that was shown when the shape was
started, and reports it. The overlay does not know the composer. The chat panel adds the chip of every reported
mark to the message with `ComposerViewModel.AddMark` and tells the overlay which mark chips the message holds, so the
overlay draws only the marks of the shown picture whose chips are in the message and says how many it hides. While a
tool is chosen the overlay has the preview pane turn off dragging the picture out, and Escape throws a shape away.

`MessagePayload` and the values it is made of are the one form in which the message that the user sends reaches the
AI copilot: the text with `[Mark n]` and `[Attachment n: name]` at the places of the marks and
attachments, the same message as parts in order that keep typed text apart from dictated text, the attachments with the
place where the user had them and their path in the project, and, for each picture that marks were drawn on, the picture
with the marks drawn and numbered on it, an image of the area around each mark, the geometry and note of each mark in
the pixels of the picture, and the parts of the source file under each mark. The values copy what they are given and
never change. `PayloadBuilder` makes the payload from a snapshot of the composer, the marks, and the attachments that
were put into the project, uses only the marks whose chips are in the message, and reads only values that cannot change,
so it can run in the background. `MarkImageComposer` draws the marks on the picture with CanvasNet, from an SVG document
that it builds in memory around the picture the user saw, and cuts the areas; it is the only item besides
`ImageRenderService` that uses CanvasNet to draw, and it never writes a file. `PayloadDescriber` writes a payload as
text for the user to read. Only the `Chat` subsystem and the composition root use the `Payload` subsystem, and no
subsystem uses the `Annotation` subsystem except `Payload`, `Composer`, `Chat`, and the composition root.

`ChatPanel` is the panel in which the user converses: above, the transcript of the messages that were sent in the open
project, oldest first; below, the composer's editing control; and between them and the user, a button that listens while
it is held and a button that sends. The transcript shows only sent messages, each once, with its words in the order they
were written (dictated words with the same subtle background as in the editing control), the chips of its attachments
and marks in their places with their pictures, and a button, "View payload". The panel writes nothing itself and keeps
nothing in the project: the transcript is dropped from the screen when the project changes or closes, and the words of
the conversation are kept on the user's computer by `ChatHistoryStore`, below `%LOCALAPPDATA%\CanvasPilot\history` in a
folder named by a hash of the path of the project file, never in the project folder, which may be copied or put under
source control. When a project is opened again the panel shows its saved conversation as text (no pictures, no payload),
gives the saved words to the copilot with the next message, and offers "Clear chat history", which deletes the saved
history, empties the transcript, and makes the copilot forget the conversation. Sending is the composer's own command,
so the panel only follows its state and shows the result: an entry for a message that was sent, and, when sending
failed, the composer's status text with the draft untouched. The talk button listens exactly as long as it is held:
pressing the pointer starts it and captures the pointer, and letting go anywhere, losing the capture, losing the focus,
or removing the panel ends it; when dictation cannot work, the composer's status line in the panel says why and what to
do. The panel also joins the annotation layer to the composer, which do not know each other: it adds the chip of every
mark that is drawn to the message at the caret as one undo step, and each time the message changes it tells the layer
which mark chips the message holds and whether marks can be made. It prepares dictation when it is shown and gives the
chips their pictures: the area around the mark, cut from the picture the mark was drawn on, and the preview of an
attached picture file. `PayloadInspector` is the window that "View payload" opens: it shows the text that
`PayloadDescriber` writes for the payload of the message, which is exactly what the assistant would receive, and, next
to it, the pictures of the payload: the annotated picture of every marked file and the area around every mark, each with
a caption. The window cannot change anything. The panel does not know what shows the inspector; it asks an
`IPayloadInspectorLauncher`, which `IDialogService` extends, so the main window shows it as a dialog. `ChatTurnRunner`
holds the logic of one turn of the conversation without a window: it gives the message that was sent to the AI copilot
through `IAgentService`, shows the events of the turn as rows of the transcript (the streamed answer, one line for each
tool that the copilot calls, and notices and errors), stops on request and keeps what was written, never throws into the
interface, and tells the panel when it is busy, so that the panel can block sending while the copilot works. The panel
starts a turn for every message that was sent when it has a copilot (without one, it only adds the entry), shows the
moving indicator and the Stop button while the turn runs, shows the text of a reply as plain selectable text while it
streams and as Markdown, with the `MarkdownBlocksPanel` of the `Knowledge` subsystem, once it is whole, shows a banner
with the sentence of the readiness report of the copilot when the provider cannot be used (or can be used with a
warning), and raises a request to open the settings, from the banner and from an error row, which the shell turns into
its own command; it does not depend on `Settings`. The `Chat` subsystem depends on `Composer`, `Annotation`, `Payload`,
`Speech`, `Logging`, `Project` (the open project and the path of its file, for the history), `Knowledge` (to draw
Markdown only), and, through `IAgentService` and the events of a turn only, `Agent`, and not on `AppShell`. The `Agent`
subsystem does not depend on `Chat`. The main window places the panel in the chat column of the workspace, which is
shown while a project is open, and the shell makes the panel the target of the Edit menu (through the composer) and of
the talk key.

`AgentContract` is the one thing of the AI copilot that the rest of the application sees: `IAgentService` with the
request of a turn, the events that answer it, and the readiness report, all plain records that name no AI library.
`AgentService` implements it for the open project. It makes nothing until the first message is sent, then builds the
tools (`AgentToolRegistry`, with `ProjectTools`), the instructions (`AgentInstructions`), the agent of the chosen
provider (`AgentProviderFactory`), and one provider session, and runs one turn at a time: `PayloadMessageMapper` turns
the `MessagePayload` into the message of the model, and `AgentEventMapper` turns what the agent does into the events
that the chat shows, the same for both providers. The copilot has only locked-down tools: `canvasnet_help`, `style_get`,
`style_check`, `project_list`, `project_read`, `project_write`, and `render_image`; every one is confined to the project
folder, refuses links, the hidden `.canvaspilot` folder, and the project file, and a write replaces a file directly,
with no backup and no question, and says `created` or `replaced`. `AgentReadiness` answers whether the chosen provider
can be used, for the chat banner and for the connection check of the settings dialog: for Ollama it checks the settings,
the server, the model, the model settings (the model can call tools and the window and level fit it), and the context
window (a warning below 16,384 tokens), and for GitHub Copilot it checks in order
that the Copilot CLI is found (`CopilotCliLocator`, which finds the real `copilot.exe`, never an editor's shim), that
its version is 1.0.113 or later, that credentials exist, that the one shared Copilot runtime
(`CopilotRuntimeStarter`) starts on that CLI and is signed in, and that the model settings are valid for it.
`ProviderModelCatalog` lists the models of the provider for the AI Provider page. `AgentService` never writes into a
project except through
`project_write`, keeps the data of the Copilot runtime in a folder of its own and the restored history in memory, and
logs metadata only. The `Agent` subsystem depends on `Settings`, `Project`, `Payload`, `Knowledge`, `Style`, `Preview`,
`Logging`, and the AI libraries, and not on `Chat` or `AppShell`; `AppShell` builds it, and `Settings` does not depend
on it (see *AI Provider Seam*).

`KnowledgeCatalog` holds the knowledge of how to draw and how to work in a project as a tree of cards: the help of
CanvasNet, without the few cards that are only about the host program, extended with cards of CanvasPilot that are
embedded in the application and tell where files go in a project, how to make the picture the user uses, and how to read
the marks and attachments of a message. The catalog is built once, from embedded resources only, and never changes.
`KnowledgeSearch` finds the cards that fit some words, `KnowledgePromptBuilder` writes the index and the cards as text
for the prompt of an assistant within a size budget, `KnowledgeTools` describes the one tool by which an assistant
fetches them, and `KnowledgeSkills` describes each card as a skill; none of these units refers to a provider of a
language model, so the same knowledge goes to any provider: the AI copilot is given the index in its instructions and
fetches a card with the tool `canvasnet_help`, and the user reads the same cards in the browser. `KnowledgeMarkdown`
reads the Markdown of a card into blocks that a window can draw, without a Markdown
library, and `KnowledgeBrowser` is the window in which the user reads the cards: a tree, a search box that runs the same
search as the assistant, the card with its related cards and examples, and a button that copies an example. The window
is the only unit of the subsystem with a user interface. The browser does not know the main window; the shell asks an
`IKnowledgeBrowserLauncher`, which `IDialogService` extends, so the main window shows it beside itself, one at a time
and without blocking. The `Knowledge` subsystem depends on `Logging` (the log of a clipboard failure) and on CanvasNet,
and not on `AppShell`.

`StyleKitDocument` is the format of a style kit and the one place that reads and writes it: a folder with a `style.json`
file (a palette of named colors, fonts, a line width, a corner radius, a spacing, a Mermaid theme, a Vega-Lite
configuration, advice, export sizes, and captions) and an `examples` folder, read with a message for each wrong
property. `StyleKitCatalog` finds the kits in the four places and chooses the most local copy of a kit by the order
project, user, shared folders, program, and it is the only item that writes kits; the kits of the program and of a team
are read only. `StyleKitArchive` exchanges a kit as a zip file and reads one as untrusted input. `StyleSelection` says
which kit the open project uses, keeps the choice in the project file, gives a new project the default kit of the user,
and is the source of export sizes that `ImageExporter` asks (the sizes of the kit, then the sizes of the application).
`StyleGuidance` and `StyleTools` give the style to the AI copilot, as a block of text of a known size in its
instructions and as two tools, `style_get` and `style_check`, which are plain values and use no provider of a language
model. `StyleChecker` measures the colors of a picture against the palette of a kit and `StyleCheckCommand` runs it for
the Image menu and puts the result among the notes of the preview. `StylePicker` is the list in the status bar,
`StyleSettingsPage` the page of the settings dialog for shared folders and the default kit, and `StyleKitManager` and
`StyleKitEditor` the window in which the user sees, makes, edits, imports, exports, and chooses kits. The `Style`
subsystem depends on `Project`, `Settings`, `Preview`, `Knowledge` (only the descriptor record of a tool), `Utilities`,
`Logging`, and CanvasNet, and not on `AppShell`.

### Dependency Direction

The dependencies between the subsystems run one way: `AppShell` depends on `Preview`, and `Preview` does not depend on
`AppShell`. The seams belong to the provider, the lower subsystem. `Preview` owns `IImageExporter`, the contract the
Image menu uses, and `IExportDialogs`, the three platform operations that an export needs: choosing where to save,
putting a picture on the clipboard, and showing a message. `ImageExporter` asks the user only through `IExportDialogs`.
`AppShell` owns `IDialogService`, which extends `IExportDialogs` and adds the dialogs of the shell, so the main window
implements one interface and passes itself to the exporter. `MainWindowShell` holds the exporter as an `IImageExporter`,
using a disabled `NullImageExporter` when none is supplied, and `ApplicationComposition`, the composition root, builds
the render service, the preview pane, and the exporter, which is its job for every subsystem. `MainWindowView` turns the
`RenderedImage` of `Preview` into an Avalonia bitmap with `RenderedImageBitmap` when it puts it on the clipboard.
`ProjectWorkspace` still holds the preview as an untyped content object, found by the view locator.

Because the direction is fixed, a subsystem added later depends on the subsystems below it and never on `AppShell`; only
the composition root and the window refer to every subsystem. The `Chat` subsystem is the first of them: it uses
`Composer`, `Annotation`, `Payload`, `Speech`, and, for the turn of the AI copilot, `Agent` (only `IAgentService` and
the events of a turn, never a provider), and the seam by which it asks for the inspector window,
`IPayloadInspectorLauncher`, belongs to `Chat`, so `IDialogService` extends it just as it extends the seams of `Project`
and `Preview`. The `Agent` subsystem sits above `Settings`, `Project`, `Payload`, `Knowledge`, `Style`, and `Preview`
and below `Chat`: it reads what they offer (the provider choices, the open project, the message, the drawing knowledge
and style as plain tool descriptions, and the render service) and none of them refers to it, and it never refers to
`Chat` or `AppShell`. Its one seam towards a lower subsystem is the connection check of the settings dialog:
`Settings` owns `IProviderCheck` and `IProviderModelSource`, which name no type of `Agent`, and `AgentReadiness` and
`ProviderModelCatalog` implement them, just as `Composer`
owns `IMessageBuilder` for `Payload`; `Settings` therefore never refers to `Agent`, and the composition root hands the
check to the AI Provider page. The `Knowledge` subsystem follows the same rule: the seam by which the shell asks for the
browser,
`IKnowledgeBrowserLauncher`, belongs to `Knowledge`, and `IDialogService` extends it too. The `Style` subsystem sits
above `Project`, `Settings`, `Preview`, and `Knowledge` and none of them refers to it: the seams that they need are
declared by the lower subsystem (`IExportPresetSource` and `IRenderNoteSink` in `Preview`), the id of the kit of a
project is plain text in the project file, and the style settings are a plain record in the settings.
`IStyleKitDialogs`, the windows and pickers that the Style Kits window asks for, belongs to `Style`, and
`IDialogService` extends it as well. The direction is shown in the model and in the diagram above, and the verification
of `ImageExporter` uses a substitute for `IExportDialogs`, so no test of `Preview` needs the dialog service of
`AppShell`, and the verification of the chat panel and of the Help command uses a substitute for the launcher.

The same rule keeps `Composer` and `Payload` apart. The payload is built from the composer's snapshot, so `Payload`
depends on `Composer`; the composer sends the messages, so it needs them built. The seam belongs to the lower
subsystem: `Composer` owns `IMessageBuilder` and the `SentMessage` it returns, and `PayloadBuilder` implements the
interface, so `ComposerViewModel` never refers to the `Payload` subsystem. The composition root hands the payload
builder to the composer, and the sent message carries the payload as an object that only the `Payload` subsystem and the
code that shows it interpret.

### AI Provider Seam

The AI copilot answers the messages through one of two providers, chosen in the settings: a local Ollama server, which
works end to end in this release (covered by automated tests; live checks against a running server are pending), and
GitHub Copilot, which runs on the Copilot CLI that the user installed. Everything provider-specific is behind the
`Agent` subsystem; `Chat` sees only
`IAgentService` and the events of a turn, and `Settings` sees only the `IProviderCheck` and `IProviderModelSource`
interfaces that it owns.

**The seam.** The input of a turn is the `MessagePayload` that is built for every sent message (and shown in the payload
inspector) together with the `AiProviderSettings` of the settings file (provider, model id, reasoning effort, context
window, Ollama endpoint, Copilot command-line tool path, and the flag for unrestricted agent tools). `AgentService`
reads the settings through `SettingsService` and rebuilds its conversation when the project or one of the settings that
decide the agent changes (the signature holds the provider, the model, the reasoning effort, the address, the window,
and the CLI path). The agent is detached from the user interface: it takes its settings, the project folder, and the
payload, answers through events, and is tested without a window against a scripted model. The default context window is
32,768 tokens for new settings, a stored value is kept as it is, and a window below 16,384 tokens is reported as a
warning that does not block sending. The flag for unrestricted tools is not offered in the dialog: the copilot has only
the locked-down tools of the `Agent` subsystem.

**Choosing a Model.** The model is chosen from the models that the provider offers, not typed from memory.
`ProviderModelCatalog` of the `Agent` subsystem lists them for the AI Provider page through `IProviderModelSource`: for
GitHub Copilot from the catalog of the one shared runtime (the models of the signed-in account, as the AI library
reports them), for Ollama from the catalog of the server at the address of the settings (the models that are pulled,
described by `/api/show`). Every model is listed, those that cannot be used as well, with what it can do in
CanvasPilot's own terms: tool calling, pictures, reasoning (and which levels), and the context window. The page states
what a model lacks and does not decide for the user.

- *What the application needs.* Tool calling is required: every drawing is made by calling tools, so the check and the
  factory refuse a model without it (`ModelSettingsInvalid`, with the sentence of the AI library). Pictures are wanted
  and not required: a model that cannot see still reads and writes text and files, so it is a warning in the page, in
  the readiness banner (`ModelLacksVision`, usable), and in the list. The policy is in one place (`ModelRequirements`)
  so the list, the check, and the factory agree.
- *Reasoning level.* The efforts that exist are the model's (none, low, medium, high, xhigh, max, and whatever a
  provider adds), so the setting is the provider's own id as text (`ReasoningEffort`), the page offers only the efforts
  that the chosen model lists and enables the choice only then, and the AI library applies it when the agent is made:
  for Copilot as the reasoning effort of the session, and for Ollama as the thinking level of each request. The window
  of Ollama is validated against the model's trained length, so a window that cannot be honored is reported at the start
  and not by a failed request.
- *Validation at the start.* When a conversation starts (and in the readiness check), the catalog of the provider
  validates the model and the effort of the settings with `SelectAsync`; a model that the account does not have, a model
  that cannot be used, one that cannot call tools, or an effort that the model does not offer is told in the banner with
  the sentence of the library, which names what is supported, before any turn is spent. No model named means the default
  model of the runtime, and then nothing is validated.
- *Manual entry.* When the list cannot be read (the server is down, the runtime is not signed in), the page says why and
  the model name stays a text box, with the stored model and level kept as they are; Refresh models asks again. A server
  that does not describe its models at the start of a conversation leaves the typed Ollama model to be tried as it is.
- *Settings compatibility.* The reasoning effort replaces the `reasoningLevel` of earlier builds in the same settings
  file without a new version number: the old values are read into the new setting (see the design of `SettingsService`).

**Running the Installed Copilot CLI.** GitHub Copilot is reached through the Copilot CLI that the user installed, which
the AI library runs at the explicit path that `CopilotCliLocator` found (the path in the settings first). CanvasPilot
never bundles, copies, or downloads the Copilot runtime: `CopilotSkipCliDownload` is set for every project, the
installed CLI is the only source that is ever chosen (the `Bundled` source is not), and the build asserts that neither
the published output nor the installer holds a Copilot file. The readiness checks run in order: the real CLI is located
by a rule that rejects the launchers (shims) of editors, its version must be 1.0.113 or later, credentials must exist
(a signed-in GitHub CLI, or one of the variables `COPILOT_GITHUB_TOKEN`, `GH_TOKEN`, and `GITHUB_TOKEN`; a token is
never stored, shown, or logged), the shared runtime must start, and the model settings must be valid for it. The runtime
start is the authoritative check of the CLI and the sign-in, and it goes through the single seam
`ICopilotRuntimeStarter`.

- *One shared runtime.* The `CopilotRuntimeStarter` starts one runtime, and the readiness check ("Check connection"),
  the agent factory for every conversation, and the list of models all get that same running runtime. A start is
  single-flight and keyed by the CLI path and the data folder: the same key returns the running runtime, another key
  stops the old runtime and starts a new one (only one runtime may use one data folder), and a failed start is not
  remembered, so a fix is noticed by the next request. The application stops the runtime once when it ends.
- *Sign-in.* The runtime signs in from a signed-in GitHub CLI (`gh auth login`, with `gh` on the `PATH`) or from the
  variable `COPILOT_GITHUB_TOKEN`, `GH_TOKEN`, or `GITHUB_TOKEN`. CanvasPilot passes no token, has no token setting and
  no sign-in page, and never reads, stores, or logs a credential. **Limitation:** the sign-in of the Copilot CLI itself
  (`/login`) is not used, because the AI library gives the runtime a home of its own, so a user who is signed in only to
  the CLI is "not signed in for CanvasPilot" and is told the working routes. This was verified with a CLI that was
  signed in with `/login`: with the GitHub CLI off the `PATH` and no token variable the runtime started but was not
  authenticated. It is a documented limitation and a request for the AI library (see the design of the AgentKit OTS
  item), not something that the application works around.
- *Data folder.* The runtime keeps its home and its sessions in `%LOCALAPPDATA%\CanvasPilot\copilot`, chosen by the
  application and never inside a project; the factory deletes a session when its conversation is cleared.
- *Failures are sentences.* A CLI that is too old, a runtime that is not signed in, a path in the settings that the
  library refuses, and any other failed start are each a typed readiness status with one sentence that says what to do;
  none is an exception.

A later direction, user-selectable AgentKit tool packs, is possible now that the tools package of the AI library pins
the same CanvasNet line as CanvasPilot, but the package is not referenced yet; spoken replies of the copilot are
deferred.

**Where data lives.** Nothing the copilot needs is ever kept in the project folder except what the user or the copilot
is asked to create.

- *Project folder*: the files that the copilot creates or replaces with `project_write` (replaced directly, with no
  backup and no question, so the user keeps a project under source control), the `attachments` copies of sent messages,
  the `styles` folder, the project file, and, in the hidden `.canvaspilot` folder, only the pictures that the user
  pasted into a message that is not sent yet.
- *Chat history*: `%LOCALAPPDATA%\CanvasPilot\history\{key}`, where the key is a hash of the full path of the project
  file; the words of the conversation only, no pictures and no payload; a project that is moved or renamed starts with
  no history; the command "Clear chat history" deletes it.
- *Copilot runtime data*: `%LOCALAPPDATA%\CanvasPilot\copilot`, chosen by `AgentService` and handed to the one
  `CopilotRuntimeStarter` by the composition; its sessions are deleted when
  a conversation ends.
- *Log*: `%LOCALAPPDATA%\CanvasPilot\logs`; the copilot writes only metadata (provider, model, tool names, sizes,
  status, durations), never a message, an answer, a picture, a token, or the content of a file.
- *Settings*: `%APPDATA%\CanvasPilot\settings.json`; it never holds a credential.
- *Memory only*: the digest of the restored history that is given to a new provider session, and the pictures that
  `render_image` draws.

A project that contains the folder of the chat history or of the Copilot data (a project in the profile folder of the
user, for example) is refused, so that a configuration mistake cannot write into a project.

## External Interfaces

**Log Files**: Local diagnostic output written by the application.

- *Direction*: Output.
- *Type*: Text files on the local file system.
- *Format*: One timestamped, leveled entry per line (exception details follow on additional lines),
  UTF-8, in `canvaspilot.log` plus timestamped archive files in the log folder.
- *Constraints*: The default location is `%LOCALAPPDATA%\CanvasPilot\logs`. Files are written locally
  only; no telemetry or remote transmission is performed. Disk usage is bounded by the rotation size
  and the retained archive count.

**Settings File**: The user's persisted settings.

- *Direction*: Input and output.
- *Type*: JSON file on the local file system.
- *Format*: One UTF-8 `settings.json` document with a `version`, the recent projects, the AI provider
  choices, the speech choices, and the style choices (the shared kit folders and the default kit). Unknown properties
  are ignored.
- *Constraints*: The default location is `%APPDATA%\CanvasPilot\settings.json`. The file is written
  atomically. A corrupt file is renamed `settings.json.bad` and defaults are used. The file never
  contains credentials.

**Settings Dialog**: The window in which the user edits settings.

- *Direction*: Input and output.
- *Type*: Avalonia modal window.
- *Format*: A page list with OK, Cancel, and Apply buttons.
- *Constraints*: Changes take effect only on OK or Apply, and invalid entries cannot be applied.

**Project Folder and Project File**: The user's projects.

- *Direction*: Input and output.
- *Type*: A folder on the local file system holding a `{name}.canvaspilot` JSON file, a hidden `.canvaspilot`
  folder, the image source files, and optionally a `styles` folder of style kits.
- *Format*: The project file holds `formatVersion` (1), `name`, `createdUtc`, an optional `modifiedUtc`, and an optional
  `activeStyleKit` (the id of the style kit that the project uses; absent for a project with no style); unknown
  properties are ignored when reading and kept when saving.
- *Constraints*: A project is created only in a new or empty folder. A project file with a newer
  `formatVersion` is refused with a message. Project and file names follow rules that hold on every supported
  platform.

**New Project Dialog and Project Explorer**: The windows and controls in which the user creates a project and
works with its files.

- *Direction*: Input and output.
- *Type*: Avalonia modal window and list control.
- *Format*: A name and location entry with OK and Cancel; a list of the image source file names of the open
  project.
- *Constraints*: OK is enabled only for a project that can be created. The list shows only image source files in
  the project root and follows file changes.

**Image Preview and Export**: The picture of the selected file, and the files and clipboard it is exported to.

- *Direction*: Input (the selected image source file and the files it uses) and output (the picture, a PNG file,
  and a clipboard image).
- *Type*: Avalonia control in the main window, files on the local file system, and the system clipboard.
- *Format*: The picture is drawn by CanvasNet at the file's natural size. An export as drawn is a PNG file with the same
  pixels, including transparency; an export at a size is the file drawn again at that size (fitted into a width and a
  height, or scaled to one of them, keeping its ratio) over the background of the size or transparent, with the
  resolution of the size written into the file; a copy is a bitmap on the clipboard.
- *Constraints*: The drawn file may read other files only from inside the project folder. A file that cannot be
  drawn is explained with its error code, message, and hint, never an unhandled failure. Export and copy are
  enabled only while a picture is shown.

**Picture Drag-out**: The shown picture, dragged out of the preview into another program.

- *Direction*: Output.
- *Type*: The operating system's drag and drop, started from the preview in the main window, and PNG files in a
  folder named `drag` in the application's folder of the per-user local application data folder.
- *Format*: One drag carries one item that offers two formats: a file (a PNG named after the source file and the
  first eight characters of the picture's pixel fingerprint, for example `flow-1a2b3c4d.png`) and a bitmap of the
  same picture. The allowed effect is copy.
- *Constraints*: A drag starts only with the left button, after the pointer has moved four pixels, while a picture is
  shown and dragging out is not turned off. The application writes only files of that name pattern in its folder,
  deletes the files of earlier runs when it starts and its own files when it ends, and never changes the file
  the user dropped. A drop target that refuses the picture, or a file that cannot be written, is logged and never an
  unhandled failure. Whether a particular program accepts the file, the bitmap, or both is checked by hand (see the
  verification of `ImageDragSource`).

**Dropped and Pasted Content and Attachments**: What the user drops or pastes into the message, and the copies that are
made in the project when the message is sent.

- *Direction*: Input from the operating system's drag and drop and clipboard; output to files in the project folder.
- *Type*: Paths of files and folders, PNG bytes of pictures, and plain text, read by the view and handed over as one
  `DroppedContent`; files in the project's `attachments` folder and in `.canvaspilot/attachments`.
- *Format*: A dropped path becomes a chip that refers to it. A pasted picture is saved as
  `paste-{yyyyMMdd-HHmmss}-{n}.png` in `.canvaspilot/attachments` while the message is a draft. When the message is sent
  a file or folder from outside the project is copied to `attachments/{name}`, with `{name} (2)` and so on when a name
  is taken, a pasted picture is moved there, and a thing that is in the project already is referred to by its
  project-relative path with forward slashes.
- *Constraints*: Everything dropped is data, never opened or run. Paths that are not complete, that have `..`, device
  names, alternate data streams, or the device namespace are left out with a reason. One drop accepts at most 200 items,
  and one message copies at most 500 files, 500 folders, and 200 MB; a pasted picture is at most 50 MB. Nothing is
  copied while the message is a draft, nothing is overwritten, links are not followed, and a failed copy leaves nothing
  behind. The project explorer lists only image source files in the project root, so neither folder is listed.

**Drawing Knowledge**: The cards that tell how to draw with the formats CanvasNet draws and how to work in a project.

- *Direction*: Input (the cards that are embedded in the application and in the CanvasNet library) and output (the
  window in which the user reads them, and text copied to the clipboard).
- *Type*: Embedded Markdown resources, read once into an in-memory tree; an Avalonia window opened by Help > Drawing
  Knowledge; the system clipboard.
- *Format*: Each card has an identifier that is its path in the tree, a title, a one-line summary, tags, other names, a
  Markdown body, related cards, and examples with their text. The AI copilot is given the same
  cards as an index in its instructions and, on demand, through one tool.
- *Constraints*: Nothing is written to a file or sent anywhere. The few cards that are only about the host program
  are left out. A card that cannot be read, or a clipboard that cannot be reached, is a message, never an
  unhandled failure.

**Style Kits**: The folders that describe how pictures should look, and the zip files that carry them.

- *Direction*: Input (kits from four places and from zip files) and output (kits the user makes, copies, edits, or
  exports).
- *Type*: Folders on the local file system, `.zip` files, an Avalonia window opened by Tools > Style Kits, the list in
  the status bar, and the Style Kits page of the settings dialog.
- *Format*: A kit is a folder `{id}` with a `style.json` file (`formatVersion` 1) and an `examples` folder. The places
  are, from the winning one down, the `styles` folder of the open project, `%APPDATA%\CanvasPilot\styles`, the shared
  folders listed in the settings (earlier ones first), and the `StyleKits` folder beside the executable that comes with
  the program (`clean`, `slides-dark`, and `dema`). A zip file holds `style.json` and `examples/` at its root, or in one
  wrapper folder.
- *Constraints*: Every kit is bounded (a style file is at most 256 KiB, an example file at most 16 MiB, at most 24
  examples) and is read with a message for each wrong property. The kits of the program and of shared folders are never
  written. An import writes only inside the chosen place, refuses an archive as a whole when a name could leave it, and
  counts the bytes it writes (at most 100 entries, 16 MiB for an entry, and 64 MiB in all). A shared folder that cannot
  be reached is reported and never blocks the window.

**AI Providers**: The Ollama server that answers the messages, and the GitHub Copilot CLI that the application runs.

- *Direction*: Input and output for Ollama (the messages and pictures of the conversation go out, the streamed answers
  and tool calls come back); input and output for the Copilot CLI (the messages and tool calls go out through the AI
  library, the streamed answers come back).
- *Type*: HTTP to the Ollama endpoint of the settings (the local address by default), through OllamaSharp; a short
  `--no-auto-update --version` run of the installed `copilot.exe` and, for the credential check, of `gh auth token`,
  whose output is never kept; and the installed `copilot.exe` run as a child process of the AI library, which speaks to
  it over standard input and output.
- *Format*: One agent conversation per open project; the context window of the settings is sent with every Ollama
  request. For Copilot, the located path, the version, and the readiness sentence, and a conversation on the one shared
  runtime.
- *Constraints*: The server is asked only for its model list when the provider is checked, and receives a message only
  when the user sends one; the endpoint is the one the user typed. The Copilot runtime is started only when the provider
  is checked or a message is sent, on the CLI that the user installed, and the application never includes or downloads
  it (see *AI Provider Seam*). Every failure, an unreachable server, a missing model, a runtime that is not signed
  in, or a refused tool, is a sentence that names the remedy, never an unhandled failure.

**Chat History and Copilot Data**: What the AI copilot keeps on the user's computer.

- *Direction*: Input and output.
- *Type*: JSON files and folders below `%LOCALAPPDATA%\CanvasPilot`.
- *Format*: `history\{key}\history.json` (version 1, at most 200 rows and 2 MB) with a `project.json` that names the
  project, where the key is 32 hexadecimal characters of a hash of the project file path; the text of the conversation
  only. `copilot\` is the home and the sessions of the Copilot runtime.
- *Constraints*: Never inside a project folder; written atomically; a corrupt or newer file is set aside as `.bad`; the
  history is deleted by "Clear chat history"; a moved or renamed project starts with no history; no picture, payload,
  or credential is saved.

**Speech Settings, Audio Devices, and Model Store**: The speech choices, the audio system, and the downloaded models.

- *Direction*: Input and output.
- *Type*: The speech section of the settings file; the operating system's audio system, through the
  DemaConsulting.Speech library; the library's default model store on the local file system; and HTTPS downloads of
  model files.
- *Format*: Devices are stored by name, models by identifier, and each model's parameter values as
  invariant-culture strings, in the `speech` section of `settings.json`. The model store is
  `%LOCALAPPDATA%\DemaConsulting.Speech\Models` on Windows and is shared with other DEMA Consulting applications.
- *Constraints*: The audio system and the model store are used only when the Speech page is opened or dictation is used.
  Downloads start only when the user asks, verify checksums before installing, and are cancelled when the settings
  dialog closes. A machine without audio hardware, a blocked network, or an unreadable store is explained on the page
  and never causes a failure.

**Microphone and Speech Engine**: What dictation listens through and recognizes with.

- *Direction*: Input.
- *Type*: The operating system's audio input, through the audio library of the DemaConsulting.Speech library, and the
  native speech inference runtime and Sherpa interface that arrive with the library's packages.
- *Format*: Mono audio at the sample rate of the speech model, converted by the audio system from the format of the
  hardware; the output is text.
- *Constraints*: The microphone is open only while the talk control is held and a short moment after it is released, and
  is released when the session ends. The audio is never stored, and neither is the recognized text, except in the
  message that the user is composing; the application log holds events but never words. The native runtime files must be
  beside the application, and the speech engine is not loaded unless they are. Windows can block desktop applications
  from using the microphone; dictation then says so.

**Windows Installer Package**: The way the application is delivered.

- *Direction*: Output (built by the release pipeline) and input (to the user's Windows Installer).
- *Type*: One per-machine, 64-bit MSI file named `CanvasPilot-{version}-win-x64.msi`, built by the WiX Toolset from the
  installer project, which is not part of the solution.
- *Format*: The package carries the self-contained single-file `CanvasPilot.exe` publish output, the native libraries
  that stay beside it (the Skia, text shaping, and ANGLE graphics libraries and the speech inference runtime, Sherpa
  interface, and audio library), the `StyleKits` folder of the three style kits that come with the program, a Start Menu
  shortcut, and the Add/Remove Programs icon and links; its version is the numeric part of the build version.
- *Constraints*: Installing a package replaces any installed version, older or newer, and asks a running
  `CanvasPilot.exe` to close first. The package contains no user data, and uninstalling leaves the user's settings,
  logs, and projects in place. The package contains no GitHub Copilot runtime: the application never ships or
  downloads one, which the build asserts on the published folder and on the file table of the package.

**Main Window**: The application's window and menus.

- *Direction*: Input and output.
- *Type*: Avalonia window with File, Edit, Image, Tools, and Help menus, a project explorer, a preview area with a
  layer for marks, a chat column with the message composer and the transcript, a status bar with the style kit of the
  project, and modal dialogs (New Project, Settings, About, payload inspector, export size, style kit editor, and
  message, confirmation, and prompt dialogs) and, from the Help and Tools menus, the windows of the drawing knowledge
  and of the style kits, which do not block the main window.
- *Format*: The window title is `{project name} - CanvasPilot` while a project is open, otherwise `CanvasPilot`.
  Every menu item has a name and an automation identifier that are the same. The F8 key is the talk key: dictation
  listens while it is held.
- *Constraints*: Menu items are enabled only when their command can run; Undo and Redo act on the part of the window
  that has the focus and are disabled when that part has nothing to undo. The chat column is shown only while a project
  is open. Problems are shown in a dialog, never as an unhandled failure.

## Dependencies

The `Agent`, `Annotation`, `AppShell`, `Chat`, `Composer`, `Knowledge`, `Logging`, `Payload`, `Preview`, `Project`,
`Settings`, `Speech`, `Style`, and `Utilities` subsystems use .NET base class library types, `System.Text.Json` for the
settings and project files and the description of a payload, and, for dialogs, pages, and lists, Avalonia and
CommunityToolkit.Mvvm, which are runtime third-party items (the `Composer` editor view also uses the AvaloniaEdit
editing control, which is a runtime third-party item too; the other `Annotation`, `Chat`, and `Composer` units use base
class library types and Avalonia only, and the `Payload` units base class library types and CanvasNet; of the
`Knowledge` units only the browser uses Avalonia and CommunityToolkit.Mvvm, and the catalog reads the help cards of
CanvasNet; the `Style` units read and write their files with `System.Text.Json` and `System.IO.Compression`,
`StyleKitDocument` classifies font families with CanvasNet, and the picker, page, manager, and editor use Avalonia and
CommunityToolkit.Mvvm). The `ImageRenderService` unit draws files and encodes PNG with the
`DemaConsulting.CanvasNet.Viewer` library, the `MarkImageComposer` unit draws the marks of a message over a picture with
the same library, and the `ProjectExplorer` unit uses its extension list to decide which files are image sources. The
`AudioDeviceService`, `SpeechModelService`, and `DictationService` units list audio devices and speech models, download
models, create capture devices, and load and run the speech engine with the `DemaConsulting.Speech` library and its
Sherpa model package, whose native speech runtime is shipped with the application, and the speech descriptor types are
shown by the other units of the `Speech` subsystem; both libraries are documented as OTS items (see *OTS Dependencies*).
The units of the `Agent` subsystem use the AI libraries (AgentKit, which brings the path policy and the tool framework,
the Microsoft Agent Framework, Microsoft.Extensions.AI, and OllamaSharp, and the Copilot SDK, with which AgentKit talks
to a Copilot CLI), which are documented as OTS items too; the Copilot runtime that the Copilot SDK would bundle is
switched off for every project by `CopilotSkipCliDownload` in `Directory.Build.props` and is never shipped or
downloaded. Build, documentation, and quality tools are documented in the OTS design documents. The Windows installer
package is built from the published application by the WiX Toolset, also documented as an OTS item; no run-time item
depends on it.

## Risk Control Measures

**Path confinement**: Every file path built from a caller-supplied name passes through
`PathHelpers.SafePathCombine`, which rejects traversal sequences and absolute paths. `RollingFileLogger`
applies this to its configurable base file name, so logging cannot create or overwrite files outside the
log folder. `SettingsService` applies it to the settings and temporary file names, and `ProjectService`
and `ProjectExplorer` apply it to project folders, project files, and renamed files. Segregation of the
checking logic into the `Utilities` subsystem lets it be reviewed and verified once for every consumer.

**Bounded diagnostics**: Log rotation caps the disk space consumed by diagnostic output, so a failure
that logs repeatedly cannot exhaust the user's disk.

**Safe settings handling**: A save writes a temporary file and then replaces the settings file, so a failure
cannot leave a truncated file. A corrupt or newer file is tolerated with defaults and a log entry, so a
damaged file cannot prevent startup. Values are validated when read and when entered, so an out-of-range
value cannot reach the rest of the application.

**Safe agent defaults**: The copilot has only a fixed, locked-down set of tools. The flag for unrestricted agent tools
is stored as off by default and is not offered in the settings dialog, and no setting widens the tools of the copilot.

**Confined copilot**: The tools of the copilot (`canvasnet_help`, `style_get`, `style_check`, `project_list`,
`project_read`, `project_write`, and `render_image`) are the only things it can call; a tool with the name of a built-in
tool of the Copilot CLI is refused when the set is built, and the permission handler denies by default. Every path goes
through the path policy of the project, which denies the `.canvaspilot` folder and the project file, and through a guard
of `ProjectTools` that also refuses links (the policy of the AI library does not follow them), stream and device names,
and short names. Writes are limited to text image sources and `.csv`, `.json`, and `.txt` files of at most 256 KiB,
never below `styles`. A write replaces a file directly, with no backup and no question, by the decision of the product;
the result says `created` or `replaced`, the chat shows it, the explorer selects the file, the role prompt tells the
copilot to read a file before it replaces it, and the user guide tells the user to keep projects under source control.

**Safe agent data**: What the copilot keeps for itself stays outside the project (see *AI Provider Seam*): the history
of the chat and the data of the Copilot runtime are below `%LOCALAPPDATA%\CanvasPilot`, a project that contains them is
refused, the restored history is given to a new provider session from memory only, and the log of the copilot holds
metadata only, never text, pictures, tokens, or file content. A token that the readiness check finds is never stored,
shown, or logged, and the settings file holds no credential.

**No Copilot runtime shipped**: The application never bundles, copies, or downloads the GitHub Copilot runtime. The
build asserts that the published output and the file table of the installer hold no Copilot file, so a change of a
package or a build property cannot add one unseen.

**Failures are results**: A provider that is missing, not signed in, too old, or out of reach is a readiness report, and
a
failed turn is a `Failed` event with a sentence that names the remedy; neither reaches the user as an unhandled failure.

**Safe project handling**: A project is never created in a folder that already has content, and a failed or
cancelled creation removes what it added. Project files are saved by writing a temporary file and replacing
the project file, and a file from a newer version is refused rather than rewritten, so it cannot be
damaged. Renaming a file never replaces an existing file or folder, and deleting is left to the caller to
confirm with the user. Names are checked against rules that reject separators, traversal, hidden names, and
reserved device names.

**Confined drawing**: The files that a drawn file references (embedded pictures, model includes, chart data) are read
only from inside the project folder, through CanvasNet's `UnderRoots` file access policy, so an image source file
cannot pull unrelated files of the user's computer into a picture that is exported or shared. A denied file is
reported as a note. Drawing runs in the background and can be cancelled, so a slow or hostile file cannot block the
window.

**Dropped content is data**: Files, folders, pictures, and text that the user drops or pastes may come from the internet
or from mail and may be programs, scripts, or names built to reach devices, other drives, or other files.
`AttachmentIntake` only asks the file system what a path is and how large a file is, which does not open it, so a
program or script becomes a chip like any other file and nothing is run, rendered, or followed; it leaves out, with a
reason, every path that is not a complete path or that has a `.` or `..` part, a device name, an alternate data stream,
a trailing dot or space, or the device namespace, and a whole drive. Pasted pictures are saved only after the PNG
signature and a limit of 50 MB are checked, and are never decoded. Nothing is copied while the message is a draft.

**Confined attachment copies**: When the message is sent, `AttachmentStore` is the only item that creates files for
attachments, and it creates them only in the project's `attachments` folder and in `.canvaspilot/attachments`. Every
path is built with `PathHelpers.SafePathCombine` from names that were made safe (characters a name cannot hold replaced,
device names changed, trailing dots and spaces removed, length limited), files are created exclusively and never replace
another file, symbolic links and junctions in a dropped folder are not followed, a folder that holds the project is
refused so that it cannot be copied into itself, the project's private data folder cannot be attached except for the
pictures the store saved, and the copy is bounded by 500 files, 500 folders, and 200 MB for one message, checked before
the copy and again during it. A failed copy removes everything it created. The originals are never changed or deleted.

**Bounded drag files**: The files written for dragging a picture out are named from the source file's name with
characters that a file name cannot hold replaced and the length limited, and are built with
`PathHelpers.SafePathCombine`, so a file name cannot lead outside the drag folder. The unit deletes only files that
match its own name pattern, never folders or other files, so cleaning up cannot remove the user's files.

**Clean message text**: Text that enters the message being composed, whether typed, pasted, dropped, or recognized from
speech, is data from outside the application. `ComposerDocument` removes from it the character that stands for a chip
and every control character other than tab and line feed, so that text can never be taken for a chip or confuse the
editing control or the tools that read the message. The draft and its undo steps are kept in memory only, are never
written to disk, and are cleared when the message is sent.

**Safe speech handling**: Downloads of speech models start only when the user asks, run over HTTPS, and are verified
against checksums by the speech library before a model is installed completely or not at all, so a damaged or
tampered download is discarded. A download never outlives the settings dialog that shows it. A choice that the page
cannot show, such as a device that is not connected or a model while the model store cannot be read, is kept in the
settings instead of being erased, and failures of the audio system and the model store are results, never
unhandled exceptions.

**Safe speech runtime**: Starting the speech engine without its native runtime beside the application does not fail
cleanly: Windows then loads an older inference runtime from its system folder and the process crashes inside the native
library, which cannot be caught and would lose the message that the user has not sent. `DictationService` is therefore
the only item that loads an engine, it checks that the runtime files exist before it does, and it reports that the
speech runtime is missing instead when they do not. The files are in the published output, which the installer harvests,
and the build verifies both the published output and the file table of the installer package. Dictation also listens
only while the talk control is held, releases the microphone when it ends, keeps neither audio nor recognized text
outside the message, and writes events but never words to the log.

**Safe style kits**: A style kit may come from a shared folder, a download, or a zip file in the mail, so it is read as
untrusted input. `StyleKitDocument` bounds every part before anything is made from it (file size, depth, number and
length of entries, example captions) and reports each wrong value with its property; `StyleKitArchive` refuses a whole
archive, before it writes anything, when a name could leave the target folder (parent parts, drive letters, rooted
names, links, characters a name cannot hold) or when a file is not part of a kit, counts the bytes that it really
writes, so a header that lies about a size does not help, and puts a kit in place only when it is valid. A shared folder
is read on a worker with a time limit, so one that does not answer cannot stop the window; the kits of the program and
of a shared folder are never written. The example images of a kit are drawn with only the folder of the kit readable,
`style_check` reads only a file inside the project (not through a link that leads out), and the block of guidance says
that the text of a kit is data and not instructions, so a kit cannot instruct an assistant, and its size is capped.

Application subsystems call `RollingFileLogger.Log` with a severity, a message, and optional exception
context. `RollingFileLogger` asks `PathHelpers` to resolve the active file path inside the log folder,
rotates and prunes archives when the active file has reached its size threshold, and appends the
formatted entry to the active file. Nothing flows back to the caller except a propagated `IOException`
when an entry cannot be written.

At startup the application loads the settings through `SettingsService`, which reads and normalizes
`settings.json`. To change settings, the application shows `SettingsDialog` with the pages to host. Each
page loads its working state from the settings in effect; on OK or Apply the dialog merges every page into the
latest settings and calls `SettingsService.Save`, which writes the file atomically and raises `Changed`.

To create a project, the application shows `NewProjectDialog`, which validates the entries through
`ProjectService`; when the dialog was accepted, the application calls `ProjectService.CreateAsync`, which
creates the project folder, the project file, and the data folder. To open a project, the application calls
`ProjectService.OpenAsync`. Either way, the project file is added to `RecentProjects`, which stores the list
through `SettingsService`, and the project folder is opened in `ProjectExplorer`, which lists its image source
files, watches the folder, and refreshes the list after file changes.

At startup `App` asks `ApplicationComposition` to build the units: the logger, the settings service (whose settings are
loaded at once), the project service, the recent projects list, the project explorer, the render service, the image drag
source (which deletes the files of earlier drags), the preview pane, the workspace, the image exporter, the speech
services with the dictation service, the shell, the mark registry, the mark image composer and payload builder, the
composer (with its attachment store and intake), the annotation overlay, and the chat panel, which it places in the
workspace and makes the target of the talk key. It also builds the style units: the catalog of style kits, the selection
of the project, the list for the status bar, the command of the Image menu, the two tools of an assistant (which it
gives to the registry of the tools of the copilot), and the source of export sizes that it gives the exporter; the kits
are read on a worker, so the start never waits for them. When it is given the folder for the local data of the
application (the running application gives the local application data folder) it builds the AI copilot and the chat
history over it, gives them to the chat panel, and joins the copilot to the explorer, the settings, and the shell as the
`AppShell` design describes. It also gives the shell a function that gives the catalog of the drawing knowledge, which
reads the cards the first time Help > Drawing Knowledge is used and not at startup. The `MainWindowView` is created over
the shell and gives itself to the shell as the dialog service. From then on the user's menu choices become shell
commands, which call the `Project` and `Settings` subsystems, show dialogs through the dialog service, and, when the
open project changes, update the window title and open or close the project in the workspace. On exit `App` disposes the
composition.

When the user selects a file in `ProjectExplorer`, `PreviewPane` cancels any render that is running and asks
`ImageRenderService` to draw the file in the background; the result, a picture with notes or an error with its code and
hint, is shown unless a newer render was started meanwhile. When files in the project change, the pane renders again
after a short delay. The Image menu runs `ImageExporter`, which asks the user which size to export at
(`ExportPresetDialog`, with the sizes that `StyleSelection` offers) and then either encodes the shown picture as PNG or
draws the file again at the chosen size with `ImageRenderService` and encodes that, and writes it where the user
chooses, or hands the picture to the window to put on the clipboard. When the user chooses Check Against Style,
`StyleCheckCommand` takes the shown picture, has `StyleChecker` measure it against the palette of the kit of the project
on a worker thread, and gives the notes of the result to `PreviewPane`, which shows them with the other notes until the
picture is drawn again. When the user chooses Tools > Style Kits, the shell makes a `StyleKitManagerViewModel` over the
catalog and gives it to the dialog service, which shows the window of the manager; the manager changes kits only through
`StyleKitCatalog` and `StyleKitArchive`, and the choice of the kit of the project through `StyleSelection`. When the
user presses the left button on the shown picture and moves the pointer, the preview view asks `PreviewPane` for the
picture as a file; `ImageDragSource` writes the PNG in its folder, and the view starts the operating system's drag with
that file and a bitmap of the picture.

The message being composed is held by `ComposerDocument`, and every change the user makes to it, typing, dictating,
dropping, pasting, adding a mark, and deleting, is made as one `ComposerEdit` that `ComposerHistory.Execute` applies to
the document and records. Undo and redo revert and apply whole edits and return the caret to restore. The editing
control does not make edits itself: it reports what the user did to `ComposerViewModel`, whose methods
(`ApplyTextChange`, `AddDictatedBlock`, `HandleDrop`, `HandlePaste`, `AddMark`, `DeleteChip`) each make exactly one edit
and tell the control once that the message changed. When the message is sent, the history hands over a snapshot of the
document, from which the message is built, and empties the document and the history.

When the user draws a mark on the shown picture, the layer over the picture gives `MarkRegistry.Add` the picture, the
shape, and the points of the pointer in the picture's pixels, once the shape is finished and its optional note is
entered or skipped. The registry puts the points in normal form inside the picture, numbers the mark, and returns it;
the overlay reports the mark, and the chat panel adds its chip to the message as one `ComposerEdit.AddMark`, so
that undoing it removes the chip and hides the mark. Each time the composer changes, the panel tells the overlay which
mark chips the message holds, and the overlay asks `MarkRegistry.VisibleMarks` with those identities and the key of the
picture to show a picture. To describe a mark to the assistant, `MarkSourceResolver.Resolve` reads the element positions
or anchors of the mark's picture and returns the parts of the source file under the mark, or that the picture cannot
tell.

When the message is sent, `ComposerViewModel.SendAsync` takes a snapshot of the document, puts the attachment chips of
the snapshot into the project with `AttachmentStore.StageForSend` on a thread-pool thread, and gives the snapshot, the
marks whose chips are in it, and the staged attachments to `IMessageBuilder.BuildAsync`, which `PayloadBuilder`
implements. While this goes on the draft cannot be changed. Only when both steps worked does it empty the draft with
`ComposerHistory.CompleteSend`, forget the marks with `MarkRegistry.Clear`, remove the saved pictures that no chip
refers to, and raise `MessageSent` with the immutable `SentMessage`; when a step fails, the draft, the history, and the
marks are left as they were and the status text says why. The builder walks the snapshot once, writing the text with
its references and the parts of the message in order, and keeps the marks whose chips are in it. It groups those marks
by picture, has `MarkImageComposer` draw the marks of each picture and cut the area around each mark, asks
`MarkSourceResolver` for the parts of the source under each mark, and returns a `MessagePayload` that nothing can
change. `PayloadDescriber.Describe` writes the payload as text for the user.

When the user drops files or folders on the message, or pastes a picture or text into it, the view reads the drop or the
clipboard into a `DroppedContent` and gives it to `AttachmentIntake.Accept`. The intake checks the text of each path,
asks the file system whether it is a file or a folder, saves pasted pictures through `AttachmentStore.SavePastedImage`,
and returns chips and text, with notes for what it left out; `ComposerViewModel.HandleDrop` or `HandlePaste` inserts
them as one `ComposerEdit.Drop` and names what was left out in its status text. Nothing is copied. When the message is
sent, the chips of the snapshot are given to `AttachmentStore.StageForSend`, which plans the copies against the limits,
copies the attachments from outside the project into the project's `attachments` folder, moves pasted pictures there,
and returns for each chip its path in the project, or the chip that prevents the message from being sent and why. After
sending, when a project is closed, and when a project is opened, `RemoveLeftovers` deletes the pasted pictures that no
chip refers to. When the user opens Settings and chooses the Speech page, `SpeechSettingsPage` loads: `DeviceSelection`
asks `AudioDeviceService` for the devices and selects the stored ones by name, `ModelCatalogPanel` asks
`SpeechModelService` for the models and selects the stored ones by identifier, and `ModelParameterEditor` shows the
parameters of the chosen models with their stored values. A download requested in the panel goes through
`SpeechModelService` to the speech library, which reports progress to the row and ends as installed, canceled, or
failed. On OK or Apply the page merges the choices into the speech section of the settings, which `SettingsService.Save`
writes. When the dialog closes the page cancels downloads that are still running.

When the user holds a talk control, the caller asks `ComposerViewModel.BeginPushToTalkAsync` to begin, which asks
`DictationService.BeginAsync`; the composer shows why when there is no session, and the words as they are recognized
when there is, moving them to the thread of the caller. The service reads the speech settings, asks
`SpeechModelService.Enumerate` whether the chosen speech-to-text model is installed, checks the native speech files,
and, the first time, has `SpeechModelService.LoadRecognizerAsync` load the engine with the stored parameter values
converted to their types, and keeps it. It asks `AudioDeviceService.CreateCaptureDevice` for the chosen microphone,
creates a recognition session over the engine and the device, starts reading its results, and starts the session. Each
provisional result replaces the sentence being recognized and each final result ends it, and the words so far are
available as `PartialText`. When the user lets go the caller ends the session through
`ComposerViewModel.EndPushToTalkAsync`: it keeps listening for a short moment, stops the recognition, which finishes the
audio already received, releases the session and the microphone, and returns the final text, which
`ComposerEdit.Dictate` adds to the message at the caret as one dictated block. If dictation cannot start, the same call
returns the reason and the message, and nothing is loaded that could crash the process.

In the chat panel the talk button does this with the pointer: `ChatPanelView` starts `BeginPushToTalkAsync` when the
pointer is pressed on the button and captures the pointer, and ends it with `EndPushToTalkAsync` when the pointer is
released, wherever it is, when the capture is lost, when the button loses the focus, or when the panel leaves the
window, so that the microphone is never left open. When the composer raises `MessageSent`, `ChatPanelViewModel` appends
a `TranscriptEntryViewModel` for the immutable message, whose chips get their pictures from the cut-outs that the
payload already holds and from the copies of attached pictures in the project. "View payload" makes a
`PayloadInspectorViewModel` for the payload of the entry, which writes the description with `PayloadDescriber.Describe`
and lists the payload's pictures, and gives it to the `IPayloadInspectorLauncher`, which the main window implements by
showing `PayloadInspectorView` as a dialog.

When the user chooses Help > Drawing Knowledge, `MainWindowShell` asks the function it was given for the catalog, which
builds `KnowledgeCatalog.Default` on its first use, makes a `KnowledgeBrowserViewModel` over the catalog and a
`KnowledgeSearch` made from it, and gives it to the `IKnowledgeBrowserLauncher`, which the main window implements by
showing `KnowledgeBrowserView` as a window of its own; a second request brings that window to the front. The view model
builds the tree of nodes from the roots of the catalog. A card is shown the same way whether the user chose it in the
tree, chose a hit of the search, or followed a link: `Select` opens the cards above it, selects it, and reads its text
with `KnowledgeMarkdown.Parse`, its cards below and related cards with the catalog, and its examples. Two or more
letters in the search box call `KnowledgeSearch.Search` and show its hits in place of the tree. Copy gives the text of
an example to the clipboard that the window supplies and says in the status line whether it worked.

When a message has been sent, `ChatPanelViewModel` hands the `SentMessage` to `ChatTurnRunner`, which asks
`IAgentService.RunTurnAsync` for the turn with the payload and the saved words of the conversation. On the first turn
`AgentService` asks `AgentReadiness` whether the provider can be used (when it cannot, the turn is one `Failed` event
with the sentence of the report, which for GitHub Copilot without credentials is the sign-in sentence), checks that the
project
does not contain the data folders of the application, and builds the tools of the project, the instructions, the agent,
and one provider session. `PayloadMessageMapper` turns the payload into the message of the model, the agent streams its
updates, and `AgentEventMapper` turns them into events that the runner shows as the streamed answer, one row for each
tool call, and notices; a file that `project_write` creates or replaces also raises `FileWritten`, which the shell turns
into a refresh and a selection of the file in the explorer. Stop cancels the token of the turn, which ends the stream
without a failure and keeps what was written. After a completed, stopped, or failed turn the panel saves the words of
the conversation with `ChatHistoryStore`; "Clear chat history" deletes them, empties the transcript, and has the service
forget the conversation and delete the session of the provider.

## Design Constraints

- Log files shall be written only to local disk; no telemetry or remote transmission is performed.
- The default log folder shall be under the per-user local application data folder so that no
  administrator rights are required to write logs.
- All file paths built from caller-supplied names shall be resolved with `PathHelpers.SafePathCombine`.
- Logging shall be safe to call from multiple threads.
- The settings file shall be under the per-user application data folder, written atomically, and tolerant
  of missing, corrupt, and newer content.
- Settings pages shall be supplied to the settings dialog, never referenced by it.
- The settings subsystem shall hold no reference to any AI provider SDK or client.
- The project file shall carry a format version, and a file with a newer version shall never be rewritten.
- The `.canvaspilot` folder of a project is for application data; the project explorer shall never list its content,
  and only `AttachmentStore` shall write there, only the pictures that the user pastes into a message
  (`.canvaspilot/attachments`).
- The project subsystem shall not depend on the main window; the folder picker and refresh scheduler are
  supplied by the application.
- View models shall talk to the user only through `IDialogService`, which the main window implements, so no
  view model references a window.
- Every control that a test or assistive technology must find shall carry an automation identifier.
- The image preview plugs into the shell through `IImageExporter` and `ProjectWorkspaceViewModel.Preview`
  and does not require changes to the shell's commands. `Preview` shall not depend on `AppShell`: it owns the
  exporter seam `IImageExporter` and the dialog seam `IExportDialogs`, and `AppShell` refers to `Preview` only for
  these interfaces, the picture type, and the composition root (see *Dependency Direction*).
- Only `ImageRenderService` shall call the CanvasNet viewer to draw files, and it shall confine the files a drawn file
  reads to the project folder. `MarkImageComposer` shall draw only an SVG document that it made itself, from a stream,
  which names no file, and shall write no file.
- Rendering shall never run on the user interface thread, and the result of a render that is no longer wanted shall
  never be shown.
- Only `ImageDragSource` shall write the files for dragging a picture out, only in its own folder, and a failure to
  prepare or to drag shall be logged and shall never reach the user as an unhandled failure.
- The application's units shall be built only in `ApplicationComposition`.
- Every change that the user makes to the message being composed shall be one `ComposerEdit` executed through
  `ComposerHistory`, so that the message and the undo steps never disagree; the units of the `Composer` subsystem that
  hold the message shall not write it anywhere.
- The `Composer` subsystem shall not depend on the `Payload` subsystem: the composer owns `IMessageBuilder` and the
  payload builder implements it, so the dependency runs from `Payload` to `Composer` only.
- A mark shall hold the picture it was drawn on and shall be shown only on that picture; its geometry shall be in the
  pixels of that picture, and the units of the `Annotation` subsystem shall not write anything anywhere.
- The `AnnotationOverlay` shall not depend on the composer: it reports the marks it makes and is told which mark chips
  the message holds, and the chat panel joins the two, so that the dependency between `Composer` and `Annotation`
  runs one way. It shall draw a mark with the colors and sizes of the annotated picture of a message, so that the user
  sees what the assistant gets.
- The `Chat` subsystem shall list only messages that were sent, shall keep the words of a conversation, and only the
  words, on the user's computer outside the project folder, shall refuse a history folder inside the project, and shall
  show in the payload inspector exactly the text that `PayloadDescriber` writes and the picture bytes that the payload
  holds; it shall not depend on `AppShell`, and it shall not write anything into a project.
- The `Chat` subsystem shall reach the AI copilot only through `IAgentService` and the `AgentEvent` records, shall never
  refer to a provider, a tool, or a library of the `Agent` subsystem, and shall never let a failure of the copilot throw
  into the interface; the `Agent` subsystem shall not depend on `Chat`. The composer decides whether a message can be
  sent: the chat only gives it the reason that blocks sending while a turn runs.
- The `Agent` subsystem shall not depend on `Chat`, `AppShell`, or any subsystem above it, and no lower subsystem shall
  depend on it: `Settings` owns `IProviderCheck` and `IProviderModelSource`, which `AgentReadiness` and
  `ProviderModelCatalog` implement, and `Settings` shall never refer to
  `Agent`. Only the composition root shall build its units.
- The copilot shall have only the tools of the `Agent` subsystem, each confined to the project folder through the path
  policy and the link guard; it shall write only text image sources and small data files, directly, and shall say
  whether a file was created or replaced; and it shall never write the `.canvaspilot` folder, the project file, or the
  style kits of the project.
- The copilot shall keep its data, the history of the chat, and the data of the Copilot runtime outside the project
  folder, shall refuse a project that contains them, shall write only metadata to the log, and shall never store, show,
  or log a credential.
- The application shall never bundle, copy, or download the GitHub Copilot runtime, and GitHub Copilot shall be
  reported ready only when the runtime started on the Copilot CLI that the user installed and is signed in.
- The talk button shall listen only while it is held: a release of the pointer anywhere, a lost capture, a lost focus,
  and the removal of the panel shall each end it.
- A payload shall be built only from values that cannot change and shall itself never change, and the units of the
  `Payload` subsystem shall not write anything anywhere.
- Content that is dropped or pasted shall be treated as data: it shall never be opened, run, rendered, or followed,
  and only `AttachmentStore` shall create files for attachments, only in `attachments` and `.canvaspilot/attachments`
  of the project, never over an existing file, and, for one message, at most 500 files, 500 folders, and 200 MB.
- Only `AudioDeviceService` shall use the audio system, only `SpeechModelService` shall use the speech model
  catalog, and only `DictationService` shall hold a loaded speech engine and create recognition sessions; no
  backend shall be used before it is first asked, and none of them shall throw for a missing audio system, an
  unreadable model store, a failed download, or a speech engine that cannot start.
- Speech devices shall be stored by name, speech models by identifier, and model parameter values as
  invariant-culture strings.
- A speech model download shall start only on the user's request and shall be cancelled when the settings dialog
  closes.
- The native speech runtime shall be shipped beside the application and in the installer, and no speech engine shall be
  loaded unless the inference runtime and the Sherpa interface are present; the published output and the installer's
  file table shall be verified for it with every Windows build.
- Dictation shall listen only while a talk control is held and for a short moment afterwards, shall release the
  microphone when the session ends, and shall never write what was recognized to the log.
- The `Knowledge` subsystem shall hold no reference to any AI provider SDK, client, or agent framework, shall write
  nothing anywhere, and shall have a user interface only in `KnowledgeBrowser`; it shall not depend on `AppShell`, which
  asks for the browser through `IKnowledgeBrowserLauncher`.
- The catalog shall be extended with the cards of the application before the cards that are only about the host program
  are left out (by identifier, not by the tag `hosting`, which is also on cards that a drawing needs), shall be built
  once from embedded resources only, and shall never change; the index of its cards shall stay
  within the character budget of `KnowledgePromptBuilder`.
- The installer project shall stay outside the solution so that building and restoring the solution never needs the
  WiX Toolset or Windows; it shall be built only by the Windows build job from the published win-x64 output.
- The `Style` subsystem shall hold no reference to any AI provider SDK, client, or agent framework and shall add no
  package; it shall not be depended on by `Preview`, `Project`, `Settings`, or `Knowledge`, and it shall not depend on
  `AppShell`.
- Only `StyleKitCatalog` shall write the kits of the user and of the project and only `StyleKitArchive` shall create
  files from an archive, only inside the folder it was given; the kits of the program and of shared folders shall never
  be changed by the application, and the project's kits shall live in `styles`, never in `.canvaspilot`.
- A shared folder shall be read with a time limit and off the user interface thread, and a kit that cannot be read shall
  be reported and shall never keep the other kits from being listed.
- The guidance of a kit shall not exceed `StyleGuidance.BudgetCharacters` characters, and the kit's text shall be marked
  as data.
- The installer shall replace any installed version and shall not write or remove user data.
