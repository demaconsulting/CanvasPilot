package CanvasPilot_ThemeResources {
    private import SoftwareItemMetadata::*;

    part def ThemeResources {
        doc /* The design tokens of the user interface (colors, spacing, corner radii, type sizes, and shadows) for the dark and the light theme variant, the line icons, the control themes, and the palettes that make the standard controls follow the brand. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/Styles/Tokens.axaml",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/Styles/Icons.axaml",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/Styles/Controls.axaml",
                "THIRD-PARTY-NOTICES.md"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.UiTests/ThemeResourcesViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ControlThemesViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/app-shell-subsystem/theme-resources.md");
            verificationFiles = ("docs/verification/canvas-pilot/app-shell-subsystem/theme-resources.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/app-shell-subsystem/theme-resources.yaml");
        }
    }
}
