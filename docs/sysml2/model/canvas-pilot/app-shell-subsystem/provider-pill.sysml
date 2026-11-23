package CanvasPilot_ProviderPill {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_AgentContract::*;
    private import CanvasPilot_ChatPanel::*;
    private import CanvasPilot_SettingsService::*;

    part def ProviderPill {
        doc /* The pill in the title bar of the main window that names the AI provider and the model that the settings choose and says with a few words and a colored dot (green, amber, red, or a hollow ring while it is checked) whether the provider can be used, from the same check as the readiness banner of the chat panel, with the sentence of that check as its tooltip; using it opens the settings on the AI provider page. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/ProviderPillViewModel.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/ProviderPillView.axaml",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/ProviderPillView.axaml.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/AppShellSubsystem/ProviderPillTests.cs");
            designFiles = ("docs/design/canvas-pilot/app-shell-subsystem/provider-pill.md");
            verificationFiles = ("docs/verification/canvas-pilot/app-shell-subsystem/provider-pill.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/app-shell-subsystem/provider-pill.yaml");
        }

        // Follows the readiness check that the chat panel runs and reports whether a copilot exists.
        dependency ProviderPill to ChatPanel;

        // Reads the readiness status of the check, whose results it puts into words.
        dependency ProviderPill to AgentContract;

        // Reads the chosen provider and model and follows their changes.
        dependency ProviderPill to SettingsService;
    }
}
