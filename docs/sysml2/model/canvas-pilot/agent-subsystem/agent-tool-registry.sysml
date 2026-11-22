package CanvasPilot_AgentToolRegistry {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::String;
    private import CanvasPilot_KnowledgeTools::*;
    private import CanvasPilot_ProjectService::*;
    private import OtsDependencies::*;

    part def AgentToolRegistry {
        doc /* Builds the tools that the copilot may use for one project. It confines them to the project folder with a path policy of the AI library (the folder is the working directory and the one read-write grant, and the data folder .canvaspilot and every project file *.canvaspilot are denied; the policy does not follow folder links, so the project tools must refuse links themselves), offers the built-in packs made of the existing tool descriptions (canvasnet_help of the drawing knowledge, and the style tools style_get and style_check) through a descriptor tool pack that turns each description into a function of the AI library whose schema is the description's schema and whose call writes the arguments as the JSON text the description reads and never throws (a failure is an answer that names the tool and the exception type), adds the packs that the application supplies per project and the optional packs that the user chose from a catalog that is empty in this version, and refuses a set that has no tool, repeats a name or a pack prefix, or uses the name of a built-in tool of the Copilot CLI. It holds the descriptor tool pack, the function, the project context, and the entry of the optional pack catalog as supporting types, which have no part definition of their own. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/AgentToolContext.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/AgentToolRegistry.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/DescriptorFunction.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/DescriptorToolPack.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/OptionalPackEntry.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/AgentToolRegistryTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/DescriptorToolPackTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/ToolTestDoubles.cs"
            );
            designFiles = ("docs/design/canvas-pilot/agent-subsystem/agent-tool-registry.md");
            verificationFiles = ("docs/verification/canvas-pilot/agent-subsystem/agent-tool-registry.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/agent-subsystem/agent-tool-registry.yaml");
        }

        // Takes the plain tool descriptions (KnowledgeTool) of the drawing knowledge and of the style tools, which know nothing of the AI library, and offers them as functions.
        dependency AgentToolRegistry to KnowledgeTools;

        // Reads the name of the data folder and the extension of the project file, which the path policy denies to every tool (the project document is part of the project service unit).
        dependency AgentToolRegistry to ProjectService;

        // Builds the path policy, composes the tool packs, wraps each function with the limits of the policy, and takes the names of the packs from the naming rule.
        dependency AgentToolRegistry to AgentKit;

        // Derives the function type that carries a tool description, with its name, words, schema, and call.
        dependency AgentToolRegistry to ExtensionsAi;

        // The family prefix of the pack with the drawing knowledge.
        attribute canvasNetPrefix : String;

        // The family prefix of the pack with the style tools.
        attribute stylePrefix : String;
    }
}
