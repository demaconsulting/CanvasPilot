### AgentToolRegistry

#### Verification Approach

`AgentToolRegistry` is verified with unit tests defined in `AgentToolRegistryTests.cs` (the path policy, the packs and
their order, the optional pack catalog, and the check of the set) and `DescriptorToolPackTests.cs` (the descriptor tool
pack and the function that carries a tool description). `ToolTestDoubles.cs` holds the doubles: tool descriptions that
record the JSON text of their calls, a pack that records the policy it is given, a function whose name the naming rule
of the AI library does not see, and an argument value that cannot be written as JSON. The real AI library is used
(there is no model, no network, and no Copilot CLI): the policy and the packs are the library's own, so the tests show
that the unit builds them correctly and that the library's decisions are the ones that the design relies on.

The policy tests use a real temporary folder with a project folder, a folder beside it whose name begins like the
project's (`project-evil`), and, on Windows, a folder junction that leads from the project to a folder outside it. The
round-trip tests compare the schema of a description with the schema of the function as JSON trees (not as text), and
call the real `canvasnet_help` description over a small catalog. The registry tests use the real description of the
drawing knowledge and stand-ins for the two style tools that have their real names and schemas
(`StyleTools.GetParametersSchema` and `CheckParametersSchema`), so no style service is needed. No test reads the
library's own list of reserved built-in names, which is not public; the registry's list is kept complete by the step of
upgrading the AI library that is described in the design of the unit.

The subsystem test (`AgentSubsystemTests.cs`) and the system test (`CanvasPilotTests.cs`) show the confinement as the
copilot meets it: the tools of a project are only the application's own tools, the knowledge tool answers from the real
cards, and the policy that a supplied pack receives allows a picture file of the project and refuses the data folder,
the project file that the project service really wrote, and a place outside the project.

#### Test Environment

The tests use a temporary folder that each test class makes and deletes (a junction in it is removed as a link before
the folder is deleted). The junction test is skipped on a platform other than Windows. No network, display, or external
service is used.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The policy has the project folder as its working directory (with or without a trailing separator) and exactly one
  grant, for reading and writing, on that folder, with the deny patterns `.canvaspilot` and `*.canvaspilot`; the limits
  given are the limits of the policy, and the defaults apply when none are given.
- A file at the top of the project, one below a folder that does not exist yet, and one given by its full path are
  permitted; the data folder, a project file, and both of them below a folder are refused for reading and for writing;
  a path that climbs out with `..`, a full path outside, a folder beside the project that starts with its name, and a
  network path are refused, each with a message and not an exception.
- A folder link inside the project that leads outside it is permitted by the policy and keeps its spelling inside the
  project (the finding that the project tools must refuse links themselves).
- An empty, white space, null, or relative project folder is refused.
- The function of a description has the name, the words, and the schema of the description, as JSON equal to the
  description's text, before and after it is wrapped with the limits.
- A call with JSON elements, with plain values (a string, a number, a boolean, a list), and with no arguments gives the
  description one JSON object (`{}` for none); the answer of the description comes back as text.
- A description that throws, arguments that cannot be written, and a null answer give a sentence that names the tool and
  the exception type without the text of the exception, or empty text; the description is not called when the arguments
  cannot be written. A canceled token throws the cancellation and does not call the description.
- An answer over the ceiling of the limits is refused with a message that names `ResourceTooLarge` and does not contain
  the answer.
- A pack refuses an empty or null prefix, a name that does not start with the prefix, a name that breaks the naming
  rule, a name used twice, a description with a schema that is not a JSON object, and one with no words or no function.
- The registry offers `canvasnet_help`, `style_get`, `style_check` in that order, then the tools of the project packs,
  then those of the chosen optional packs in the order of their identifiers; the project pack supplier gets the context
  and its pack gets the confined policy.
- The optional catalog is empty by default, an identifier that is not in it is refused with its name, only chosen packs
  are made, and a pack that needs vision is not offered to a host without it and is offered to a host with it.
- A set with no tool, a tool name used twice, a name like a built-in tool of the Copilot CLI, or two packs with one
  prefix is refused with a message that names the problem.

#### Test Scenarios

**AgentToolRegistry_CreatePolicy_ProjectFolder_HasOneReadWriteGrantThatDeniesTheDataFolderAndProjectFiles**: The policy
made for the project folder (with and without a trailing separator) has it as working directory and one read-write grant
with the two deny patterns; the limits given are carried and the defaults apply otherwise. This scenario is tested by
`AgentToolRegistry_CreatePolicy_ProjectFolder_HasOneReadWriteGrantThatDeniesTheDataFolderAndProjectFiles`.

**AgentToolRegistry_CreatePolicy_PathsInsideTheProject_ArePermittedForReadAndWrite**: A top-level file, a file below a
folder that does not exist, and a full path inside the project are permitted and resolve inside the project. This
scenario is tested by `AgentToolRegistry_CreatePolicy_PathsInsideTheProject_ArePermittedForReadAndWrite`.

**AgentToolRegistry_CreatePolicy_DataFolderAndProjectFiles_AreRefusedForReadAndWrite**: The data folder, a project file,
and both below a folder are refused for reading and writing with a message. This scenario is tested by
`AgentToolRegistry_CreatePolicy_DataFolderAndProjectFiles_AreRefusedForReadAndWrite`.

**AgentToolRegistry_CreatePolicy_PathsOutsideTheProject_AreRefusedForReadAndWrite**: Climbing out, a full path outside,
a look-alike sibling folder, and a network path are refused. This scenario is tested by
`AgentToolRegistry_CreatePolicy_PathsOutsideTheProject_AreRefusedForReadAndWrite`.

**AgentToolRegistry_CreatePolicy_FolderLinkInsideTheProject_IsPermittedAndMustBeRefusedByTheTools**: A junction inside
the project to a folder outside it is permitted by the policy for reading and keeps its spelling inside the project. The
test pins the behavior of the library; when a later version of the library follows links, the test fails and the project
tools can be simplified. This scenario is tested by
`AgentToolRegistry_CreatePolicy_FolderLinkInsideTheProject_IsPermittedAndMustBeRefusedByTheTools`.

**AgentToolRegistry_CreatePolicy_EmptyOrRelativeFolder_IsRefused**: Null, empty, white space, and a relative folder are
refused. This scenario is tested by `AgentToolRegistry_CreatePolicy_EmptyOrRelativeFolder_IsRefused`.

**AgentToolRegistry_CreateTools_BuiltInPacks_OfferKnowledgeThenStyleToolsWithTheirSchemas**: The registry offers the
three tools in order with the schemas of their descriptions, and a call reaches the description. This scenario is tested
by `AgentToolRegistry_CreateTools_BuiltInPacks_OfferKnowledgeThenStyleToolsWithTheirSchemas`.

**AgentToolRegistry_CreateTools_ProjectPacks_AreGivenTheContextAndTheConfinedPolicyAndComeLast**: A supplied pack's tool
comes after the built-in ones, the supplier gets the context, and the pack gets the confined policy. This scenario is
tested by `AgentToolRegistry_CreateTools_ProjectPacks_AreGivenTheContextAndTheConfinedPolicyAndComeLast`.

**AgentToolRegistry_CreateTools_EmptyOptionalCatalog_AcceptsNoChoiceAndRefusesAnyId**: The default catalog is empty, an
empty choice gives the three tools, and an identifier is refused with its name. This scenario is tested by
`AgentToolRegistry_CreateTools_EmptyOptionalCatalog_AcceptsNoChoiceAndRefusesAnyId`.

**AgentToolRegistry_CreateTools_OptionalCatalog_AddsOnlyTheChosenPacksInOrderOfTheirIds**: Of three catalog entries two
are chosen, only their factories run, and their tools follow in the order of the identifiers. This scenario is tested by
`AgentToolRegistry_CreateTools_OptionalCatalog_AddsOnlyTheChosenPacksInOrderOfTheirIds`.

**AgentToolRegistry_CreateTools_OptionalPackNeedingVision_FollowsTheHostCapabilities**: A pack that needs vision is not
offered without the ability and is offered with it. This scenario is tested by
`AgentToolRegistry_CreateTools_OptionalPackNeedingVision_FollowsTheHostCapabilities`.

**AgentToolRegistry_CreateTools_RepeatedToolName_IsRefusedWithTheName**: A pack that offers one tool twice gives a
refused set that names the tool. This scenario is tested by
`AgentToolRegistry_CreateTools_RepeatedToolName_IsRefusedWithTheName`.

**AgentToolRegistry_CreateTools_CopilotBuiltInName_IsRefusedWithTheName**: A tool named like the built-in
`report_intent` is refused with its name and the words "Copilot CLI". This scenario is tested by
`AgentToolRegistry_CreateTools_CopilotBuiltInName_IsRefusedWithTheName`.

**AgentToolRegistry_CreateTools_RepeatedPackPrefix_IsRefusedWithThePrefix**: A project pack with the prefix of the style
pack is refused with the prefix. This scenario is tested by
`AgentToolRegistry_CreateTools_RepeatedPackPrefix_IsRefusedWithThePrefix`.

**AgentToolRegistry_CreateTools_NoTool_IsRefused**: A registry with no descriptions and no pack gives a refused set.
This scenario is tested by `AgentToolRegistry_CreateTools_NoTool_IsRefused`.

**AgentToolRegistry_ArgumentsAreChecked_NullsAndRelativeFolder_AreRefused**: Null descriptions, a null context, a null
choice, and a relative project folder are refused. This scenario is tested by
`AgentToolRegistry_ArgumentsAreChecked_NullsAndRelativeFolder_AreRefused`.

**AgentToolRegistry_Constructor_BuiltInDescriptionWithTheWrongPrefix_IsRefused**: A style tool in the list of the
knowledge tools, and the other way round, are refused when the registry is made. This scenario is tested by
`AgentToolRegistry_Constructor_BuiltInDescriptionWithTheWrongPrefix_IsRefused`.

**DescriptorToolPack_CreateTools_SchemaAndWordsOfTheDescription_ComeOutAsTheFunction**: The schema, name, and words of a
description are those of the bare and of the wrapped function; the prefix is the pack's and it needs no capability. This
scenario is tested by `DescriptorToolPack_CreateTools_SchemaAndWordsOfTheDescription_ComeOutAsTheFunction`.

**DescriptorToolPack_Invoke_JsonElementArguments_ReachTheDescriptionAsJsonAndTheAnswerComesBack**: A JSON element
argument with quotes and a non-ASCII letter reaches the description as JSON and the answer comes back. This scenario is
tested by `DescriptorToolPack_Invoke_JsonElementArguments_ReachTheDescriptionAsJsonAndTheAnswerComesBack`.

**DescriptorToolPack_Invoke_PlainValueArguments_AreWrittenAsJson**: A string, a number, a boolean, and a list are
written as JSON of their own kind. This scenario is tested by
`DescriptorToolPack_Invoke_PlainValueArguments_AreWrittenAsJson`.

**DescriptorToolPack_Invoke_NoArguments_GivesAnEmptyJsonObject**: A call without arguments gives `{}`. This scenario is
tested by `DescriptorToolPack_Invoke_NoArguments_GivesAnEmptyJsonObject`.

**DescriptorToolPack_Invoke_DescriptionThrows_IsAnAnswerThatNamesToolAndTypeOnly**: An exception with a path in its text
becomes a sentence with the tool and the exception type and without the path. This scenario is tested by
`DescriptorToolPack_Invoke_DescriptionThrows_IsAnAnswerThatNamesToolAndTypeOnly`.

**DescriptorToolPack_Invoke_UnwritableArguments_IsAnAnswerAndTheDescriptionIsNotCalled**: A value that throws when it is
written gives a sentence and no call of the description. This scenario is tested by
`DescriptorToolPack_Invoke_UnwritableArguments_IsAnAnswerAndTheDescriptionIsNotCalled`.

**DescriptorToolPack_Invoke_DescriptionAnswersNull_IsAnEmptyAnswer**: A null answer is empty text. This scenario is
tested by `DescriptorToolPack_Invoke_DescriptionAnswersNull_IsAnEmptyAnswer`.

**DescriptorToolPack_Invoke_CanceledToken_ThrowsCancellationAndDoesNotCall**: A canceled token throws the cancellation
and the description is not called. This scenario is tested by
`DescriptorToolPack_Invoke_CanceledToken_ThrowsCancellationAndDoesNotCall`.

**DescriptorToolPack_Invoke_AnswerOverTheCeiling_IsRefusedByTheLimits**: With a ceiling of 100 characters, an answer of
10 is delivered and one of 5,000 is refused with `ResourceTooLarge` and without the text. This scenario is tested by
`DescriptorToolPack_Invoke_AnswerOverTheCeiling_IsRefusedByTheLimits`.

**DescriptorToolPack_CreateTools_RealKnowledgeDescriptor_RoundTripsSchemaAndAnswer**: The real `canvasnet_help`
description gives a function with its schema, which answers the index and a card. This scenario is tested by
`DescriptorToolPack_CreateTools_RealKnowledgeDescriptor_RoundTripsSchemaAndAnswer`.

**DescriptorToolPack_Constructor_BadPrefixOrNullArguments_AreRefused**: Null arguments and an empty prefix are refused.
This scenario is tested by `DescriptorToolPack_Constructor_BadPrefixOrNullArguments_AreRefused`.

**DescriptorToolPack_Constructor_BadNames_AreRefusedWithTheName**: A name of another pack, a name with capital letters,
and a name used twice are refused with the name; an empty pack is accepted. This scenario is tested by
`DescriptorToolPack_Constructor_BadNames_AreRefusedWithTheName`.

**DescriptorToolPack_Constructor_UnusableDescriptions_AreRefused**: A schema that is not JSON, not an object, or empty,
a description without words, without a function, or without a name are refused. This scenario is tested by
`DescriptorToolPack_Constructor_UnusableDescriptions_AreRefused`.

**DescriptorToolPack_CreateTools_NullPolicy_IsRefused**: Asking for the tools without a policy is refused. This scenario
is tested by `DescriptorToolPack_CreateTools_NullPolicy_IsRefused`.
