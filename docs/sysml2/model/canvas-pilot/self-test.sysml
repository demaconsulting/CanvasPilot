package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def SelfTestSubsystem {
        doc /* Self-validation framework exercising the tool's own capabilities on demand. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/SelfTest/SelfTestSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/self-test.md");
            verificationFiles = ("docs/verification/canvas-pilot/self-test.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/self-test.yaml");
        }

        part validation : Validation;
    }
}
