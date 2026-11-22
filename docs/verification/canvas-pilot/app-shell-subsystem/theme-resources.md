### ThemeResources

#### Verification Approach

`ThemeResources` is verified by headless UI tests in `ThemeResourcesViewTests.cs`. They read the resources of the real
running application (the real `App` with the Fluent palettes and the merged token dictionary) in each theme variant, so
nothing is substituted. The contrast ratios are computed from the resolved colors with the formula of the Web Content
Accessibility Guidelines; tints are laid over the surface they are drawn on first. The tests state facts about what the
user sees (a key resolves, a color is readable, a control repaints) and never read the text of the resource files.

#### Test Environment

The tests run under the Avalonia headless platform with Skia and the embedded Inter font, serialized with test
parallelization disabled; no display is required.

#### Acceptance Criteria

- All UI tests pass with zero failures.
- Every color, shadow, spacing, radius, and type token resolves with its documented type in the dark and the light
  variant, the surface and text colors differ between the variants, and the spacing scale is 4, 8, 12, 16, 24, 32,
  and 48.
- Primary and secondary text, accent text, and white on the accent are at least 4.5 to 1 on each surface they are
  documented for; the tertiary and state colors are at least 4.5 to 1 on the window, pane, and raised surfaces.
- The focus ring, the input outline, and the mark color are at least 3 to 1 on the pane and raised surfaces.
- A border that takes its background from a token shows the color of the requested variant, and the color of the other
  variant after the window switches, without being created again.

#### Test Scenarios

**ThemeResources_Tokens_ResolveInBothVariants**: Every token is looked up in the dark and the light variant and has its
documented type; the surfaces, the main text color, and the focus ring differ between the variants. This scenario is
tested by `ThemeResources_Tokens_ResolveInBothVariants`.

**ThemeResources_Tokens_TextColorsMeetContrastOnTheirSurfaces**: The contrast of each text color on each of its
surfaces, of white on the accent, and of the text colors on a selected row is computed in both variants and is at least
4.5 to 1. This scenario is tested by `ThemeResources_Tokens_TextColorsMeetContrastOnTheirSurfaces`.

**ThemeResources_Tokens_NonTextColorsMeetContrast**: The contrast of the focus ring, the input outline, and the mark
color on the pane and raised surfaces is at least 3 to 1 in both variants. This scenario is tested by
`ThemeResources_Tokens_NonTextColorsMeetContrast`.

**ThemeResources_DynamicResource_FollowsVariantSwitch**: A border bound to the pane token inside a window that asks for
dark shows the dark pane color; after the window asks for light it shows the light pane color. This scenario is tested
by `ThemeResources_DynamicResource_FollowsVariantSwitch`.
