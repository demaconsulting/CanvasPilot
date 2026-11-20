package CanvasPilot {
    private import SoftwareItemMetadata::*;

    part def LoggingSubsystem {
        doc /* Local rolling log files for user-attachable bug reports. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/LoggingSubsystem/LoggingSubsystemTests.cs");
            designFiles = ("docs/design/canvas-pilot/logging-subsystem.md");
            verificationFiles = ("docs/verification/canvas-pilot/logging-subsystem.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/logging-subsystem.yaml");
        }

        part rollingFileLogger : RollingFileLogger;
    }
}
