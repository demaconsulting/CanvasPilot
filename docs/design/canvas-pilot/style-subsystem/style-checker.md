### StyleChecker

![Style Structure](StyleSubsystemView.svg)

#### Purpose

`StyleChecker` measures the colors of a picture against the palette of a style kit and reports how much of the picture
is on the palette, how much is a blend of palette colors at an edge, and which colors are off the palette, with the
share of each and the palette color that is nearest. A style is kept only when departures from it can be seen, and the
report tells the person (and, later, an assistant) what to change. The unit works on the pixels that the application
already holds for a rendered picture, reads no file, and has no user interface.

#### Data Model

- `StyleCheckOptions`: the limits, all changeable in this one place: the **tolerance** (color difference 10) within
  which a color counts as a palette color, the **cluster difference** (8) within which colors that are off the palette
  are one color, the **blend tolerance** (5) from a line between two palette colors, the **minimum share** (1 percent)
  for an off-palette color to be listed, and the **limit** (5 percent) of the picture that may be off the palette. At
  most 8 colors are listed (`MaxListed`), and a pixel with an alpha below 16 is empty (`TransparentBelowAlpha`).
  `Validate` refuses a limit that is not a number in its range.
- `StyleCheckReport`: the kit id and name, the size of the picture, the number of counted and of empty pixels, the
  palette colors that are used (`StylePaletteUse`: role, color, share, the largest first), the share of blended edges,
  the colors off the palette that are listed (`StyleOffPaletteColor`: color, share, nearest role and color, difference,
  the largest first), the share of the other colors off the palette, the share off the palette, and whether the picture
  passed. The shares are of the counted pixels, and the three kinds add up to 1.
- `ColorMath` (internal): sRGB to CIELAB with the white of D65, the CIE76 difference (the Euclidean distance in CIELAB),
  and the difference between a color and the nearest point of the straight line between two colors.

#### Key Methods

**Check**: Takes a rendered picture (or its straight-alpha RGBA bytes with a width and a height), a kit, and the
options, and returns the report. It refuses a missing kit, a size that does not fit the bytes, a limit out of range, and
a kit with no color in the form `#RRGGBB`. The steps:

1. The palette is read in the order of the kit and each color is converted to CIELAB once.
2. The pixels are counted in one pass into a histogram of 32 levels per channel (32,768 buckets: the number of pixels
   and the sums of the three channels). A pixel with an alpha below 16 is counted as empty. A pixel with a larger alpha
   that is not fully opaque is flattened over the color of the role `background` (white when there is none) with
   rounding. A run of equal pixels is added in one step, so flat areas cost almost nothing. All of this is integer
   arithmetic, so the result does not depend on the order of the pixels.
3. Each occupied bucket is given the mean color of its pixels. When the difference to its nearest palette color is
   within the tolerance, its pixels are credited to that color (a tie goes to the color that comes first in the
   palette). Otherwise, when the color is within the blend tolerance of the straight line in sRGB between two palette
   colors, its pixels are blended edge. Otherwise they are off the palette.
4. The buckets off the palette are taken together, the largest first (a tie goes to the lower key): a bucket joins the
   first color whose first bucket is within the cluster difference, and otherwise starts a color. The color of a cluster
   is the mean of its pixels. A cluster whose share is at least the minimum is listed, at most eight of them, the
   largest first; all the others are added to the other share.
5. The picture passes when the share that is off the palette, listed or not, is within the limit. A picture with no
   counted pixel has no share and passes.

**ToNotes**: The report as notes for the list of notes of a picture: `style-summary` (information: the result sentence),
`style-ok` (information: the palette colors used), `style-blend` (information: the blended edges, when there are any),
and one `style-off-palette` warning for each listed color and one for the other colors, if any. Every code starts with
`style-`, the name of the group in the sense of `IRenderNoteSink`.

**ToText**: The report as up to four lines of text: what was checked and how many pixels were counted; the result with
the share off the palette and the limit; the palette colors that are used and the blended edges; and the colors off the
palette with the nearest palette color of each and the advice to use the palette colors.

**The choice of the method.** CIE76 is used and not CIEDE2000 because the check is a threshold on whether a color is
near one of at most a few dozen palette colors, CIE76 has no special cases and gives the same answer on every machine,
and its habit of over-stating differences between saturated blues only makes the check slightly stricter. The public
image inspector of CanvasNet (`ImageInspector`) was evaluated and is not used: it works from the path of a file (a
second render under a second access policy that would have to be kept equal), it merges the near colors of one family
into a mean and lists at most 32 colors, which is right for the dominant colors of a picture and wrong for the question
whether a color is in a palette.

**Known limit.** A smooth gradient between two palette colors lies on the line between them, so it counts as blended
edges and not as colors off the palette; a gradient to a color that is not in the palette is off the palette.

#### Error Handling

The methods throw `ArgumentNullException` for a missing picture or kit, `ArgumentOutOfRangeException` for a negative
size or a limit out of its range, and `ArgumentException` for pixels that do not fit the size and for a kit with no
usable color. Nothing else fails: every picture gets a report.

#### Dependencies

- **StyleKitDocument** unit — the kit and the reading of its colors (`StyleColors`).
- **Preview** subsystem — `RenderedImage` (the pixels) and `RenderDiagnostic` (the notes).

#### Callers

- The command that checks the shown picture and `StyleTools` (later steps) — check a picture and show or return the
  report.
- The tests of the built-in kits — check that their examples are on their own palette.
