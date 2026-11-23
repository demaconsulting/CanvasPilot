### AgentContract

#### Verification Approach

`AgentContract` is verified with unit tests defined in `AgentContractTests.cs`. The unit
is plain records and one interface, so no collaborator is replaced: the tests build the records and read what they hold,
check the rules that the records enforce themselves (the cutting of summaries to the limit, the refusal of an empty call
id, tool name, or failure message, the equality of events by value, the default empty history, which readiness statuses
allow a turn, and, for `ToolProgress`, the refusal of an empty call id or name, a negative byte count made zero, and a
blank path made `null`). A payload is built from text only, with the public constructor of `MessagePayload`. The rules
of the pipeline that depend on the kind of an event (what may be merged and what may not) are verified in the unit
verification of `AgentService`, and the mapping of the usage record in that of `AgentEventMapper`.

#### Test Environment

N/A - standard test environment. The tests use no files, network, display, or external services.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Tool summaries never exceed 200 characters; a missing summary is empty; an empty or missing call id, tool name, or
  failure message is refused.
- Events compare by value.
- A progress event refuses an empty call id and an empty tool name, counts a negative number of bytes as zero, and has
  no path for a path of white space.
- A request keeps its payload, has an empty history unless one is given, and refuses a `null` payload.
- Only the ready status and the Ollama context-too-small status are usable.

#### Test Scenarios

**AgentEvent_ToolEvents_LongSummaries_AreCutToTheLimit**: A tool-started event with an argument summary longer than the
limit, and tool-finished events with a summary of exactly the limit and one padded with white space, are built. The long
one is exactly 200 characters and ends with `...`, the one at the limit is kept, the padded one is trimmed, and the
success and picture flags are kept. This scenario is tested by `AgentEvent_ToolEvents_LongSummaries_AreCutToTheLimit`.

**AgentEvent_ToolEvents_NullSummaryEmptyIdOrName_AreHandled**: A missing summary becomes an empty one; an empty call id
or tool name raises an argument exception and a missing one raises an argument null exception. This scenario is tested
by `AgentEvent_ToolEvents_NullSummaryEmptyIdOrName_AreHandled`.

**AgentEvent_TextDeltaAndFailed_InvalidArguments_AreRefused**: A text piece of `null` is refused, a single space is
accepted, and a failed event with a missing, empty, or white-space message is refused. This scenario is tested by
`AgentEvent_TextDeltaAndFailed_InvalidArguments_AreRefused`.

**AgentEvent_Records_WithSameValues_AreEqual**: Events built from the same values are equal, and a written-file event
that differs in the replaced flag is not. This scenario is tested by `AgentEvent_Records_WithSameValues_AreEqual`.

**AgentEvent_ProgressEvents_ValidateTheirArguments**: A tool-progress event is built with an empty call id and with an
empty tool name, with a negative byte count, and with a path of white space. The first two raise an argument exception,
the negative count is zero, and the blank path is `null`. This scenario is tested by
`AgentEvent_ProgressEvents_ValidateTheirArguments`.

**AgentTurnRequest_Constructor_PayloadAndHistory_AreKept**: A request keeps its payload, has no restored history unless
it is given, keeps the given history in order, and refuses a `null` payload. This scenario is tested by
`AgentTurnRequest_Constructor_PayloadAndHistory_AreKept`.

**AgentReadinessReport_IsUsable_ByStatus_IsTrueOnlyForReadyAndWarning**: A report is built for each of the nine
statuses; it is usable for ready and for Ollama context too small, and for no other status, and it keeps its status and
message. This scenario is tested by `AgentReadinessReport_IsUsable_ByStatus_IsTrueOnlyForReadyAndWarning`.

**AgentReadinessReport_Constructor_EmptyMessage_IsRefused**: A report with a missing, empty, or white-space message is
refused. This scenario is tested by `AgentReadinessReport_Constructor_EmptyMessage_IsRefused`.
