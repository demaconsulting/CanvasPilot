package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def PathHelpers {
        doc /* Safe path-combination utility guarding against path-traversal. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasPilot/Utilities/PathHelpers.cs");
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/Utilities/PathHelpersTests.cs");
            designFiles = ("docs/design/canvas-pilot/utilities/path-helpers.md");
            verificationFiles = ("docs/verification/canvas-pilot/utilities/path-helpers.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/utilities/path-helpers.yaml");
        }
    }
}
