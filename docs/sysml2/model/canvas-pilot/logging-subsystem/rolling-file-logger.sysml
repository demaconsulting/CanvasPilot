package CanvasPilot_RollingFileLogger {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_PathHelpers::*;

    part def RollingFileLogger {
        doc /* Writes application events to local size-rolling log files. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasPilot/LoggingSubsystem/RollingFileLogger.cs");
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/LoggingSubsystem/RollingFileLoggerTests.cs");
            designFiles = ("docs/design/canvas-pilot/logging-subsystem/rolling-file-logger.md");
            verificationFiles = ("docs/verification/canvas-pilot/logging-subsystem/rolling-file-logger.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/logging-subsystem/rolling-file-logger.yaml");
        }

        // Resolves its log file paths with the safe path-combination utility so a malformed
        // file name cannot escape the log folder.
        dependency RollingFileLogger to PathHelpers;
    }
}
