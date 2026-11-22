package CanvasPilot {
    private import SoftwareItemMetadata::*;
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
    private import OtsDependencies::*;

    doc
    /*
     * CanvasPilot is an AI-assisted drawing application built on CanvasNet.
     * The model contains the application shell, agent, annotation, chat, composer, knowledge, logging, payload, preview,
     * project, settings, speech, style, and utility subsystems. Messages are composed, dictated, marked, and sent into a
     * payload; the agent subsystem holds the contract through which the AI copilot that answers them is run, and its
     * units are added to the model as they are built.
     */

    part def CanvasPilotSystem {
        doc /* The CanvasPilot system as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/CanvasPilotTests.cs");
            designFiles = ("docs/design/canvas-pilot.md");
            verificationFiles = ("docs/verification/canvas-pilot.md");
            requirementFiles = ("docs/reqstream/canvas-pilot.yaml");
        }

        part agent : AgentSubsystem;
        part annotation : AnnotationSubsystem;
        part appShell : AppShellSubsystem;
        part chat : ChatSubsystem;
        part composer : ComposerSubsystem;
        part knowledge : KnowledgeSubsystem;
        part logging : LoggingSubsystem;
        part payload : PayloadSubsystem;
        part preview : PreviewSubsystem;
        part project : ProjectSubsystem;
        part settings : SettingsSubsystem;
        part speech : SpeechSubsystem;
        part style : StyleSubsystem;
        part utilities : UtilitiesSubsystem;

        // The user interface framework, its text editing control, and the view model source generators are consumed at
        // run time by the subsystems that have views and view models.
        part userInterface : Avalonia;
        part textEditor : AvaloniaEdit;
        part viewModelToolkit : CommunityToolkitMvvm;

        // CanvasNet is consumed at run time by the Preview, Project, Payload, Knowledge, and Style subsystems.
        part canvasNet : CanvasNet;

        // The speech library is consumed at run time by the Speech subsystem.
        part speechLibrary : Speech;

        // The AI libraries are consumed at run time by the AI copilot: the confined tools and agent factories, the
        // Copilot SDK that AgentKit drives (never bundled), the agent and streaming types, the tool, message, and
        // picture types, and the Ollama client.
        part agentLibrary : AgentKit;
        part copilotClient : CopilotSdk;
        part agentRuntime : AgentFramework;
        part aiAbstractions : ExtensionsAi;
        part ollamaClient : OllamaSharp;

        // The Windows installer package is built by the WiX Toolset from the published application.
        part installer : WiX;
    }
}
