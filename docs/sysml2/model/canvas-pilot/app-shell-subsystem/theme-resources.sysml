package CanvasPilot_ThemeResources {
    private import SoftwareItemMetadata::*;

    part def ThemeResources {
        doc /* The design tokens of the user interface (colors, spacing, corner radii, type sizes, and shadows) for the dark and the light theme variant, the line icons, the control themes and shared looks (icon, tool, pill, main, quiet, and card buttons, chip, splitter, project tree row, card, headings, and banner), and the palettes that make the standard controls follow the brand. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/ThemeVariantApplier.cs",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/Styles/Tokens.axaml",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/Styles/Icons.axaml",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/Styles/Controls.axaml",
                "src/DemaConsulting.CanvasPilot/AppShellSubsystem/Styles/ThemeConverters.cs",
                "THIRD-PARTY-NOTICES.md"
            );
            testFiles = (
                "test/DemaConsulting.CanvasPilot.UiTests/ThemeResourcesViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ControlThemesViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/ThemeVariantApplierViewTests.cs",
                "test/DemaConsulting.CanvasPilot.UiTests/AccessibilityViewTests.cs"
            );
            designFiles = ("docs/design/canvas-pilot/app-shell-subsystem/theme-resources.md");
            verificationFiles = ("docs/verification/canvas-pilot/app-shell-subsystem/theme-resources.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/app-shell-subsystem/theme-resources.yaml");
        }
    }
}
