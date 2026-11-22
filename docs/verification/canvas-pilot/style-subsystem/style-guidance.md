### StyleGuidance

#### Verification Approach

`StyleGuidance` is verified with unit tests in `StyleGuidanceTests.cs`. They write the block for the kits that come with
the program, read from the folder that is copied beside the test assembly the way it is copied beside the executable,
and for kits that the tests build in memory: a kit of a middle size whose notes, variables, examples, and configuration
the test chooses, a kit with the most of everything that the format allows, a kit with nothing optional, and a kit whose
text tries to break the block. The text of the kit `clean` is compared with a golden file in the test project, after the
folder of the kit is written as `{KIT}` and the line breaks and the path separators are made the same on every machine.
The size of the block is asserted for the three kits that come with the program and for the largest kit; the size that
was measured is recorded below.

#### Test Environment

N/A - standard test environment. The tests read files copied beside the test assembly (the kits of the program and the
golden file); no network or display is needed.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The block for `clean` is the golden text.
- For each kit that comes with the program the block has at most 4,000 characters without being cut, and has every color
  with its role, the frontmatter of Mermaid, the configuration of Vega-Lite, the sizes, the line that says it is data,
  and the full path of every example, which is a real file; and every color in the text is a color of the palette. The
  sizes measured when the unit was made were 2,506 characters for `clean`, 2,566 for `slides-dark`, and 2,663 for
  `dema`, with the kit in the output folder of the test (the length depends on the length of that path).
- A kit with the most of everything that the format allows gets a block of at most 4,000 characters that ends with the
  truncation note, starts with the header, has every color, and is the same text when it is written again.
- The cuts are made in the order of the design and only as far as the budget needs: a kit whose notes alone are too long
  keeps its configuration and its examples; a larger kit loses the configuration and then examples.
- A kit with no optional part has no heading and no code block for them, and a theme without variables has only the
  theme.
- A value of a kit cannot end a code block, start a heading, or change the lines of the block.
- Each section is written whole, with no cut; a section that the kit does not have is said so; an unknown section is
  refused with the list of the known ones.
- A block asked for without the example images has every other part of the full block and none of the example images.

#### Test Scenarios

**StyleGuidance_Build_CleanKit_IsTheGoldenText**: The block for the kit `clean`, with its folder written as `{KIT}`, is
exactly the text in `Golden/clean-guidance.txt` of the test project. This scenario is tested by
`StyleGuidance_Build_CleanKit_IsTheGoldenText`.

**StyleGuidance_Build_BuiltInKits_FitTheBudgetAndHaveEveryPart**: For `clean`, `slides-dark`, and `dema` the block is
between 1,500 and 4,000 characters, has no truncation note, names the kit and its place, has a line `- {role} {color}`
for every palette entry, the Mermaid frontmatter, the Vega-Lite configuration, a line for each size, and the line
`- {full path}: {caption}` for each example, where the path is absolute and the file exists, and no color in the text is
outside the palette. This scenario is tested by `StyleGuidance_Build_BuiltInKits_FitTheBudgetAndHaveEveryPart`.

**StyleGuidance_Build_MaximalKit_StaysWithinBudgetAndSaysItWasCut**: A kit with 24 colors, 40 Mermaid variables of 60
characters, a configuration of 8 KiB, 1,500 characters of notes, ten items of 160 characters in each list, 16 sizes, and
24 examples with captions of 160 characters gets a block of at most 4,000 characters that starts with the header,
contains every color, ends with the truncation note, and is the same text the second time. This scenario is tested by
`StyleGuidance_Build_MaximalKit_StaysWithinBudgetAndSaysItWasCut`.

**StyleGuidance_Build_KitOverTheBudget_CutsInTheOrderOfTheDesign**: A kit with no notes fits whole. The same kit with
notes of 1,500 characters has them cut to a few hundred characters with an ellipsis and keeps its configuration and its
ten examples. A kit with more of everything has no configuration (a pointer to `style_get` instead) and at most six
examples, and fits. This scenario is tested by `StyleGuidance_Build_KitOverTheBudget_CutsInTheOrderOfTheDesign`.

**StyleGuidance_Build_KitWithoutOptionalParts_OmitsThoseBlocks**: The smallest kit has the palette and the fonts and
none of the Mermaid, Vega-Lite, list, notes, size, or example headings and no code block; a theme without variables is a
frontmatter with the theme only. This scenario is tested by
`StyleGuidance_Build_KitWithoutOptionalParts_OmitsThoseBlocks`.

**StyleGuidance_Build_HostileText_CannotEndACodeBlockOrStartAHeading**: A kit whose name, description, notes, list
items, Mermaid variable, and configuration contain back ticks, line breaks, and headings gets a block with exactly the
four code fences that the block writes and exactly the headings of the block, with the notes quoted line by line, the
back ticks replaced, the YAML value escaped, and the JSON back tick escaped. This scenario is tested by
`StyleGuidance_Build_HostileText_CannotEndACodeBlockOrStartAHeading`.

**StyleGuidance_BuildSection_EachSection_IsTheWholeSectionOrSaysThereIsNone**: For the largest kit each section is whole
(24 colors, the whole configuration, 40 variables, 24 examples, ten items, 16 sizes, the fonts, with the name in any
case); for the smallest kit each section that it does not have is a sentence that says so; an unknown section is refused
with the list of the names. This scenario is tested by
`StyleGuidance_BuildSection_EachSection_IsTheWholeSectionOrSaysThereIsNone`.

**StyleGuidance_Build_WithoutExamples_LeavesOnlyTheExampleImagesOut**: For the kit `clean`, the block built with
`includeExamples: false` has the palette and every other part, is not cut, is shorter than the full block, and equals
the full block with the part `## Example images` removed. This scenario is tested by
`StyleGuidance_Build_WithoutExamples_LeavesOnlyTheExampleImagesOut`.

**StyleGuidance_InvalidArguments_Throw**: A missing entry or section is refused. This scenario is tested by
`StyleGuidance_InvalidArguments_Throw`.

**BuiltInStyleKits_ReadFolder_EachKit_LoadsWithNoErrorAndNoWarning**: The test of the kits that come with the program
also asserts that the block for each kit has at most 4,000 characters. This scenario is tested by
`BuiltInStyleKits_ReadFolder_EachKit_LoadsWithNoErrorAndNoWarning`.
