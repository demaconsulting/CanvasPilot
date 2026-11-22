### ThemeResources

![AppShell Structure](AppShellSubsystemView.svg)

#### Purpose

`ThemeResources` is the one place that says how CanvasPilot looks. It holds the design tokens (colors, spacing, corner
radii, type sizes, and shadows) for the dark and the light theme variant, the line icons, the control themes, and the
palettes that make the standard controls of the Fluent theme follow the brand. Every view takes its look from these
resources, so the look of the application changes in one place and a change of the theme variant repaints what is on the
screen.

#### Data Model

**Tokens** (`AppShellSubsystem/Styles/Tokens.axaml`): one resource dictionary that the application merges after the
Fluent theme. It has two theme dictionaries, `Dark` and `Light`, that hold what differs per variant, and the keys
that are the same in both variants at the top level. All keys start with `Cp`.

- *Surfaces*: `CpSurfaceWindow` (title bar and status bar), `CpSurfacePane` (explorer, chat, dialogs),
  `CpSurfaceStage` (around the picture), `CpSurfaceRaised` (cards, composer, inputs), and `CpSurfaceOverlay`
  (floating bars, menus, and tooltips).
- *Lines*: `CpBorderSubtle` (separators), `CpBorderStrong` (decorative outlines), and `CpBorderControl` (the outline
  that shows where an input ends; at least 3 to 1 on the pane and on raised surfaces).
- *Text*: `CpTextPrimary`, `CpTextSecondary`, and `CpTextTertiary` (hints; never below 11 px, never on overlays or on
  tinted rows).
- *Accent*: `CpAccent` and `CpTextOnAccent` (the same in both variants), `CpAccentText`, `CpAccentTint` and
  `CpAccentTintHover` (selected and hovered rows), `CpFocusRing`, `CpAccentGradient` (the brand gradient), and the
  mark colors `CpMark` and `CpMarkTint`.
- *State*: `CpSuccess`, `CpWarning`, and `CpDanger`, for text and icons, always paired with a word or a shape.
- *Scales*: spacing `CpSpace1` to `CpSpace7` (4, 8, 12, 16, 24, 32, and 48), radii `CpRadiusKey`, `CpRadiusRow`,
  `CpRadiusInput`, `CpRadiusCard`, `CpRadiusWelcome`, and `CpRadiusPill`, type sizes `CpFontCaption` to
  `CpFontDisplay` (11, 12, 13, 14, 16, 20, and 34) with line heights `CpLineCaption` to `CpLineDisplay`, and
  shadows `CpShadowFloating`, `CpShadowDialog`, and `CpShadowPicture`.

**Icons** (`AppShellSubsystem/Styles/Icons.axaml`): a style file with one `StreamGeometry` resource per glyph, named
`CpIcon` and the name of the glyph (`CpIconAdd`, `CpIconFolderOpen`, `CpIconSparkle`, and so on; 43 glyphs for the
commands, files, states, tools, and navigation of the views), and one style, `PathIcon.icon`, that makes an icon 16 by
16. A view draws an icon with a `PathIcon` of class `icon` whose `Data` is a dynamic resource of the glyph; the
`PathIcon` fills the geometry with the foreground of the control around it, so the icon takes the color of its button,
on hover and when disabled too. The glyphs are outlines to be filled, not strokes. They are vendored from Fluent System
Icons (20 px, regular, MIT license, copyright Microsoft Corporation), rounded to two decimals, and are not an OTS
package: they are assets in the source, like the logo, so no OTS item exists for them. The license notice is in the
header of the file and in `THIRD-PARTY-NOTICES.md` at the root of the repository, which the project copies beside the
executable (build and publish output), so the installer, which takes the published folder whole, ships it. An icon
button that has no text must have an automation name and a tooltip; this is the duty of the view that uses the icon.

**Control themes** (`AppShellSubsystem/Styles/Controls.axaml`): keyed control themes that more than one view uses,
each with its own template (none is based on the Fluent theme, so no state of the stock look shows through) and built
on the tokens:

- `IconButton` (a `Button`) and `ToolToggle` (a `ToggleButton`): at least 32 by 32, no fill at rest, the accent tint on
  hover and press, 40 percent opacity when disabled; a checked `ToolToggle` is filled with the accent and its content
  is white. The icon button has no text, so the view must give it an automation name and a tooltip.
- `PillButton` (a `Button`): at least 28 high, fully rounded, raised surface with a subtle outline that turns to the
  accent on hover.
- `ChipBorder` (a `Border`): the raised, outlined, fully rounded shape of a chip, at least 24 high.
- `SplitterGrip` (a `GridSplitter`): a 1 px line with a 4 by 40 grip centered in a hit area that the view sets (9 px in
  the workspace); the line is 2 px in the accent while the pointer is over it or it is dragged (the thumb has the
  `:pressed` state, there is no separate dragging state); the keyboard increment is 16 px. The theme is for a vertical
  splitter between two columns.

Each theme except the chip shows the keyboard focus the same way: a template part `PART_FocusRing`, a 2 px border
that is transparent at rest and has the focus ring color while the control has the pseudo-class `:focus-visible` (focus
by the keyboard, never by the pointer). The ring lies 1 px outside the control, so the themes switch off the clipping
of the control to its bounds, and the default focus adorner of Avalonia is switched off so that two rings do not show.
A view that puts such a control in a container must not clip the container to its bounds if the ring is to be seen.

**Palettes** (`App.axaml`): `FluentTheme.Palettes` has one `ColorPaletteResources` per variant that sets the system
accent to the brand accent, the region color (the window surface of dialogs) to the pane surface, the main text color
to the primary text color, and the error text color to the danger color. This makes the standard controls follow the
brand without overriding single brushes.

**ThemeVariantApplier** (`AppShellSubsystem/ThemeVariantApplier.cs`): a small class that makes the application show the
theme that the settings choose. It is created by the composition root for a running application (the `application`
argument of `ApplicationComposition.Create`), applies `AppSettings.Appearance.Theme` at once, and applies it again on
every `ISettingsService.Changed`. `ToVariant` maps the choices: `Dark` and `Light` are the Avalonia variants of the same
name, and `FollowSystem` is `ThemeVariant.Default`, which lets the operating system decide and follow. A change that
comes from a thread other than the user interface thread (the settings service raises its event on the thread that
changed the settings) is posted to the user interface thread, and a variant that is already requested is not set again.
`Dispose` stops following; the composition disposes it with the other units.

**Contrast**: every text color is at least 4.5 to 1 on the surfaces it is documented for, and the focus ring, the
input outline, and the mark color are at least 3 to 1 on the pane and on raised surfaces. The light warning color is a
little darker than the first proposal of the design (`#a84e08` instead of `#b45309`) so that it reaches 4.5 to 1 on the
window surface as well, and the input outline has its own token because the decorative strong border of the design is
only about 1.5 to 1.

#### Key Methods

**Resolution**: Views refer to a token with a dynamic resource, which looks the key up in the theme variant of the
control that asks. Changing the requested theme variant of the application or of a window therefore changes every
control that uses a token, with no reload. The palettes and the token dictionary are loaded once, when the
application is created.

#### Error Handling

N/A - the unit is declarative resources and has no operations that can fail. A key that is missing would show as a
missing value, which the tests that resolve every key in both variants prevent. A geometry that does not parse stops the
application when it loads the style file, which the headless tests would show at once.

#### Dependencies

- **Avalonia** and the Fluent theme — resource dictionaries, theme variants, and the palettes of the theme.
- **SettingsService** — `ISettingsService` and `AppearanceSettings`, from which `ThemeVariantApplier` reads the theme
  choice and whose change event it follows.

#### Callers

- Every view of the application, through dynamic resources. `App` merges the dictionary and sets the palettes.
- The composition root, which creates the `ThemeVariantApplier` when it is given the running application.
