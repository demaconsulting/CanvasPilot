// Views rendered into docs/design/generated/ for the Design document.
// Declares one repository-level views package that imports the system and subsystem
// packages so each 'expose' statement can reference its target part def directly. Per
// SysML2Tools syntax, 'expose' is only valid inside a named 'view' usage (not a
// 'view def' definition), and it - not 'render' - is what scopes the rendered diagram
// content.
//
// This file is a sibling of docs/sysml2/model/ (not nested inside it), so a single
// 'docs/sysml2/model/**/*.sysml' glob never needs to exclude view files.
//
// SoftwareStructureView is the system-level overview: it exposes only the system part def,
// so the system renders as a frame with its direct subsystem usages (unit detail lives in
// the per-system and per-subsystem views). OtsView keeps the OTS items apart from it, and
// the 'SoftwareInfo' metadata def is left out of every overview by not exposing its package.
//
// Each container view exposes the container part def and its direct children
// ('expose X; expose X::*;') and renders 'asInterconnectionDiagram' so nested
// composition renders correctly.
//
// The '*DependencyView' views and the themed '{Subsystem}{Theme}View' views are focused
// dependency views: each exposes the units of one theme plus the suppliers that their
// 'dependency' statements name, so the dependency edges render. A subsystem whose edges
// would pass about 15 in one diagram is split into themed views. Four hub suppliers that
// many units depend on are omitted from every one of them - RollingFileLogger (23 units),
// ImageRenderService (12), PathHelpers (12), and ProjectService (15) - and each view's
// 'doc' names the hubs that its units use; ChatSubsystemHistoryView is the one exception, which
// shows ProjectService because the key of the chat history comes from the project. The small LoggingSubsystem, UtilitiesSubsystem,
// AnnotationSubsystem, ProjectSubsystem, SettingsSubsystem, and PreviewSubsystem have few
// edges that are not hub edges, so they have no dependency view; use
// 'sysml2tools query dependencies' for them.
package CanvasPilotDesignViews {
    private import StandardViewDefinitions::*;
    private import Views::*;
    private import OtsDependencies::*;
    private import CanvasPilot::*;
    private import CanvasPilot_AgentSubsystem::*;
    private import CanvasPilot_AnnotationSubsystem::*;
    private import CanvasPilot_AppShellSubsystem::*;
    private import CanvasPilot_ChatSubsystem::*;
    private import CanvasPilot_ComposerSubsystem::*;
    private import CanvasPilot_KnowledgeSubsystem::*;
    private import CanvasPilot_LoggingSubsystem::*;
    private import CanvasPilot_PayloadSubsystem::*;
    private import CanvasPilot_PreviewSubsystem::*;
    private import CanvasPilot_ProjectSubsystem::*;
    private import CanvasPilot_SettingsSubsystem::*;
    private import CanvasPilot_SpeechSubsystem::*;
    private import CanvasPilot_StyleSubsystem::*;
    private import CanvasPilot_UtilitiesSubsystem::*;

    view SoftwareStructureView : InterconnectionView {
        expose CanvasPilot::CanvasPilotSystem;  // system part def: the system frame with its direct subsystem usages
        filter not (@SysML::Documentation);
    }

    view OtsView : GeneralView {
        // Each OTS part def is named so that the SoftwareInfo metadata def, which the package
        // imports, stays out of the diagram.
        expose OtsDependencies::Avalonia;
        expose OtsDependencies::AvaloniaEdit;
        expose OtsDependencies::CommunityToolkitMvvm;
        expose OtsDependencies::CanvasNet;
        expose OtsDependencies::Speech;
        expose OtsDependencies::AgentKit;
        expose OtsDependencies::CopilotSdk;
        expose OtsDependencies::AgentFramework;
        expose OtsDependencies::ExtensionsAi;
        expose OtsDependencies::OllamaSharp;
        expose OtsDependencies::WiX;
        filter not (@SysML::Documentation);
    }

    view CanvasPilotView {
        expose CanvasPilotSystem;
        expose CanvasPilotSystem::*;
        render asInterconnectionDiagram;
    }

    view AgentSubsystemView {
        expose AgentSubsystem;
        expose AgentSubsystem::*;
        render asInterconnectionDiagram;
    }

    view AnnotationSubsystemView {
        expose AnnotationSubsystem;
        expose AnnotationSubsystem::*;
        render asInterconnectionDiagram;
    }

    view AppShellSubsystemView {
        expose AppShellSubsystem;
        expose AppShellSubsystem::*;
        render asInterconnectionDiagram;
    }

    view ChatSubsystemView {
        expose ChatSubsystem;
        expose ChatSubsystem::*;
        render asInterconnectionDiagram;
    }

    view ComposerSubsystemView {
        expose ComposerSubsystem;
        expose ComposerSubsystem::*;
        render asInterconnectionDiagram;
    }

    view KnowledgeSubsystemView {
        expose KnowledgeSubsystem;
        expose KnowledgeSubsystem::*;
        render asInterconnectionDiagram;
    }

    view LoggingSubsystemView {
        expose LoggingSubsystem;
        expose LoggingSubsystem::*;
        render asInterconnectionDiagram;
    }

    view PayloadSubsystemView {
        expose PayloadSubsystem;
        expose PayloadSubsystem::*;
        render asInterconnectionDiagram;
    }

    view PreviewSubsystemView {
        expose PreviewSubsystem;
        expose PreviewSubsystem::*;
        render asInterconnectionDiagram;
    }

    view ProjectSubsystemView {
        expose ProjectSubsystem;
        expose ProjectSubsystem::*;
        render asInterconnectionDiagram;
    }

    view SettingsSubsystemView {
        expose SettingsSubsystem;
        expose SettingsSubsystem::*;
        render asInterconnectionDiagram;
    }

    view SpeechSubsystemView {
        expose SpeechSubsystem;
        expose SpeechSubsystem::*;
        render asInterconnectionDiagram;
    }

    view StyleSubsystemView {
        expose StyleSubsystem;
        expose StyleSubsystem::*;
        render asInterconnectionDiagram;
    }

    view UtilitiesSubsystemView {
        expose UtilitiesSubsystem;
        expose UtilitiesSubsystem::*;
        render asInterconnectionDiagram;
    }

    // Focused dependency views. Each exposes the units of one theme plus the suppliers that
    // their 'dependency' statements name; the hub suppliers are omitted (see the header).

    view AgentSubsystemRuntimeView : GeneralView {
        doc /* Dependencies of the readiness checks of the AI provider on the Copilot CLI locator, the starter of the Copilot runtime, the model list and its policy, the settings, DemaConsulting.AgentKit, and the OllamaSharp client, of the model list on the locator, the starter, the page that owns its interface, DemaConsulting.AgentKit, and the OllamaSharp client, of the event mapper on the contract, the readiness sentences, and the settings, and of the provider factory on the contract, the readiness sentences, the starter of the Copilot runtime, the model list and its policy, the locator, the settings, DemaConsulting.AgentKit, and the OllamaSharp client. Hub suppliers omitted: RollingFileLogger (the event mapper and the provider factory log metadata), and the Microsoft Agent Framework and Microsoft.Extensions.AI (the event mapper and the provider factory use their agent, content, and function types). */
        expose CanvasPilot_AgentReadiness::AgentReadiness;
        expose CanvasPilot_CopilotRuntimeStarter::CopilotRuntimeStarter;
        expose CanvasPilot_ProviderModelCatalog::ProviderModelCatalog;
        expose CanvasPilot_CopilotCliLocator::CopilotCliLocator;
        expose CanvasPilot_AgentEventMapper::AgentEventMapper;
        expose CanvasPilot_AgentProviderFactory::AgentProviderFactory;
        expose CanvasPilot_AgentContract::AgentContract;
        expose CanvasPilot_SettingsService::SettingsService;
        expose CanvasPilot_AiProviderSettingsPage::AiProviderSettingsPage;
        expose OtsDependencies::AgentKit;
        expose OtsDependencies::OllamaSharp;
        filter not (@SysML::Documentation);
    }

    view AgentServiceTurnView : GeneralView {
        doc /* Dependencies of the agent service on what runs a turn: the contract that it implements (the events, the failure kinds, and the readiness report), the event mapper that turns the updates of the agent into events, the provider factory that makes the agent and deletes its session, the readiness checks, and the settings whose change rebuilds the conversation; and, among the same units, what the mapper and the factory use of the contract, the readiness sentences, and the settings. Hub suppliers omitted: ProjectService (the open project and its change event), RollingFileLogger (the service logs the metadata of each turn and conversation), and the Microsoft Agent Framework (the agent and the provider session). */
        expose CanvasPilot_AgentService::AgentService;
        expose CanvasPilot_AgentContract::AgentContract;
        expose CanvasPilot_AgentEventMapper::AgentEventMapper;
        expose CanvasPilot_AgentProviderFactory::AgentProviderFactory;
        expose CanvasPilot_AgentReadiness::AgentReadiness;
        expose CanvasPilot_SettingsService::SettingsService;
        filter not (@SysML::Documentation);
    }

    view AgentServiceProjectView : GeneralView {
        doc /* Dependencies of the agent service on what it builds for the open project: the tool registry that confines the tools to the project folder, the project tools whose written files it passes on, the instructions writer, the mapper of the message of the user, and the style kit of the project that the instructions describe; and, among the same units, what the project tools use of the registry and of the active style. Hub suppliers omitted: ProjectService (the open project), RollingFileLogger (the metadata of each turn), and the Microsoft Agent Framework. */
        expose CanvasPilot_AgentService::AgentService;
        expose CanvasPilot_AgentToolRegistry::AgentToolRegistry;
        expose CanvasPilot_ProjectTools::ProjectTools;
        expose CanvasPilot_AgentInstructions::AgentInstructions;
        expose CanvasPilot_PayloadMessageMapper::PayloadMessageMapper;
        expose CanvasPilot_StyleSelection::StyleSelection;
        filter not (@SysML::Documentation);
    }

    view AgentToolsView : GeneralView {
        doc /* Dependencies of the tool registry and the project tools of the AI copilot: the registry on the drawing knowledge tool descriptions it offers and on the AI libraries that confine and carry the tools, and the project tools on the registry that composes them, on the active style kit and the styles folder name, on the project explorer's filter of image sources, and on the AI libraries, and the instructions on the knowledge prompt builder that writes the index, the style guidance that writes the style, and the settings that give the recommended context window, and the payload mapper on the message payload that it maps and on the AI library that carries the message. Hub suppliers omitted: ProjectService (both read the data folder name and project file extension from the project document), ImageRenderService (the picture tool draws through it), and RollingFileLogger (the project tools log each call). */
        expose CanvasPilot_AgentToolRegistry::AgentToolRegistry;
        expose CanvasPilot_ProjectTools::ProjectTools;
        expose CanvasPilot_AgentInstructions::AgentInstructions;
        expose CanvasPilot_PayloadMessageMapper::PayloadMessageMapper;
        expose CanvasPilot_MessagePayload::MessagePayload;
        expose CanvasPilot_KnowledgeTools::KnowledgeTools;
        expose CanvasPilot_KnowledgePromptBuilder::KnowledgePromptBuilder;
        expose CanvasPilot_StyleGuidance::StyleGuidance;
        expose CanvasPilot_SettingsService::SettingsService;
        expose CanvasPilot_StyleSelection::StyleSelection;
        expose CanvasPilot_StyleKitCatalog::StyleKitCatalog;
        expose CanvasPilot_ProjectExplorer::ProjectExplorer;
        expose OtsDependencies::AgentKit;
        expose OtsDependencies::ExtensionsAi;
        filter not (@SysML::Documentation);
    }

    view AppShellSubsystemWorkspaceView : GeneralView {
        doc /* Dependencies of the main window shell and the project workspace on the workspace panels they host. Hub supplier omitted: ProjectService. */
        expose CanvasPilot_MainWindowShell::MainWindowShell;
        expose CanvasPilot_ProjectWorkspace::ProjectWorkspace;
        expose CanvasPilot_AboutDialog::AboutDialog;
        expose CanvasPilot_ProjectExplorer::ProjectExplorer;
        expose CanvasPilot_ChatPanel::ChatPanel;
        expose CanvasPilot_KnowledgeBrowser::KnowledgeBrowser;
        expose CanvasPilot_PayloadInspector::PayloadInspector;
        expose CanvasPilot_ComposerViewModel::ComposerViewModel;
        filter not (@SysML::Documentation);
    }

    view AppShellSubsystemChromeView : GeneralView {
        doc /* Dependencies of the main window shell on the status bar, the AI provider pill, and the welcome screen that it creates, on the settings it saves and follows, on the key that is held to talk, which it listens for and which the status bar and the welcome screen name, and on the design tokens, and of those three units on the panels they report. Hub supplier omitted: RollingFileLogger. */
        expose CanvasPilot_MainWindowShell::MainWindowShell;
        expose CanvasPilot_StatusBar::StatusBar;
        expose CanvasPilot_ProviderPill::ProviderPill;
        expose CanvasPilot_Welcome::Welcome;
        expose CanvasPilot_ThemeResources::ThemeResources;
        expose CanvasPilot_SettingsService::SettingsService;
        expose CanvasPilot_ProjectWorkspace::ProjectWorkspace;
        expose CanvasPilot_PreviewPane::PreviewPane;
        expose CanvasPilot_ChatPanel::ChatPanel;
        expose CanvasPilot_AgentContract::AgentContract;
        expose CanvasPilot_ProjectExplorer::ProjectExplorer;
        expose CanvasPilot_PushToTalkBinding::PushToTalkBinding;
        filter not (@SysML::Documentation);
    }

    view AppShellSubsystemActionsView : GeneralView {
        doc /* Dependencies of the main window shell on the dialogs and actions that it opens. Hub supplier omitted: ProjectService. */
        expose CanvasPilot_MainWindowShell::MainWindowShell;
        expose CanvasPilot_RecentProjects::RecentProjects;
        expose CanvasPilot_NewProjectDialog::NewProjectDialog;
        expose CanvasPilot_SettingsDialog::SettingsDialog;
        expose CanvasPilot_ImageExporter::ImageExporter;
        expose CanvasPilot_ExportPresetDialog::ExportPresetDialog;
        expose CanvasPilot_StylePicker::StylePicker;
        expose CanvasPilot_StyleKitManager::StyleKitManager;
        expose CanvasPilot_StyleChecker::StyleChecker;
        filter not (@SysML::Documentation);
    }

    view ChatSubsystemPanelView : GeneralView {
        doc /* Dependencies of the chat panel on the units that compose a message, on the turn runner that runs the turn of the AI copilot, on the planner of the prompts that it offers, on the key that is held to talk whose name it shows, on the contract of the Agent subsystem through which it asks whether the provider can be used, and on the Markdown reader of the Knowledge subsystem that draws the final text of a reply. Hub suppliers omitted: RollingFileLogger, PathHelpers, and ProjectService; the dependencies on the chat history store and the project service are in ChatSubsystemHistoryView. */
        expose CanvasPilot_ChatPanel::ChatPanel;
        expose CanvasPilot_ChatTurnRunner::ChatTurnRunner;
        expose CanvasPilot_FollowUpSuggestions::FollowUpSuggestions;
        expose CanvasPilot_PushToTalkBinding::PushToTalkBinding;
        expose CanvasPilot_AgentContract::AgentContract;
        expose CanvasPilot_KnowledgeMarkdown::KnowledgeMarkdown;
        expose CanvasPilot_ComposerViewModel::ComposerViewModel;
        expose CanvasPilot_ComposerEditor::ComposerEditor;
        expose CanvasPilot_MarkRegistry::MarkRegistry;
        expose CanvasPilot_AnnotationOverlay::AnnotationOverlay;
        expose CanvasPilot_MarkImageComposer::MarkImageComposer;
        expose CanvasPilot_MessagePayload::MessagePayload;
        expose CanvasPilot_DictationService::DictationService;
        expose CanvasPilot_PayloadInspector::PayloadInspector;
        filter not (@SysML::Documentation);
    }

    view ChatSubsystemTurnView : GeneralView {
        doc /* Dependencies of the chat turn runner on the rows of the chat panel that it makes and changes, the contract of the Agent subsystem through which it reaches the AI copilot, the composer view model whose sent message it takes, and the message payload that it gives to the copilot. Hub supplier omitted: RollingFileLogger (the runner logs the kind of a failure). */
        expose CanvasPilot_ChatTurnRunner::ChatTurnRunner;
        expose CanvasPilot_ChatPanel::ChatPanel;
        expose CanvasPilot_AgentContract::AgentContract;
        expose CanvasPilot_ComposerViewModel::ComposerViewModel;
        expose CanvasPilot_MessagePayload::MessagePayload;
        filter not (@SysML::Documentation);
    }

    view ChatSubsystemHistoryView : GeneralView {
        doc /* Dependencies of the chat history store and of the chat panel on it: the store takes the project information types of the project service, whose project file names the history, and the panel takes the open project from the same service. The project service is a hub supplier that the other views omit; it is shown here on purpose, because the key of the history comes from the project. Hub suppliers omitted: RollingFileLogger and PathHelpers. */
        expose CanvasPilot_ChatHistoryStore::ChatHistoryStore;
        expose CanvasPilot_ChatPanel::ChatPanel;
        expose CanvasPilot_ProjectService::ProjectService;
        filter not (@SysML::Documentation);
    }

    view ChatSubsystemInspectorView : GeneralView {
        doc /* Dependencies of the payload inspector on the payload types that it shows. No hub supplier is used by these units. */
        expose CanvasPilot_PayloadInspector::PayloadInspector;
        expose CanvasPilot_MessagePayload::MessagePayload;
        expose CanvasPilot_PayloadDescriber::PayloadDescriber;
        filter not (@SysML::Documentation);
    }

    view ComposerSubsystemDependencyView : GeneralView {
        doc /* Dependencies of every composer unit and their suppliers. Hub suppliers omitted: RollingFileLogger, PathHelpers, and ProjectService. */
        expose CanvasPilot_ComposerDocument::ComposerDocument;
        expose CanvasPilot_ComposerHistory::ComposerHistory;
        expose CanvasPilot_AttachmentIntake::AttachmentIntake;
        expose CanvasPilot_AttachmentStore::AttachmentStore;
        expose CanvasPilot_ComposerViewModel::ComposerViewModel;
        expose CanvasPilot_ComposerEditor::ComposerEditor;
        expose OtsDependencies::AvaloniaEdit;
        expose CanvasPilot_MarkRegistry::MarkRegistry;
        expose CanvasPilot_DictationService::DictationService;
        filter not (@SysML::Documentation);
    }

    view KnowledgeSubsystemDependencyView : GeneralView {
        doc /* Dependencies of every knowledge unit and their suppliers. Hub supplier omitted: RollingFileLogger. */
        expose CanvasPilot_KnowledgeCatalog::KnowledgeCatalog;
        expose CanvasPilot_KnowledgeSearch::KnowledgeSearch;
        expose CanvasPilot_KnowledgePromptBuilder::KnowledgePromptBuilder;
        expose CanvasPilot_KnowledgeTools::KnowledgeTools;
        expose CanvasPilot_KnowledgeSkills::KnowledgeSkills;
        expose CanvasPilot_KnowledgeMarkdown::KnowledgeMarkdown;
        expose CanvasPilot_KnowledgeBrowser::KnowledgeBrowser;
        expose OtsDependencies::CanvasNet;
        filter not (@SysML::Documentation);
    }

    view PayloadSubsystemBuilderView : GeneralView {
        doc /* Dependencies of the payload builder on the composer, annotation, and payload units that it assembles. Hub supplier omitted: ImageRenderService. */
        expose CanvasPilot_PayloadBuilder::PayloadBuilder;
        expose CanvasPilot_ComposerDocument::ComposerDocument;
        expose CanvasPilot_AttachmentStore::AttachmentStore;
        expose CanvasPilot_MarkRegistry::MarkRegistry;
        expose CanvasPilot_MarkSourceResolver::MarkSourceResolver;
        expose CanvasPilot_MarkImageComposer::MarkImageComposer;
        expose CanvasPilot_MessagePayload::MessagePayload;
        expose CanvasPilot_ComposerViewModel::ComposerViewModel;
        filter not (@SysML::Documentation);
    }

    view PayloadSubsystemMessageView : GeneralView {
        doc /* Dependencies of the message payload, its description, and the mark image composer. Hub supplier omitted: ImageRenderService. */
        expose CanvasPilot_MessagePayload::MessagePayload;
        expose CanvasPilot_PayloadDescriber::PayloadDescriber;
        expose CanvasPilot_MarkImageComposer::MarkImageComposer;
        expose OtsDependencies::CanvasNet;
        expose CanvasPilot_MarkRegistry::MarkRegistry;
        expose CanvasPilot_ComposerDocument::ComposerDocument;
        expose CanvasPilot_MarkSourceResolver::MarkSourceResolver;
        filter not (@SysML::Documentation);
    }

    view SpeechSubsystemDependencyView : GeneralView {
        doc /* Dependencies of every speech unit and their suppliers. Hub supplier omitted: RollingFileLogger. */
        expose CanvasPilot_AudioDeviceService::AudioDeviceService;
        expose CanvasPilot_SpeechModelService::SpeechModelService;
        expose CanvasPilot_DictationService::DictationService;
        expose CanvasPilot_DeviceSelection::DeviceSelection;
        expose CanvasPilot_ModelCatalogPanel::ModelCatalogPanel;
        expose CanvasPilot_ModelParameterEditor::ModelParameterEditor;
        expose CanvasPilot_SpeechSettingsPage::SpeechSettingsPage;
        expose CanvasPilot_PushToTalkBinding::PushToTalkBinding;
        expose OtsDependencies::Speech;
        expose CanvasPilot_SettingsService::SettingsService;
        expose CanvasPilot_SettingsDialog::SettingsDialog;
        filter not (@SysML::Documentation);
    }

    view StyleSubsystemAssistantView : GeneralView {
        doc /* Dependencies of the style guidance, checker, and tools that serve an assistant. Hub suppliers omitted: ImageRenderService and PathHelpers. */
        expose CanvasPilot_StyleGuidance::StyleGuidance;
        expose CanvasPilot_StyleChecker::StyleChecker;
        expose CanvasPilot_StyleTools::StyleTools;
        expose CanvasPilot_StyleKitDocument::StyleKitDocument;
        expose CanvasPilot_PreviewPane::PreviewPane;
        expose CanvasPilot_StyleSelection::StyleSelection;
        expose CanvasPilot_StyleKitCatalog::StyleKitCatalog;
        expose CanvasPilot_KnowledgeTools::KnowledgeTools;
        filter not (@SysML::Documentation);
    }

    view StyleSubsystemKitsView : GeneralView {
        doc /* Dependencies of the style kit document, catalog, archive, and selection. Hub suppliers omitted: RollingFileLogger, PathHelpers, and ProjectService. */
        expose CanvasPilot_StyleKitDocument::StyleKitDocument;
        expose CanvasPilot_StyleKitCatalog::StyleKitCatalog;
        expose CanvasPilot_StyleKitArchive::StyleKitArchive;
        expose CanvasPilot_StyleSelection::StyleSelection;
        expose CanvasPilot_SettingsService::SettingsService;
        expose OtsDependencies::CanvasNet;
        expose CanvasPilot_ImageExporter::ImageExporter;
        filter not (@SysML::Documentation);
    }

    view StyleSubsystemEditingView : GeneralView {
        doc /* Dependencies of the style kit editor, picker, and settings page. Hub supplier omitted: ProjectService. */
        expose CanvasPilot_StyleKitEditor::StyleKitEditor;
        expose CanvasPilot_StylePicker::StylePicker;
        expose CanvasPilot_StyleSettingsPage::StyleSettingsPage;
        expose CanvasPilot_StyleKitDocument::StyleKitDocument;
        expose CanvasPilot_StyleKitCatalog::StyleKitCatalog;
        expose CanvasPilot_StyleSelection::StyleSelection;
        expose CanvasPilot_SettingsDialog::SettingsDialog;
        filter not (@SysML::Documentation);
    }

    view StyleSubsystemManagerView : GeneralView {
        doc /* Dependencies of the style kit manager on the units that it drives. Hub suppliers omitted: ImageRenderService and ProjectService. */
        expose CanvasPilot_StyleKitManager::StyleKitManager;
        expose CanvasPilot_StyleKitCatalog::StyleKitCatalog;
        expose CanvasPilot_StyleKitArchive::StyleKitArchive;
        expose CanvasPilot_StyleSelection::StyleSelection;
        expose CanvasPilot_StyleKitEditor::StyleKitEditor;
        expose CanvasPilot_StyleGuidance::StyleGuidance;
        expose CanvasPilot_StylePicker::StylePicker;
        filter not (@SysML::Documentation);
    }
}
