### PayloadDescriber

#### Verification Approach

`PayloadDescriber` is verified with unit tests in `PayloadDescriberTests.cs` on payloads that are made directly from
values with recognizable bytes and a fixed time. One test compares the whole description with the exact expected text;
the others read the description back as JSON, which also proves that the text is valid JSON. Nothing is substituted.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The description of a payload with everything in it is exactly the expected text, with line feeds only.
- The same payload and an equal payload give the same text.
- Images appear only as a size in bytes and a SHA-256 fingerprint.
- Special characters are escaped as JSON requires and read back exactly; missing values are `null`; a picture that
  cannot say what is under a mark gives `available` false with the reason.

#### Test Scenarios

**PayloadDescriber_Describe_Payload_GivesExactlyThisText**: The description of a payload with text, four parts, an
attachment, and a picture with a mark that has a note, an area, a node, and an anchor is the expected text, with the
fingerprints computed from the bytes. This scenario is tested by
`PayloadDescriber_Describe_Payload_GivesExactlyThisText`.

**PayloadDescriber_Describe_EqualPayloads_GiveTheSameText**: Describing one payload twice, and an equal payload, gives
three equal texts. This scenario is tested by `PayloadDescriber_Describe_EqualPayloads_GiveTheSameText`.

**PayloadDescriber_Describe_Images_AreShownAsByteCountAndFingerprint**: The annotated picture and the area of the mark
have exactly a byte count and a fingerprint. This scenario is tested by
`PayloadDescriber_Describe_Images_AreShownAsByteCountAndFingerprint`.

**PayloadDescriber_Describe_SpecialCharacters_AreEscapedAsJsonRequiresAndReadBackExactly**: Quotation marks,
backslashes, a line break, a tab, accented letters, a check mark, and markup characters read back as they were, and the
letters are written as letters. This scenario is tested by
`PayloadDescriber_Describe_SpecialCharacters_AreEscapedAsJsonRequiresAndReadBackExactly`.

**PayloadDescriber_Describe_MissingValues_AreWrittenAsNullAndReason**: A picture without a file fingerprint and a mark
without a note are `null`, and the mark that cannot say what is under it has `available` false, the reason, and no
elements. This scenario is tested by `PayloadDescriber_Describe_MissingValues_AreWrittenAsNullAndReason`.

**PayloadDescriber_Describe_NullPayload_ThrowsArgumentNullException**: A missing payload is refused. This scenario is
tested by `PayloadDescriber_Describe_NullPayload_ThrowsArgumentNullException`.
