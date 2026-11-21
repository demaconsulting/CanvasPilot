### ComposerDocument

#### Verification Approach

`ComposerDocument` and the segment and snapshot records are verified with unit tests in `ComposerDocumentTests.cs`. The
unit has no collaborators and performs no I/O, so nothing is substituted. The tests call the operations with controlled
content and assert on the segments, the flat text, the length, and the exceptions thrown. Chip tests use paths of the
temporary folder and never touch the files.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Text and chips are inserted at the offset given, joined or kept apart by origin, and counted as one position per
  character and per chip.
- Deleting a range returns what it removed, across text and chips, and joins the text around it.
- No text that entered the document holds the chip placeholder or a control character other than tab and line feed, and
  every kind of line break is one line feed.
- A snapshot does not change when the document changes afterwards.
- Offsets and ranges outside the document are refused without a change.

#### Test Scenarios

**ComposerDocument_InsertText_IntoEmptyDocument_HoldsOneTypedSegment**: Text is inserted into an empty document; one
typed segment holds it and the length and flat text agree. This scenario is tested by
`ComposerDocument_InsertText_IntoEmptyDocument_HoldsOneTypedSegment`.

**ComposerDocument_InsertText_InTheMiddle_InsertsAtOffset**: One character is inserted between two; the text reads in
the right order and stays one segment. This scenario is tested by
`ComposerDocument_InsertText_InTheMiddle_InsertsAtOffset`.

**ComposerDocument_InsertText_NextToTextOfSameOrigin_JoinsIntoOneSegment**: Typed text is added after typed text; both
are one segment. This scenario is tested by `ComposerDocument_InsertText_NextToTextOfSameOrigin_JoinsIntoOneSegment`.

**ComposerDocument_InsertText_DictatedInsideTypedText_KeepsOriginsSeparate**: Dictated text is inserted inside typed
text; typed, dictated, and typed segments result. This scenario is tested by
`ComposerDocument_InsertText_DictatedInsideTypedText_KeepsOriginsSeparate`.

**ComposerDocument_InsertChip_BetweenText_SplitsTextAndCountsOnePosition**: A chip is inserted between two characters;
the text is split around it, the length counts it once, and the flat text has one placeholder. This scenario is tested
by `ComposerDocument_InsertChip_BetweenText_SplitsTextAndCountsOnePosition`.

**ComposerDocument_InsertChip_AtStartAndEnd_PlacesChipsAtTheEnds**: Chips are inserted at the start and at the end. This
scenario is tested by `ComposerDocument_InsertChip_AtStartAndEnd_PlacesChipsAtTheEnds`.

**ComposerDocument_Chips_MixedContent_AreEnumeratedInOrder**: Three chips of three kinds in text are listed in message
order. This scenario is tested by `ComposerDocument_Chips_MixedContent_AreEnumeratedInOrder`.

**ComposerDocument_FlatText_TextAndChips_LengthIsCharactersPlusChips**: Three characters and two chips give five
positions and two placeholders in the flat text. This scenario is tested by
`ComposerDocument_FlatText_TextAndChips_LengthIsCharactersPlusChips`.

**ComposerDocument_Delete_RangeAcrossTextAndChips_RemovesAndReturnsSegments**: A range that starts inside text, covers a
chip, and ends inside text is deleted; the rest is joined and the removed segments are returned in order. This scenario
is tested by `ComposerDocument_Delete_RangeAcrossTextAndChips_RemovesAndReturnsSegments`.

**ComposerDocument_Delete_SingleChip_JoinsNeighboringText**: A chip between two texts of one origin is deleted; the text
is one segment again. This scenario is tested by `ComposerDocument_Delete_SingleChip_JoinsNeighboringText`.

**ComposerDocument_Delete_ZeroLength_ChangesNothing**: Deleting zero positions removes nothing. This scenario is tested
by `ComposerDocument_Delete_ZeroLength_ChangesNothing`.

**ComposerDocument_GetSlice_InsideText_CutsTextWithoutChangingDocument**: A slice from inside dictated text is cut with
its origin and the document is unchanged. This scenario is tested by
`ComposerDocument_GetSlice_InsideText_CutsTextWithoutChangingDocument`.

**ComposerDocument_SanitizeText_UnwantedCharacters_AreRemovedOrNormalized**: Text with the placeholder, control
characters, and every kind of line break is cleaned as described; tab, line feed, and plain text stay. This scenario is
tested by `ComposerDocument_SanitizeText_UnwantedCharacters_AreRemovedOrNormalized`.

**ComposerDocument_InsertText_OnlyChipPlaceholders_InsertsNothing**: Text of only placeholders adds nothing, so it
cannot shift chips. This scenario is tested by `ComposerDocument_InsertText_OnlyChipPlaceholders_InsertsNothing`.

**TextSegment_Constructor_TextWithPlaceholder_HoldsCleanedText**: A text segment made from text with a placeholder and a
control character holds clean text. This scenario is tested by
`TextSegment_Constructor_TextWithPlaceholder_HoldsCleanedText`.

**ComposerDocument_Snapshot_AfterLaterEdits_StaysUnchanged**: A snapshot is taken, the document is then changed in every
way, and the snapshot still holds the first content. This scenario is tested by
`ComposerDocument_Snapshot_AfterLaterEdits_StaysUnchanged`.

**ComposerSnapshot_HasSameContentAs_EqualAndDifferentContent_ComparesSegments**: Snapshots of equal content built in
different ways compare equal, and equal text of another origin does not. This scenario is tested by
`ComposerSnapshot_HasSameContentAs_EqualAndDifferentContent_ComparesSegments`.

**ComposerDocument_InvalidOffsetOrRange_ThrowsArgumentOutOfRangeException** and
**ComposerDocument_InsertWithInvalidOffset_ThrowsArgumentOutOfRangeException**: Negative offsets, offsets past the end,
and ranges that leave the document are refused without a change. These scenarios are tested by the tests of the same
names.

**ComposerDocument_NullArguments_Throw**: Null text, chip, segment list, and segment are refused. This scenario is
tested by `ComposerDocument_NullArguments_Throw`.

**ComposerDocument_Clear_DocumentWithContent_EmptiesDocumentAndLiveSegments**: Clearing empties the document, also in
the live list of segments that was read before. This scenario is tested by
`ComposerDocument_Clear_DocumentWithContent_EmptiesDocumentAndLiveSegments`.

**ComposerSegment_Chips_ReportNameAndLabel**: The names of chips for a file, a folder with a trailing separator, and a
drive root, the label of a mark, and the refusal of a blank path and of a mark number below one. This scenario is tested
by `ComposerSegment_Chips_ReportNameAndLabel`.
