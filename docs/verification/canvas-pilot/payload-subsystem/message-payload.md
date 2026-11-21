### MessagePayload

#### Verification Approach

The values of the payload are verified with unit tests in `MessagePayloadTests.cs`. The tests make parts directly with
recognizable lists and byte arrays, change what they passed in, and read the values back. Nothing is substituted,
because the values have no behavior and no I/O.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A payload carries the contract version one and the values it was made with.
- Clearing or changing the lists and the byte arrays that were passed in does not change the payload, at every level:
  payload, image, mark, and its source elements and points.
- Parts that cannot be part of a payload are refused.

#### Test Scenarios

**MessagePayload_New_CarriesContractVersionOne**: An empty payload has version one, its time, and no parts. This
scenario is tested by `MessagePayload_New_CarriesContractVersionOne`.

**MessagePayload_New_CopiesListsAndBytes_SoLaterChangesToTheSourcesChangeNothing**: A payload is made from lists and
arrays of the caller, which then change a byte of the area, a byte of the picture, and clear every list. The payload
still holds the original bytes, points, source elements, parts, and attachment. This scenario is tested by
`MessagePayload_New_CopiesListsAndBytes_SoLaterChangesToTheSourcesChangeNothing`.

**PayloadSegment_Parts_AreValuesThatKeepOriginAndReferences**: Equal parts are equal; text of another origin and a mark
and an attachment of the same number are different. This scenario is tested by
`PayloadSegment_Parts_AreValuesThatKeepOriginAndReferences`.

**MessagePayload_New_InvalidParts_Throw**: A missing text or list, a blank path or fingerprint, a missing format, a size
or number below one, an empty picture or area, and fewer than two points are refused. This scenario is tested by
`MessagePayload_New_InvalidParts_Throw`.
