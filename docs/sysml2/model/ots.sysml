package OtsDependencies {
    private import SoftwareItemMetadata::*;

    doc /* Off-the-shelf (OTS) dependencies used by CanvasPilot. */

    part def Avalonia {
        doc /* Avalonia UI framework (Avalonia, Avalonia.Skia, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter, Avalonia.Desktop, and Avalonia.Headless.XUnit packages) that shows the application's windows, menus, dialogs, bound views, and pictures. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/OtsSoftwareTests/AvaloniaTests.cs",
                "test/OtsSoftwareTests/TestAppBuilder.cs"
            );
            designFiles = ("docs/design/ots/avalonia.md");
            verificationFiles = ("docs/verification/ots/avalonia.md");
            requirementFiles = ("docs/reqstream/ots/avalonia.yaml");
        }
    }

    part def AvaloniaEdit {
        doc /* Avalonia.AvaloniaEdit NuGet dependency, the text editing control that the message composer is built on: the surface in which typed text, dictated text, and inline chips stand, with its caret, selection, input method support, change reports, and replaceable commands. */

        metadata info : SoftwareInfo {
            testFiles = ("test/OtsSoftwareTests/AvaloniaEditTests.cs");
            designFiles = ("docs/design/ots/avalonia-edit.md");
            verificationFiles = ("docs/verification/ots/avalonia-edit.md");
            requirementFiles = ("docs/reqstream/ots/avalonia-edit.yaml");
        }
    }

    part def CommunityToolkitMvvm {
        doc /* CommunityToolkit.Mvvm NuGet dependency whose source generators write the property change notification and command code of the application's view models. */

        metadata info : SoftwareInfo {
            testFiles = ("test/OtsSoftwareTests/CommunityToolkitMvvmTests.cs");
            designFiles = ("docs/design/ots/communitytoolkit-mvvm.md");
            verificationFiles = ("docs/verification/ots/communitytoolkit-mvvm.md");
            requirementFiles = ("docs/reqstream/ots/communitytoolkit-mvvm.yaml");
        }
    }
    part def CanvasNet {
        doc /* DemaConsulting.CanvasNet.Viewer NuGet dependency (with the DemaConsulting.CanvasNet core library it brings) that draws SVG, diagram, chart, model, document, and raster files to pixels, encodes PNG, and provides its help as cards that can be filtered by tag and extended. */

        metadata info : SoftwareInfo {
            testFiles = ("test/OtsSoftwareTests/CanvasNetTests.cs");
            designFiles = ("docs/design/ots/canvasnet.md");
            verificationFiles = ("docs/verification/ots/canvasnet.md");
            requirementFiles = ("docs/reqstream/ots/canvasnet.yaml");
        }
    }

    part def Speech {
        doc /* DemaConsulting.Speech NuGet dependency, with its DemaConsulting.Speech.Sherpa model package, that lists audio devices, creates capture devices, lists and downloads speech models, describes their tunable parameters, and streams speech recognition; the native inference, Sherpa, and audio runtime arrives with its packages and is shipped beside the application. */

        metadata info : SoftwareInfo {
            testFiles = ("test/OtsSoftwareTests/SpeechTests.cs");
            designFiles = ("docs/design/ots/speech.md");
            verificationFiles = ("docs/verification/ots/speech.md");
            requirementFiles = ("docs/reqstream/ots/speech.yaml");
        }
    }

    part def AgentKit {
        doc /* DemaConsulting.AgentKit NuGet dependency (the Core, Agents.Copilot, Agents.ChatClient, and Agents.Ollama packages) that confines the AI copilot's tools to the project with a path policy, composes tool packs, bounds tool results, builds agents on a chat client, and sizes the Ollama context window; its Copilot runtime is never bundled and not started. */

        metadata info : SoftwareInfo {
            sourceFiles = ("Directory.Build.props");
            testFiles = (
                "test/OtsSoftwareTests/AgentKitTests.cs",
                "test/OtsSoftwareTests/AgentTestSupport.cs"
            );
            designFiles = ("docs/design/ots/agentkit.md");
            verificationFiles = ("docs/verification/ots/agentkit.md");
            requirementFiles = ("docs/reqstream/ots/agentkit.yaml");
        }
    }

    part def CopilotSdk {
        doc /* Microsoft.Copilot.SDK NuGet dependency, the client library for the GitHub Copilot CLI that AgentKit drives; it is not started in this version and its runtime is neither bundled nor downloaded. */

        metadata info : SoftwareInfo {
            testFiles = ("test/OtsSoftwareTests/CopilotSdkTests.cs");
            designFiles = ("docs/design/ots/copilot-sdk.md");
            verificationFiles = ("docs/verification/ots/copilot-sdk.md");
            requirementFiles = ("docs/reqstream/ots/copilot-sdk.yaml");
        }
    }

    part def AgentFramework {
        doc /* Microsoft Agent Framework NuGet dependency (Microsoft.Agents.AI and Microsoft.Agents.AI.Abstractions) that provides the agent, the session that holds a conversation, and the streamed turn that the chat runs on. */

        metadata info : SoftwareInfo {
            testFiles = ("test/OtsSoftwareTests/AgentFrameworkTests.cs");
            designFiles = ("docs/design/ots/agent-framework.md");
            verificationFiles = ("docs/verification/ots/agent-framework.md");
            requirementFiles = ("docs/reqstream/ots/agent-framework.yaml");
        }
    }

    part def ExtensionsAi {
        doc /* Microsoft.Extensions.AI NuGet dependency (the abstractions and the function-invocation pipeline) that provides the function, message, picture content, and chat client types that carry the AI copilot's tools and conversation. */

        metadata info : SoftwareInfo {
            testFiles = ("test/OtsSoftwareTests/ExtensionsAiTests.cs");
            designFiles = ("docs/design/ots/extensions-ai.md");
            verificationFiles = ("docs/verification/ots/extensions-ai.md");
            requirementFiles = ("docs/reqstream/ots/extensions-ai.yaml");
        }
    }

    part def OllamaSharp {
        doc /* OllamaSharp NuGet dependency, the client of the Ollama server that lists the installed models and streams chat answers with tool calls for the Ollama provider. */

        metadata info : SoftwareInfo {
            testFiles = ("test/OtsSoftwareTests/OllamaSharpTests.cs");
            designFiles = ("docs/design/ots/ollamasharp.md");
            verificationFiles = ("docs/verification/ots/ollamasharp.md");
            requirementFiles = ("docs/reqstream/ots/ollamasharp.yaml");
        }
    }
    part def WiX {
        doc /* WiX Toolset v5 (WixToolset.Sdk and the Util extension) that builds the published win-x64 application into the Windows Installer (MSI) package; the installer project is built by CI and is not part of the solution. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot.Installer/DemaConsulting.CanvasPilot.Installer.wixproj",
                "src/DemaConsulting.CanvasPilot.Installer/Package.wxs"
            );
            designFiles = ("docs/design/ots/wix.md");
            verificationFiles = ("docs/verification/ots/wix.md");
            requirementFiles = ("docs/reqstream/ots/wix.yaml");
        }
    }
}
