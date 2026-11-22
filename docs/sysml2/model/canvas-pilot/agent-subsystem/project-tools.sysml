package CanvasPilot_ProjectTools {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::Boolean;
    private import ScalarValues::Integer;
    private import ScalarValues::String;
    private import CanvasPilot_AgentToolRegistry::*;
    private import CanvasPilot_ImageRenderService::*;
    private import CanvasPilot_StyleSelection::*;
    private import CanvasPilot_StyleKitCatalog::*;
    private import CanvasPilot_ProjectService::*;
    private import CanvasPilot_ProjectExplorer::*;
    private import CanvasPilot_RollingFileLogger::*;
    private import OtsDependencies::*;

    // What the shell is told after the copilot wrote a file: the path below the project folder with forward slashes (never
    // the content) and whether the file is new.
    attribute def FileWritten {
        attribute relativePath : String;
        attribute created : Boolean;
    }

    part def ProjectTools {
        doc /* The tools with which the AI copilot works on the files of the project, as two tool packs of the AI library: project (project_list, project_read, project_write) and render (render_image). Every tool is confined to the project folder by the path policy that the tool registry builds, and the tools themselves refuse what that policy does not see: a path through a link (a junction or a symbolic link, wherever it is in the path; a hard link is replaced as a name and never written through), a name that means something else (a stream name, a device name, a short 8.3 name, a trailing dot), the data folder, and the project file. project_list shows relative paths, sizes, and whether CanvasNet draws the file, hides the application's files, and stops at 200 files. project_read reads a text image source or a .csv, .json, or .txt file of at most 64 KiB as UTF-8. project_write creates or replaces a file at once, with no copy of the old file and no question (the user decided this, because drawings are kept in source control): only text image sources and .csv, .json, .txt files, at most 256 KiB, never below the styles folder of the project (the style kits), written through a temporary file that never stays; the answer says created or replaced, and the event FileWritten tells the shell, which selects the file in the project explorer. render_image draws an image source or picture of the project, or an example image of the active style kit by its full path (the kit folder is the only place read, images only), through the render service into memory, scales the PNG to at most 1,280 pixels on its longer side, and returns it with a text that has the size, the format, and every note of the renderer; a drawing that fails is an ordinary answer with the code, the message, and the hint, and nothing is saved. The log of each call has metadata only. It holds the two packs, the guard of paths, the file kinds, the scaler, the log of calls, and the event arguments as supporting types, which have no part definition of their own. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/FileWrittenEventArgs.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/ImageScaler.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/ProjectFileKinds.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/ProjectFilesToolPack.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/ProjectPathGuard.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/ProjectTools.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/RenderImageToolPack.cs",
                "src/DemaConsulting.CanvasPilot/AgentSubsystem/ToolCallLog.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/ProjectToolsSupportTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/ProjectToolsTestDoubles.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/ProjectToolsTests.cs",
                "test/DemaConsulting.CanvasPilot.Tests/AgentSubsystem/RenderImageToolTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/agent-subsystem/project-tools.md");
            verificationFiles = ("docs/verification/canvas-pilot/agent-subsystem/project-tools.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/agent-subsystem/project-tools.yaml");
        }

        // Takes the project context and is the supplier of project packs that the tool registry composes under its confining path policy.
        dependency ProjectTools to AgentToolRegistry;

        // Draws image sources into memory and encodes the PNG for render_image, which saves nothing (hub supplier, omitted from the tools view).
        dependency ProjectTools to ImageRenderService;

        // Reads the active style kit when render_image is called, to allow the example images of that kit and no other place.
        dependency ProjectTools to StyleSelection;

        // Reads the name of the styles folder of a project, whose style kits the copilot never writes.
        dependency ProjectTools to StyleKitCatalog;

        // Reads the name of the data folder and the extension of the project file that are never written (hub supplier, omitted from the tools view).
        dependency ProjectTools to ProjectService;

        // Uses the filter that tells which files CanvasNet draws.
        dependency ProjectTools to ProjectExplorer;

        // Writes the metadata of each tool call to the log (hub supplier, omitted from the tools view).
        dependency ProjectTools to RollingFileLogger;

        // Makes the tool packs, confines them with the path policy, and returns tool results, with images, and refusals.
        dependency ProjectTools to AgentKit;

        // Derives the function type of each tool and carries the picture in the answer.
        dependency ProjectTools to ExtensionsAi;

        // The most bytes that project_write writes into one file.
        attribute maxWriteBytes : Integer;

        // The most pixels on the longer side of the picture that render_image returns.
        attribute maxImageEdge : Integer;

        // The most files that project_list lists in one answer.
        attribute maxListedFiles : Integer;
    }
}
