package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def Validation {
        doc /* Orchestrates the self-validation test suite and reports pass/fail results. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasPilot/SelfTest/Validation.cs");
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/SelfTest/ValidationTests.cs");
            designFiles = ("docs/design/canvas-pilot/self-test/validation.md");
            verificationFiles = ("docs/verification/canvas-pilot/self-test/validation.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/self-test/validation.yaml");
        }
    }
}
