package CanvasPilot_UtilitiesSubsystem {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_PathHelpers::*;

    part def UtilitiesSubsystem {
        doc /* Shared, independently testable helper functions used by other subsystems. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/UtilitiesSubsystem/UtilitiesSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/utilities-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/utilities-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/utilities-subsystem.yaml");
        }

        part pathHelpers : PathHelpers;
    }
}
