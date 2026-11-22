## Microsoft.Extensions.AI Verification

This document provides the verification evidence for the Microsoft.Extensions.AI OTS software item. Requirements for
this OTS item are defined in the Microsoft.Extensions.AI OTS Software Requirements document.

### Required Functionality

Microsoft.Extensions.AI binds the named arguments of a function from JSON values and describes its parameters in a
schema, runs a function that is a class of its own in its invocation loop and gives the result to the model, keeps the
bytes and media type of picture content, and joins streamed updates into one response. The application's tools,
messages, and pictures rely on each of these behaviors, so each has a requirement and a test.

### Verification Approach

The library has no self-validation mode. It is verified by tests in `test/OtsSoftwareTests/ExtensionsAiTests.cs` that
use its types the way the Agent subsystem does, with a scripted chat client and no language model. The tests are run
again before a version upgrade is accepted.

### Test Scenarios

#### ExtensionsAi_AIFunction_Invoke_BindsNamedArgumentsFromJsonValues

**Scenario**: A function with a text and a count is made from a delegate and invoked with JSON elements as arguments;
its schema is read.

**Expected**: The result shows that both arguments were bound by name, and the schema names both parameters as
properties and as required.

**Requirement coverage**: `CanvasPilot-OTS-ExtensionsAi-BindFunctionArguments`.

#### ExtensionsAi_FunctionInvokingChatClient_RunsSubclassedFunctionAndReturnsItsResult

**Scenario**: A function that is a class of its own is offered to the invocation client over a scripted client that
calls it once and then answers.

**Expected**: The function ran once with the argument of the call, its result is in the second request to the model, and
the answer is the last text.

**Requirement coverage**: `CanvasPilot-OTS-ExtensionsAi-RunFunctionsInInvocationLoop`.

#### ExtensionsAi_DataContent_KeepsBytesAndMediaType

**Scenario**: Picture content is made from bytes and from a data address.

**Expected**: Both hold the same bytes and media type, the content is an image and not audio, and the address of the
content from bytes is a data address of the media type.

**Requirement coverage**: `CanvasPilot-OTS-ExtensionsAi-CarryPictureContent`.

#### ExtensionsAi_ChatResponseUpdates_ToChatResponse_JoinsTextAndKeepsFunctionCalls

**Scenario**: Updates that hold a piece of text, a function call, and a second piece of text are joined.

**Expected**: The text of the response is the two pieces in order, and the call is kept in its messages.

**Requirement coverage**: `CanvasPilot-OTS-ExtensionsAi-JoinStreamedUpdates`.
