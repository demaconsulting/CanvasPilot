### StyleKitDocument

#### Verification Approach

`StyleKitDocument` is verified with unit tests in `StyleKitDocumentTests.cs`. The tests build kit files as text, change
one value at a time with a small JSON editor in the test, and compare the issues with the exact path and message that
the design promises. The folder reader is tested on temporary folders, including a sparse file that is one byte over the
limit of an example. The CanvasNet font classification that the unit relies on is verified separately, as OTS evidence,
by the test `CanvasNet_GenericFontFamilies_ClassifiesBundledAliasesAndUnknownFamilies`.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A kit that uses every property is read into every field and written back as the same text, byte for byte, on a second
  round, with line feeds only, colors in upper case, and the properties in the documented order.
- A kit with only the required properties is read with the plain defaults and is written without the optional parts.
- Every wrong value in the list of the tests is an error that names the property in the words of the file, says what is
  allowed, and gives no kit; several mistakes are all reported.
- A format version that is missing, not a number, below 1, or above 1 is the only error of the file, and a version above
  1 says the kit was made by a newer version.
- Text that is not an object, is not JSON, is nested deeper than 16 levels, or is larger than 256 KiB is one error of
  the whole file.
- An example path that is a parent folder, rooted, with a drive, with a backslash, in a sub-folder, or with a character
  that a file name cannot have is an error.
- A property that the format does not know is a warning, and the next write drops it.
- A font list whose first family is a generic keyword, a common alias, or a bundled Liberation family has no message,
  and any other first family is a warning that names the family and says which formats use the installed font.
- Every limit is accepted at its value and refused one above it, and the files of the examples folder beyond 24 or over
  16 MiB are ignored with a warning.
- A kit that is built in memory is given the same messages as a file.

#### Test Scenarios

**StyleKitDocument_Write_FullKit_RoundTripsEveryFieldAndIsByteStable**: A kit file that uses every property, written
with untidy white space and lower-case colors, in a folder with two example files, is read, written, read again, and
written again. There is no issue, every field of the second kit equals what the file said, and the two written texts are
equal. This scenario is tested by `StyleKitDocument_Write_FullKit_RoundTripsEveryFieldAndIsByteStable`.

**StyleKitDocument_Parse_MinimalKit_UsesDefaults**: A file with only the version, the id, the name, and three palette
colors is read with the default fonts, numbers, and no optional part, and the written text has no Mermaid or Vega-Lite
part. This scenario is tested by `StyleKitDocument_Parse_MinimalKit_UsesDefaults`.

**StyleKitDocument_Parse_LowerCaseColors_AreNormalizedToUpperCase**: A color written in lower case is held and written
in upper case. This scenario is tested by `StyleKitDocument_Parse_LowerCaseColors_AreNormalizedToUpperCase`.

**StyleKitDocument_ReadFolder_ExamplesFolder_ListsTopLevelFilesSortedWithCaptions**: Files created in an unsorted order,
a hidden file, and a file in a sub-folder of the examples folder give the examples in name order without the hidden file
and the sub-folder, with the caption only on the file that has one. This scenario is tested by
`StyleKitDocument_ReadFolder_ExamplesFolder_ListsTopLevelFilesSortedWithCaptions`.

**StyleKitDocument_ReadFolder_MissingFolderMissingFileOrOversizeFile_ReportsErrors**: A folder that does not exist, a
folder with no style file, and a style file of one byte over the limit are each one error of the file and give no kit.
This scenario is tested by `StyleKitDocument_ReadFolder_MissingFolderMissingFileOrOversizeFile_ReportsErrors`.

**StyleKitDocument_Parse_InvalidValue_ReportsErrorWithPathAndText**: The full kit file with one value changed at a time:
a bad id, a missing id, an id that is not text, an empty and a too long name, each wrong form of a color (`#12`, a color
name, a color with an alpha channel, and a number), a missing required role, a bad name of a color, a missing and a
mistyped palette, a bad Mermaid theme, a missing theme, a bad variable name, a Vega-Lite configuration that is a list
and one that is missing, stroke, radius, and spacing out of range and of the wrong type, a font list that does not
parse, advice that is empty or not text, a preset width and height out of range or not whole, a resolution out of range,
an empty preset name, a transparency that is not a boolean, a preset that is not an object, and a caption that is not
text. Each gives an error that starts with the path and message of the test and gives no kit. This scenario is tested by
`StyleKitDocument_Parse_InvalidValue_ReportsErrorWithPathAndText`.

**StyleKitDocument_Parse_PresetSizesAndNames_AreChecked**: A preset with neither a width nor a height is an error of the
preset, a preset with only a height is accepted, and a preset name that repeats another in a different case is an error
of the second. This scenario is tested by `StyleKitDocument_Parse_PresetSizesAndNames_AreChecked`.

**StyleKitDocument_Parse_SeveralErrors_AreAllReported**: Three unrelated mistakes are three errors, in the order of the
file. This scenario is tested by `StyleKitDocument_Parse_SeveralErrors_AreAllReported`.

**StyleKitDocument_Parse_FormatVersion_IsCheckedFirst**: A format version that is missing, `0`, `2`, the text `"1"`, and
`1.5` each give one error and no other, although the file has another mistake. This scenario is tested by
`StyleKitDocument_Parse_FormatVersion_IsCheckedFirst`.

**StyleKitDocument_Parse_NotAnObjectNotJsonTooDeepOrTooLarge_IsOneErrorOfTheFile**: A list, text that is not JSON, an
empty text, 40 nested lists, and a text over the limit are each one error of the whole file with an empty path. This
scenario is tested by `StyleKitDocument_Parse_NotAnObjectNotJsonTooDeepOrTooLarge_IsOneErrorOfTheFile`.

**StyleKitDocument_Parse_ExamplePathOutsideTheExamplesFolder_IsAnError**: Captions whose path climbs with `..`, is
rooted by a slash or a drive, uses a backslash, goes into a sub-folder, is not in the examples folder, is the folder
alone, or has a character that a file name cannot have on any platform (`|` is one, though Linux and macOS accept it)
each give one error that names the path. This scenario is tested by
`StyleKitDocument_Parse_ExamplePathOutsideTheExamplesFolder_IsAnError`.

**StyleKitDocument_Parse_UnknownProperty_IsAWarningAndIsDroppedOnWrite**: A property that the format does not know is
one warning, the kit is read, and the written text does not have it. This scenario is tested by
`StyleKitDocument_Parse_UnknownProperty_IsAWarningAndIsDroppedOnWrite`.

**StyleKitDocument_Parse_BundledOrCommonFonts_HaveNoMessage**: Font lists whose first family is `sans-serif`,
`monospace`, `Segoe UI`, `Consolas`, `Arial`, `Liberation Mono`, or empty give no issue. This scenario is tested by
`StyleKitDocument_Parse_BundledOrCommonFonts_HaveNoMessage`.

**StyleKitDocument_Parse_FontThatIsNotBundled_IsAWarning**: A list that starts with `Inter` and one that is
`Comic Sans MS` each give one warning on the heading font that names the family and says which formats use the installed
font, and the kit is read. This scenario is tested by `StyleKitDocument_Parse_FontThatIsNotBundled_IsAWarning`.

**StyleKitDocument_ReadFolder_CaptionWithoutFileAndFolderNameDifferentFromId_AreWarnings**: A kit in a folder with
another name than its id and with a caption but no file is read with a warning for each, and the caption is not written
again. This scenario is tested by
`StyleKitDocument_ReadFolder_CaptionWithoutFileAndFolderNameDifferentFromId_AreWarnings`.

**StyleKitDocument_Parse_Limits_AreErrorsWhenExceededAndAcceptedAtTheLimit**: Files with 24 palette colors, 40
variables, 16 presets, 10 entries of each list of advice, and 24 captions are accepted; one more of each is an error
that gives the number and the limit. This scenario is tested by
`StyleKitDocument_Parse_Limits_AreErrorsWhenExceededAndAcceptedAtTheLimit`.

**StyleKitDocument_Parse_TextLengths_AreErrorsOneCharacterOverTheLimit**: The description, notes, an entry of advice, a
preset name, a caption, a variable, and a font list are accepted at their limits and are an error one character over.
This scenario is tested by `StyleKitDocument_Parse_TextLengths_AreErrorsOneCharacterOverTheLimit`.

**StyleKitDocument_Parse_VegaLiteConfigSize_IsMeasuredOnCompactJson**: A configuration of exactly 8 KiB as compact JSON
is accepted, one byte more is an error, and a configuration with white space is read as its compact text. This scenario
is tested by `StyleKitDocument_Parse_VegaLiteConfigSize_IsMeasuredOnCompactJson`.

**StyleKitDocument_ReadFolder_TooManyOrTooLargeExampleFiles_AreIgnoredWithWarnings**: A folder with 26 small files and
one file that is a byte over 16 MiB gives the first 24 small files by name as examples, a warning for the large file,
and a warning for each of the two files beyond the limit. This scenario is tested by
`StyleKitDocument_ReadFolder_TooManyOrTooLargeExampleFiles_AreIgnoredWithWarnings`.

**StyleKitDocument_Validate_KitInMemory_GivesTheSameMessagesAsTheReader**: A kit read from a good file has no issue, and
a kit built in memory with a bad id, a bad and a missing color, a radius out of range, a Vega-Lite configuration that is
a list, and a preset with no size gives the same messages as the reader. This scenario is tested by
`StyleKitDocument_Validate_KitInMemory_GivesTheSameMessagesAsTheReader`.

**StyleKitDocument_IsValidId_ChecksThePattern**: Lower-case kebab ids are accepted; a leading dash, upper case, an
underscore, a space, a trailing line feed, empty text, and no text are refused. This scenario is tested by
`StyleKitDocument_IsValidId_ChecksThePattern`.

**StyleKitDocument_IsValidId_LengthLimit_Is48Characters**: An id of 48 characters is accepted and one of 49 is refused.
This scenario is tested by `StyleKitDocument_IsValidId_LengthLimit_Is48Characters`.

**StyleKitDocument_PublicMembers_NullArguments_ThrowArgumentNullException**: Each reader, the checker, the writer, and
the color normalizer refuse a missing argument. This scenario is tested by
`StyleKitDocument_PublicMembers_NullArguments_ThrowArgumentNullException`.

**StyleKitDocument_StyleIssue_ToString_WritesPathAndMessage**: An issue is written as the path, a colon, and the
message, and as the message alone when the path is empty. This scenario is tested by
`StyleKitDocument_StyleIssue_ToString_WritesPathAndMessage`.

**StyleKitDocument_StyleColors_ParseNormalizeAndContrast_FollowTheDefinitions**: A color is read in any case, ten forms
that are not a color are refused, black on white has the contrast 21, the same color has the contrast 1, the contrast
does not depend on the order, and a mid grey on white has the known value. This scenario is tested by
`StyleKitDocument_StyleColors_ParseNormalizeAndContrast_FollowTheDefinitions`.
