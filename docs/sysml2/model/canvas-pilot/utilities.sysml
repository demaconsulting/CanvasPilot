package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def UtilitiesSubsystem {
        doc /* Shared, independently testable helper functions used by other subsystems. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/Utilities/UtilitiesSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/utilities.md");
            verificationFiles = ("docs/verification/canvas-pilot/utilities.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/utilities.yaml");
        }

        part pathHelpers : PathHelpers;
    }
}
