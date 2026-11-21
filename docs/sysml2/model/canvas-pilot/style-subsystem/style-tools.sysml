package CanvasPilot_StyleTools {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_ImageRenderService::*;
    private import CanvasPilot_KnowledgeTools::*;
    private import CanvasPilot_PathHelpers::*;
    private import CanvasPilot_StyleChecker::*;
    private import CanvasPilot_StyleGuidance::*;
    private import CanvasPilot_StyleKitCatalog::*;
    private import CanvasPilot_StyleSelection::*;

    part def StyleTools {
        doc /* Describes the tools style_get and style_check as plain values with a function from the arguments as text to an answer as text: style_get gives the guidance or one section of a kit, and style_check draws a picture file that is confined to the folder of the open project and checks it against the palette of the kit of the project; no tool throws. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleTools.cs");
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/StyleToolsTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/StyleSubsystemFakes.cs"
            );
            designFiles = ("docs/design/canvas-pilot/style-subsystem/style-tools.md");
            verificationFiles = ("docs/verification/canvas-pilot/style-subsystem/style-tools.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/style-subsystem/style-tools.yaml");
        }

        // Gives the guidance and its sections for style_get.
        dependency StyleTools to StyleGuidance;

        // Checks the picture for style_check.
        dependency StyleTools to StyleChecker;

        // Reads the kit of the open project.
        dependency StyleTools to StyleSelection;

        // Finds a kit by its id and lists the kits that are known.
        dependency StyleTools to StyleKitCatalog;

        // Uses the descriptor record of a tool, and nothing else of the Knowledge subsystem.
        dependency StyleTools to KnowledgeTools;

        // Draws the file to check.
        dependency StyleTools to ImageRenderService;

        // Joins the path of a call to the folder of the project and refuses a path that leaves it.
        dependency StyleTools to PathHelpers;
    }
}
