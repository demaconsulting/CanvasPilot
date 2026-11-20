package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def Context {
        doc /* Command-line argument parser and I/O owner for one tool invocation. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasPilot/Cli/Context.cs");
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/Cli/ContextTests.cs");
            designFiles = ("docs/design/canvas-pilot/cli/context.md");
            verificationFiles = ("docs/verification/canvas-pilot/cli/context.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/cli/context.yaml");
        }
    }
}
