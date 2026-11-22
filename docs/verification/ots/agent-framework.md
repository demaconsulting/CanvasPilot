## Microsoft Agent Framework Verification

This document provides the verification evidence for the Microsoft Agent Framework OTS software item. Requirements for
this OTS item are defined in the Microsoft Agent Framework OTS Software Requirements document.

### Required Functionality

The Agent Framework agent streams the pieces of an answer in order, keeps the conversation of a session and starts a new
session without it, and stops a streamed turn with an operation-cancelled exception when its token is cancelled. The
chat of the application relies on each of these behaviors, so each has a requirement and a test.

### Verification Approach

The framework has no self-validation mode. It is verified by tests in `test/OtsSoftwareTests/AgentFrameworkTests.cs`
that run an agent, built the way the Agent subsystem builds it, over a scripted chat client that plays the model. No
language model is used. The tests are run again before a version upgrade is accepted.

### Test Scenarios

#### AgentFramework_ChatClientAgent_RunStreaming_YieldsTextUpdatesInOrder

**Scenario**: An agent runs one streamed turn over a client that answers in three pieces.

**Expected**: The text of the updates is the three pieces, one by one, in the order they were sent.

**Requirement coverage**: `CanvasPilot-OTS-AgentFramework-StreamAnswerPieces`.

#### AgentFramework_AgentSession_SecondTurn_SendsEarlierConversation

**Scenario**: Two turns run on one session and a third on a new session; the requests that the model received are
compared.

**Expected**: The second request holds the first question, the first answer, and the second question; the third request
holds only its own question.

**Requirement coverage**: `CanvasPilot-OTS-AgentFramework-KeepConversationOnSession`.

#### AgentFramework_ChatClientAgent_RunStreaming_CancelledMidStream_ThrowsOperationCanceled

**Scenario**: A turn with a long answer is read, and its token is cancelled after the first update.

**Expected**: The stream ends with an operation-cancelled exception before all of the pieces were delivered.

**Requirement coverage**: `CanvasPilot-OTS-AgentFramework-StopOnCancellation`.
