package CanvasPilot_PathHelpers {
    private import SoftwareItemMetadata::*;

    part def PathHelpers {
        doc /* Safe path-combination utility guarding against path-traversal, and finder of free file names. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasPilot/UtilitiesSubsystem/PathHelpers.cs");
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/UtilitiesSubsystem/PathHelpersTests.cs");
            designFiles = ("docs/design/canvas-pilot/utilities-subsystem/path-helpers.md");
            verificationFiles = ("docs/verification/canvas-pilot/utilities-subsystem/path-helpers.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/utilities-subsystem/path-helpers.yaml");
        }
    }
}
