package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def Program {
        doc /* Entry point and execution orchestrator. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasPilot/Program.cs");
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/ProgramTests.cs");
            designFiles = ("docs/design/canvas-pilot/program.md");
            verificationFiles = ("docs/verification/canvas-pilot/program.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/program.yaml");
        }
    }
}
