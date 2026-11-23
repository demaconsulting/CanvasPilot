## AgentKit Verification

This document provides the verification evidence for the AgentKit OTS software item. Requirements for this OTS item are
defined in the AgentKit OTS Software Requirements document.

### Required Functionality

AgentKit confines the copilot's tools to the project with its path policy, composes tool packs with unique family
prefixes and valid names, bounds tool results and shapes image results, runs a tool-calling turn on a chat client with
picture promotion, adds the context length to Ollama requests, validates model settings against what a model offers and
applies the selected model and reasoning effort to requests, describes the models of an Ollama server, and runs the
Copilot CLI that the user installed at an explicit path (signing in the way that the user is signed in, without a token
or a host of the application) without ever bundling or downloading a Copilot runtime. The application relies on each of
these behaviors, so each has a requirement and a test.

### Verification Approach

AgentKit has no self-validation mode. It is verified by tests in `test/OtsSoftwareTests/AgentKitTests.cs` that call it
the way the Agent subsystem does: a path policy over a temporary project folder, tool packs and guarded tools, and an
agent over a scripted chat client that plays the model. No language model and no Copilot CLI are used; the Copilot
runtime is started only with an explicit CLI path that does not exist, so that no Copilot process runs. The tests are
run again before a version upgrade is accepted. That the runtime starts on a real installed CLI, signed in as the
library requires, is shown by the live tests that are recorded in the manual verification of the system, and the starter
of CanvasPilot is verified in the unit verification of `CopilotRuntimeStarter`.

Two checks complement these tests. The FileAssert test `Copilot_RuntimeNotPublished` asserts, on the published win-x64
output that the installer harvests whole, that no file whose name starts with `copilot` or `libcopilot`, no
`runtime.node`, no `.copilot-explicit-cli`, and nothing under `runtimes\*\native\copilot*` exists, and that the
executable stays below 160 MB; and the Windows build reads the file table of the MSI and fails when it lists such a
file. Both run in CI only on Windows and are described in the verification of the system.

### Test Scenarios

#### AgentKit_PathPolicy_ResolveWrite_RefusesEscapesAndProtectedFolders

**Scenario**: A policy grants a project folder for reading and writing, except for a folder named `.canvaspilot`. A
write to a file inside, a write that climbs out with "..", a write to an absolute path beside the project, and a write
into the protected folder are resolved.

**Expected**: Only the write inside is permitted, and it resolves to the full path inside the project. The other three
are refused, and the refusal is a message and not an exception.

**Requirement coverage**: `CanvasPilot-OTS-AgentKit-ConfineToolPaths`.

#### AgentKit_PathPolicy_ResolveRead_LinkInsideTheGrant_IsJudgedBySpellingAndNotFollowed

**Scenario**: A folder link (a junction on Windows, a symbolic link elsewhere) inside the granted project leads to a
folder outside it. A read and a write through the link are resolved.

**Expected**: Both are permitted and resolve to paths spelled inside the project, so the library does not follow links.
This is the documented behavior of the library and the reason that the application's own file tools check for links;
the test fails when a library change makes the policy follow links, so that the tools can be reviewed.

**Requirement coverage**: `CanvasPilot-OTS-AgentKit-ConfineToolPaths`.

#### AgentKit_ToolPackBuilder_Add_RefusesDuplicatePrefixAndBuildsInOrder

**Scenario**: Two packs with different prefixes are added to a builder, a third with a prefix that is taken is added,
and the builder is built.

**Expected**: The third is refused with an argument exception that talks about the prefix, and the tools come in the
order of the packs with their prefixes.

**Requirement coverage**: `CanvasPilot-OTS-AgentKit-ComposeToolPacks`.

#### AgentKit_GuardedToolFactory_Create_RefusesInvalidToolNames

**Scenario**: Tools are created with the reserved name `read`, a name with capital letters, a name with a space, and a
name longer than the maximum, and then with a good name.

**Expected**: The four bad names are refused with an argument exception, and the good name is accepted and kept.

**Requirement coverage**: `CanvasPilot-OTS-AgentKit-ComposeToolPacks`.

#### AgentKit_GuardedToolFactory_AsGuarded_BinaryOverCeiling_IsRefusedAndWithinCeilingPasses

**Scenario**: A tool returns a binary result of a size that its caller chooses, bounded to 1 KiB. It is asked for 100
bytes and for 5000 bytes.

**Expected**: The small result is delivered as data content, and the large one is a refusal that names the reason
`ResourceTooLarge`.

**Requirement coverage**: `CanvasPilot-OTS-AgentKit-BoundToolResults`.

#### AgentKit_ToolResult_Image_ReturnsCaptionThenPictureAndRefusesOtherMediaTypes

**Scenario**: An image result is built with a caption, without one, and with a media type that is not an image.

**Expected**: With a caption the result holds the caption text first and the picture second with the media type and
bytes given; without one it is the picture alone; the non-image media type is refused with an argument exception.

**Requirement coverage**: `CanvasPilot-OTS-AgentKit-BoundToolResults`.

#### AgentKit_ChatClientAgentFactory_ScriptedTurn_StreamsCallResultAndText

**Scenario**: An agent with a tool that echoes its argument runs over a client that first calls the tool and then
answers in two pieces.

**Expected**: The stream holds the call, then the result, then the two text pieces, which join to the answer; the tool
received the argument of the call; and the model was asked twice.

**Requirement coverage**: `CanvasPilot-OTS-AgentKit-RunToolTurnOnChatClient`.

#### AgentKit_ChatClientAgentFactory_ToolImageResult_IsPromotedToAUserMessage

**Scenario**: A tool returns a picture with a caption, and the model calls it and then answers.

**Expected**: The second request to the model holds the picture, with its media type and bytes, as content of a user
message.

**Requirement coverage**: `CanvasPilot-OTS-AgentKit-RunToolTurnOnChatClient`.

#### AgentKit_OllamaContextSizingChatClient_GetResponse_AddsContextLengthWithoutChangingCallersOptions

**Scenario**: Requests are sent through a sizing client for 32768 tokens without options, with options of the caller,
and with options that already name `num_ctx`; a sizing client for zero is created.

**Expected**: The first two requests carry `num_ctx` of 32768 and the caller's other options, the caller's own options
object is not changed, the named length is left alone, and the length of zero is refused.

**Requirement coverage**: `CanvasPilot-OTS-AgentKit-SizeOllamaContext`.

#### AgentKit_ModelSettings_Validate_RefusesWhatTheModelCannotDoAndBindsWhatFits

**Scenario**: Model settings are validated against a model that calls tools and offers the efforts `low` and `high` with
a window of 8,192 tokens, a model that cannot call tools, and a model that cannot be used. The settings that fit, an
effort `xhigh`, a window of 16,384, tool calling for the model without it, and the disabled model are each checked; a
catalog then selects the settings for `gemma3` with the effort `low`, and a model that it does not have is asked for.

**Expected**: The settings that fit have no problem; each of the others has exactly one problem that names the model and
the cause (the efforts that the model offers, the largest window, tool calling, and the reason that the model is
disabled); the selection has the catalog's id `gemma3:latest`, the effort `low`, and the standard effort low; the
missing model is a refusal with a problem that names the provider; binding the bad effort is refused, and a blank model
id is refused.

**Requirement coverage**: `CanvasPilot-OTS-AgentKit-ValidateModelSettings`.

#### AgentKit_ModelSettingsChatClient_GetResponse_AddsTheModelAndEffortWithoutChangingCallersOptions

**Scenario**: A selection with the effort `high` and a window of 4,096 is put over a client that sizes the window to
4,096, and two requests are sent, one without options and one with options of the caller. A selection with a window is
then put over a client that applies no window.

**Expected**: Both requests carry the model id and the high reasoning effort; the caller's temperature is kept and the
caller's options object is unchanged; the selection over the client that applies no window is refused with an argument
exception.

**Requirement coverage**: `CanvasPilot-OTS-AgentKit-ValidateModelSettings`.

#### AgentKit_OllamaModelCatalog_ListAsync_DescribesCapabilitiesWindowAndEfforts

**Scenario**: The Ollama catalog reads a stand-in server whose tags list a model that completes, calls tools, sees
pictures, and thinks (trained for 262,144 tokens) and an embedding model. All models are listed, then the models that
can be used, and a model is found by its id and by a name without a tag in other letter case.

**Expected**: The first model has the three capabilities, the window, and the efforts `none`, `low`, `medium`, and
`high`; the embedding model is not usable; the plain list holds only the first model; both finds succeed and give the
catalog's ids.

**Requirement coverage**: `CanvasPilot-OTS-AgentKit-DescribeOllamaModels`.

#### AgentKit_BuildOutput_HasNoBundledCopilotRuntime

**Scenario**: With the download of the SDK switched off, the build output folder of the test project, which references
the same packages as the application, is searched for the Copilot wrapper at
`runtimes/win-x64/native/copilot-runtime.exe` and for every file that a Copilot runtime consists of anywhere in the
output.

**Expected**: The wrapper does not exist and no such file is found, so no Copilot runtime file is added to the output.

**Requirement coverage**: `CanvasPilot-OTS-AgentKit-NeverBundleCopilotRuntime`.

#### AgentKit_CopilotRuntime_StartAsync_MissingExplicitCliPath_FailsNamingThePathAndStartsNothing

**Scenario**: The Copilot runtime of AgentKit is started with a data directory that does not exist yet and an explicit
CLI path that names a file that does not exist.

**Expected**: The start fails with an invalid-operation exception that names the missing path, no new Copilot runtime
process exists afterward, and the data directory is absent or holds nothing but the ownership marker.

**Requirement coverage**: `CanvasPilot-OTS-AgentKit-NeverBundleCopilotRuntime`.

#### AgentKit_CopilotRuntime_StartAsync_CliPathVariable_IsIgnored

**Scenario**: `COPILOT_CLI_PATH` of the process is set to a script that would leave a marker file if it were run, and
the runtime is started with an explicit CLI path that does not exist.

**Expected**: The start still fails for the explicit missing path, the marker file does not exist, so the variable is
never run, and the variable is restored afterward.

**Requirement coverage**: `CanvasPilot-OTS-AgentKit-NeverBundleCopilotRuntime`.

#### AgentKit_CopilotRuntimeOptions_CanvasPilotArguments_AllowTheCopilotCliSignInAndLeaveTokenAndHostUnset

**Scenario**: Copilot runtime options are built the way that CanvasPilot builds them: a data directory that does not
exist, no token, the installed CLI as the source, and the full path of a CLI that is not run.

**Expected**: The sign-in of the Copilot CLI is allowed, the token, the token provider, and the host are not set, and
the source is the installed CLI. That the runtime reports its sign-in source, login, and host is shown by the live
tests of the system verification, because it needs a real runtime.

**Requirement coverage**: `CanvasPilot-OTS-AgentKit-SignInWithCopilotCli`.

#### AgentKit_CopilotRuntimeOptions_HostAndTokenArguments_AreNormalizedAndCombinationsRefused

**Scenario**: Options are built with the host `example.ghe.com`, with a blank host, and with a token together with a
token provider.

**Expected**: The host without a scheme is kept as `https://example.ghe.com`, the blank host is not set, and the token
together with the token provider is refused with an argument exception.

**Requirement coverage**: `CanvasPilot-OTS-AgentKit-SignInWithCopilotCli`.
