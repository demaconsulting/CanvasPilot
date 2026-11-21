package CanvasPilot_StyleKitArchive {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_PathHelpers::*;
    private import CanvasPilot_StyleKitDocument::*;

    part def StyleKitArchive {
        doc /* Writes a style kit as a zip file and reads a kit from a zip file that may come from anywhere, refusing as a whole an archive with a name that could write outside the target, a link, a file that is not part of a kit, too many or too large files, or sizes and checksums that do not match, and writing into a hidden folder that is moved into place only for a valid kit. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleImportConflict.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleImportResult.cs",
                "src/DemaConsulting.CanvasPilot/StyleSubsystem/StyleKitArchive.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/StyleSubsystem/StyleKitArchiveTests.cs");
            designFiles = ("docs/design/canvas-pilot/style-subsystem/style-kit-archive.md");
            verificationFiles = ("docs/verification/canvas-pilot/style-subsystem/style-kit-archive.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/style-subsystem/style-kit-archive.yaml");
        }

        // Writes the style file of an exported kit and reads the style file of an imported one with its rules.
        dependency StyleKitArchive to StyleKitDocument;

        // Joins every name that comes from an archive to a folder with the safe combination.
        dependency StyleKitArchive to PathHelpers;
    }
}
