### StyleChecker

#### Verification Approach

`StyleChecker` is verified with unit tests in `StyleCheckerTests.cs` over pixel buffers that the tests make, so that
every expected share is known exactly: stripes of the palette colors, a block of a color that is not in the palette, a
color just inside and just outside the tolerance, the edge of an anti-aliased shape and a ramp of such an edge, a
gradient to a color that is not in the palette, empty and partly see-through pixels, a tiny share, twelve far-apart
colors, a mixture of all kinds, the same pixels in another order, a picture with nothing in it, and a large picture
(2048 by 2000 pixels). The color arithmetic is compared with the published values of
CIELAB for white, black, a gray, and red. The boundary of the tolerance is found with the same arithmetic as the check,
on a kit that has only white so that no line between two palette colors can make a blend. The kits that come with the
program are checked on their nine example pictures, which are really drawn by the render service, in
`BuiltInStyleKitTests.cs`. A subsystem test draws two files and checks them against a kit that the catalog finds. The
command that the Image menu runs is verified in `StyleCheckCommandTests.cs` with a hand-written source of pictures,
active style, and sink of notes, and the real checker; the last test runs it under a synchronization context that counts
what is posted to it.

#### Test Environment

N/A - standard test environment. The test of the large picture allocates about 16 MB. No test measures time or memory;
the cost of the check is a design property that is recorded in the design of the unit.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Stripes of exact palette colors give each role its share, with no blend and nothing off the palette, and pass; the
  roles are in the order of their shares.
- A tenth of a picture in a color that is not in the palette is listed with that exact color, a share of 0.10 (to a
  thousandth), the nearest role, color, and difference (equal to the arithmetic of the test), and the picture fails.
- A color within the tolerance of a palette color is that palette color and the picture passes; the next color outside
  is off the palette, within one and a half units of the tolerance, and the picture fails; a larger tolerance accepts
  it.
- A column of the color half way between two palette colors, and a ramp of eight columns, are blended edges with the
  exact share, and nothing is off the palette.
- A gradient from white to a color that is not in the palette fails with most of the picture off the palette.
- Pixels with an alpha below 16 are not counted but are counted as empty; a pixel with alpha 16 is counted; partly
  see-through pixels are flattened over the background of the kit before they are measured.
- A color off the palette with less than a hundredth of the pixels is not listed but is in the share off the palette and
  in the other share; at most eight colors are listed, the largest first, and the others are summed; the shares of the
  palette colors, the blends, and the colors off the palette add up to 1 to nine decimals.
- The same pixels, and the same pixels in another order, give reports with the same text and the same notes.
- No picture, one pixel on the palette, one pixel off it, and one empty pixel are handled.
- The notes of a failing picture are `style-summary`, `style-ok`, `style-blend` (information) and `style-off-palette` (a
  warning), and the text has the four lines of the design; a passing picture has no line for colors off the palette and
  says "the picture" when it has no file name.
- A large picture of 2048 by 2000 pixels in stripes has every pixel counted and the exact share of the color that is off
  the palette.
- Each of the nine example pictures of the kits that come with the program passes against its own kit with less than a
  thousandth of its pixels off the palette. The largest value measured was 0.045 percent (the flowchart of the kit DEMA,
  with 13.5 percent of blended edges, which is the share of the anti-aliased text and lines); the bound is that value
  rounded up to the next thousandth. The other eight were between 0 and 0.022 percent, with blended edges between 0.8
  and 12.9 percent.
- The options have the values of the design and refuse a limit that is not a number in its range; the methods refuse a
  missing picture or kit, a negative size, pixels that do not fit the size, a limit out of range, and a kit with no
  color.
- The command of the user checks the shown picture against the kit of the project on a worker thread, applies the notes
  through the context of the caller, shows nothing and says why when there is no picture, no kit, or a kit that cannot
  be found, drops the result of a picture that was replaced, answers with a sentence when the check fails, and announces
  when a picture can be checked.

#### Test Scenarios

**ColorMath_ToLab_KnownColors_AreCloseToTheReferenceValues**: White is 100, 0, 0; black is 0; a gray of 119 has a
lightness of 50; pure red is 53.24, 80.09, 67.20 to the places that are tested; the difference between white and black
is 100. This scenario is tested by `ColorMath_ToLab_KnownColors_AreCloseToTheReferenceValues`.

**ColorMath_BlendDistance_PointOnBesideAndFarFromTheLine_IsZeroSmallAndInfinite**: A point on the line from white to
blue is at distance 0, a point a little beside it is within a few units, a point that is far in sRGB is not near, and a
line that is a point is not near. This scenario is tested by
`ColorMath_BlendDistance_PointOnBesideAndFarFromTheLine_IsZeroSmallAndInfinite`.

**StyleChecker_Check_StripesOfPaletteColors_AreOnThePaletteAndPass**: Four stripes of 40, 30, 20, and 10 percent in the
four palette colors give those shares for `background`, `text`, `primary`, and `accent`, with 3,000 pixels counted, no
blend, and nothing off the palette. This scenario is tested by
`StyleChecker_Check_StripesOfPaletteColors_AreOnThePaletteAndPass`.

**StyleChecker_Check_TenPercentRedBlock_ReportsColorShareAndNearestRole**: A white picture with a tenth in `#D32F2F`
lists that color with its share, the nearest role and color and the difference of the arithmetic, and fails. This
scenario is tested by `StyleChecker_Check_TenPercentRedBlock_ReportsColorShareAndNearestRole`.

**StyleChecker_Check_ColorAroundTheTolerance_IsOnThePaletteInsideAndOffJustOutside**: The color of the last step inside
the tolerance of white is white and passes; the color of the first step outside it is off the palette with a difference
a little over 10 and fails; a tolerance of 20 accepts it. This scenario is tested by
`StyleChecker_Check_ColorAroundTheTolerance_IsOnThePaletteInsideAndOffJustOutside`.

**StyleChecker_Check_AntiAliasedEdgeBetweenTwoPaletteColors_IsBlendedNotOffPalette**: One column of the color half way
between white and the blue is a hundredth blended; a ramp of eight columns is eight hundredths blended; in both nothing
is off the palette and the picture passes. This scenario is tested by
`StyleChecker_Check_AntiAliasedEdgeBetweenTwoPaletteColors_IsBlendedNotOffPalette`.

**StyleChecker_Check_GradientToAColorOffThePalette_Fails**: A ramp from white to red has most of its pixels off the
palette and fails. This scenario is tested by `StyleChecker_Check_GradientToAColorOffThePalette_Fails`.

**StyleChecker_Check_TransparentPixels_AreIgnoredAndCounted**: Fifty of a hundred pixels (alpha 0 and alpha 15, in red)
are not counted and are reported as transparent; the others, white at alpha 16 and the blue, are on the palette. This
scenario is tested by `StyleChecker_Check_TransparentPixels_AreIgnoredAndCounted`.

**StyleChecker_Check_TranslucentPixels_AreFlattenedOverTheBackground**: Red at alpha 20 is the background after it is
flattened, and the blue at alpha 128 is a blended edge. This scenario is tested by
`StyleChecker_Check_TranslucentPixels_AreFlattenedOverTheBackground`.

**StyleChecker_Check_TinyOffPaletteShare_IsCountedNotListed**: One column of red in 200 is not listed, is half a
hundredth of the picture in the off-palette share and in the other share, and the picture passes. This scenario is
tested by `StyleChecker_Check_TinyOffPaletteShare_IsCountedNotListed`.

**StyleChecker_Check_TwelveOffPaletteColors_ListCappedAtEightWithOtherShare**: Twelve colors in stripes of 12 to 1
columns give a list of the eight widest in order, the other four summed, and the whole picture off the palette. This
scenario is tested by `StyleChecker_Check_TwelveOffPaletteColors_ListCappedAtEightWithOtherShare`.

**StyleChecker_Check_MixedPicture_SharesAddUpToOne**: A picture with palette colors, an edge, and red has shares that
add up to 1, and the listed shares and the other share add up to the share off the palette. This scenario is tested by
`StyleChecker_Check_MixedPicture_SharesAddUpToOne`.

**StyleChecker_Check_SameOrShuffledPixels_GiveEqualReports**: The same pixels checked twice, and in a shuffled order,
give the same text and the same notes. This scenario is tested by
`StyleChecker_Check_SameOrShuffledPixels_GiveEqualReports`.

**StyleChecker_Check_EmptyAndOnePixelPictures_AreHandled**: A picture of no pixels, one pixel on the palette, one off
it, and one empty pixel each give the expected report. This scenario is tested by
`StyleChecker_Check_EmptyAndOnePixelPictures_AreHandled`.

**StyleCheckReport_ToNotesAndToText_FailingPicture_HaveTheShapeOfTheDesign**: A rendered picture with an edge and red
gives four notes with the codes and severities of the design and the sentences, and four lines of text. This scenario is
tested by `StyleCheckReport_ToNotesAndToText_FailingPicture_HaveTheShapeOfTheDesign`.

**StyleCheckReport_ToText_PassingPictureWithoutName_HasNoOffPaletteLine**: A passing picture says "the picture", has no
line for colors off the palette, and has two notes. This scenario is tested by
`StyleCheckReport_ToText_PassingPictureWithoutName_HasNoOffPaletteLine`.

**StyleChecker_Check_LargePicture_CountsEveryPixelAndTheOffPaletteShare**: A picture of 2048 by 2000 pixels in stripes
of the palette colors and a color off the palette is counted in full, and the share off the palette is the exact share
of the columns of that color. This scenario is tested by
`StyleChecker_Check_LargePicture_CountsEveryPixelAndTheOffPaletteShare`.

**StyleCheckOptions_DefaultsAndValidate_AreTheOnesOfTheDesign**: The defaults are 10, 8, 5, 0.01, and 0.05 and eight
listed colors; each limit that is negative, above its range, or not a number is refused. This scenario is tested by
`StyleCheckOptions_DefaultsAndValidate_AreTheOnesOfTheDesign`.

**StyleChecker_Check_InvalidArguments_Throw**: A missing picture or kit, a negative size, pixels of the wrong length, a
limit out of range, and a kit with no color are refused. This scenario is tested by
`StyleChecker_Check_InvalidArguments_Throw`.

**BuiltInStyleKits_Examples_AreOnTheirOwnKitsPalette**: Each of the nine examples of the kits that come with the program
is drawn and checked against its own kit: it passes and less than a thousandth of it is off the palette. This scenario
is tested by `BuiltInStyleKits_Examples_AreOnTheirOwnKitsPalette`.

**StyleCheckCommand_RunAsync_NoPicture_ReturnsMessageAndShowsNothing**: With a kit and no picture the command cannot
check, and answers with a sentence that tells the user that there is no picture. This scenario is tested by
`StyleCheckCommand_RunAsync_NoPicture_ReturnsMessageAndShowsNothing`.

**StyleCheckCommand_RunAsync_NoKitOrMissingKit_ReturnsMessageAndShowsNothing**: With a picture and no style kit the
sentence says that the project has no style kit and points at the status bar; with a kit that cannot be found it names
the kit; no notes are shown in either case. This scenario is tested by
`StyleCheckCommand_RunAsync_NoKitOrMissingKit_ReturnsMessageAndShowsNothing`.

**StyleCheckCommand_RunAsync_PictureOnThePalette_ShowsPassedNotes**: A picture of white and blue, which the kit has, is
checked by the real checker and the notes of the group `style` say that it passed and have no color off the palette.
This scenario is tested by `StyleCheckCommand_RunAsync_PictureOnThePalette_ShowsPassedNotes`.

**StyleCheckCommand_RunAsync_PictureOffThePalette_ShowsFailedNotesWithTheColor**: A picture of which a quarter is a red
that the kit does not have is checked: the notes say that it failed, and a warning names the red. This scenario is
tested by `StyleCheckCommand_RunAsync_PictureOffThePalette_ShowsFailedNotesWithTheColor`.

**StyleCheckCommand_RunAsync_PictureReplacedDuringTheCheck_ShowsNothing**: The shown picture is replaced as soon as the
command has asked for the kit; the check is made and its result is dropped. This scenario is tested by
`StyleCheckCommand_RunAsync_PictureReplacedDuringTheCheck_ShowsNothing`.

**StyleCheckCommand_RunAsync_CheckThrows_ReturnsMessageAndShowsNothing**: A kit with no color makes the checker refuse;
the command answers with a sentence that begins `The picture could not be checked` and shows no notes. This scenario is
tested by `StyleCheckCommand_RunAsync_CheckThrows_ReturnsMessageAndShowsNothing`.

**StyleCheckCommand_CanCheck_PictureComesAndGoes_IsAnnouncedUntilDisposed**: A picture that appears and goes away is
announced and changes `CanCheck`; after disposal (twice) nothing more is announced. This scenario is tested by
`StyleCheckCommand_CanCheck_PictureComesAndGoes_IsAnnouncedUntilDisposed`.

**StyleCheckCommand_RunAsync_CalledUnderAContext_AppliesTheResultThroughIt**: When the command is run under a
synchronization context that counts what is posted to it, the continuation is posted to that context and the notes are
shown. This scenario is tested by `StyleCheckCommand_RunAsync_CalledUnderAContext_AppliesTheResultThroughIt`.

**StyleCheckCommand_Constructor_NullArguments_Throw**: A missing source of pictures, active style, or sink of notes is
refused. This scenario is tested by `StyleCheckCommand_Constructor_NullArguments_Throw`.
