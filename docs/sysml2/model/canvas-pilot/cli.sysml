package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def CliSubsystem {
        doc /* Command-line argument parsing, output-channel management, and exit-code tracking. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/Cli/CliSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/cli.md");
            verificationFiles = ("docs/verification/canvas-pilot/cli.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/cli.yaml");
        }

        part context : Context;
    }
}
