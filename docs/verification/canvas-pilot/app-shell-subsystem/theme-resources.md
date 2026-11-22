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
- Every vendored icon resolves to a geometry with a shape that lies on the 20 by 20 grid.
- An icon of class `icon` in a button is 16 by 16 and is drawn in the foreground color of the button, inside its box.
- The icon button and the tool toggle are 32 by 32, the pill button is at least 28 by 28, and a chip is at least 24
  high; a checked tool toggle is filled with the accent with white content and an unchecked one is clear.
- The icon button, tool toggle, pill button, and splitter show a 2 px ring in the focus color when focused with the
  keyboard and none when focused with the pointer, and the ring is really drawn just outside the button.
- A splitter draws a 1 px line and a 4 by 40 grip, and the arrow keys resize the column before it in steps of 16.
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

**ThemeResources_Icons_EveryIconResolvesToGeometryOnItsGrid**: Each of the 43 icon keys is looked up and is a geometry
whose bounds are at least one unit wide and high and lie on the 20 by 20 grid of the glyphs. This scenario is tested by
`ThemeResources_Icons_EveryIconResolvesToGeometryOnItsGrid`.

**ThemeResources_Icons_PathIconIsDrawnInTheForegroundOfItsButton**: A button with a red foreground holds an icon of
class `icon` in a window of 100 by 100 that is drawn headless; the icon is 16 by 16, red pixels are found inside its box
and none outside it. This scenario is tested by `ThemeResources_Icons_PathIconIsDrawnInTheForegroundOfItsButton`.

**ThemeResources_Controls_ButtonsHaveTargetsOfAtLeast28**: An icon button, a tool toggle, and a pill button are shown
alone; the first two are 32 by 32 and the pill is at least 28 by 28. This scenario is tested by
`ThemeResources_Controls_ButtonsHaveTargetsOfAtLeast28`.

**ThemeResources_Controls_CheckedToolToggleIsFilledWithAccent**: In both variants a tool toggle is clear while
unchecked, and when it is checked its fill is the accent and its content is white. This scenario is tested by
`ThemeResources_Controls_CheckedToolToggleIsFilledWithAccent`.

**ThemeResources_Controls_ChipBorderIsARaisedPill**: In both variants a border with the chip theme has the raised
surface, the subtle outline, the pill radius, and at least 24 height. This scenario is tested by
`ThemeResources_Controls_ChipBorderIsARaisedPill`.

**ThemeResources_Controls_SplitterGripResizesByKeyboardInStepsOf16**: A splitter in a grid with a column of 200 draws a
1 px line and a 4 by 40 grip; after it is focused the right arrow makes the column 216 and the left arrow 200. This
scenario is tested by `ThemeResources_Controls_SplitterGripResizesByKeyboardInStepsOf16`.

**ThemeResources_Controls_FocusRingShowsForKeyboardFocusOnly**: In both variants each of the four controls has a
transparent ring when it is focused with the pointer and a 2 px ring in the focus color when it is focused with the
keyboard. This scenario is tested by `ThemeResources_Controls_FocusRingShowsForKeyboardFocusOnly`.

**ThemeResources_Controls_FocusRingIsDrawnOutsideTheButton**: After keyboard focus an icon button is drawn headless and
the pixel just above the middle of its top edge has the focus color in both variants, so the ring is not clipped away.
This scenario is tested by `ThemeResources_Controls_FocusRingIsDrawnOutsideTheButton`.
**ThirdPartyNotices_Published**: After the Windows build job publishes the application self-contained for win-x64,
FileAssert asserts that the published folder, which the installer takes whole, holds exactly one
`THIRD-PARTY-NOTICES.md` that names Fluent System Icons and the MIT License. This scenario is tested by
`ThirdPartyNotices_Published`, which is run in CI only.
