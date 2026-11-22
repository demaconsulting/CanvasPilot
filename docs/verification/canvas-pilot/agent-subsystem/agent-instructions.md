### AgentInstructions

#### Verification Approach

`AgentInstructions` is verified with unit tests in `AgentInstructionsTests.cs` and with one subsystem test in
`AgentSubsystemTests.cs`. The unit tests use the real parts and no stand-in for them: the real cards of the application
(`KnowledgeCatalog.Default` and the real `KnowledgePromptBuilder`), the real role prompt that is embedded in the
assembly, and the real style kits that come with the program (read from the folder beside the test assembly, the same
way the `StyleGuidance` tests read them). The size tests are measurements and not estimates: they build the instructions
for every kit in the folder, with a project whose name and folder have 150 characters each and a list of 100 files with
names of more than 150 characters, which is the largest input that the unit accepts, and compare the number of
characters with the budget of the tier. The tool text is measured on the functions that `AgentToolRegistry` builds from
the real description of the drawing knowledge tool, the two real style tools (with stand-ins for the services that they
read), and the four real project tools: the sum of the name, the description, and the JSON Schema of each of the seven
tools.

The tests of the parts call `Build` for a kit and a few files and look at the text for each part in its order; the facts
tests use files with line breaks, with names that are too long, with a blank name, and with backslashes. The subsystem
test builds the instructions and the tools of one project, as the agent service will, for a large and a small window,
and checks that the tool names in the role prompt are exactly the names of the tools that the registry offers.

#### Test Environment

The tests read the kits that come with the program from the output folder of the test assembly and make one temporary
project folder in the subsystem test, which they delete. No network, display, or external service is used.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The role prompt that comes with the application has at most 3,000 characters and says: draw with CanvasNet in the open
  project; use only the tools given, and names the seven tools; ask for a card before using a format that is not known;
  call `style_get` before drawing and `style_check` after; look at the picture with `render_image` after every change;
  example images of a style can be looked at with `render_image` only; use paths relative to the project folder and
  never invent a file; read a file with `project_read` before replacing it; `project_write` overwrites at once with no
  backup and the user relies on source control; explain briefly.
- A role prompt that is given is used with its line breaks made the same; one that is empty, white space, or one
  character over 3,000 is refused, and so is a missing writer of the index.
- A window of 16,384 tokens or more, and every GitHub Copilot model, gets the full tier; 16,383 tokens and fewer get the
  compact tier.
- The full text is the role prompt, the index of every card, the facts, and the style with the example images, in that
  order, with no carriage return and no final line feed; the facts have the name, the folder, the files with forward
  slashes, and the name and id of the kit.
- The compact text has the same role prompt, the index of the top cards only, the style without the example images, and
  at most 15 files; the full text has every card and the example images.
- A list of files that is longer than the limit is cut and says how many of how many are listed and that `project_list`
  gives the rest; names that are blank are left out; a name with a line break is written on one line; a name longer than
  120 characters is cut with an ellipsis; a list whose paths are longer than the character limit of the tier stops at
  the limit and always lists at least one file; no file gives its own sentence.
- A project without a kit, and one that names a kit that cannot be found, each get their own sentence at the end.
- The same input gives the same text; a missing facts object and a tier that is not defined are refused.
- For every kit that comes with the program, and the largest project, the full text has at most 20,000 characters and
  the compact text at most 11,000; the words and schemas of the seven tools together have at most 7,000. The sizes
  measured when the unit was made were 18,025 to 18,180 characters (full), 6,380 to 6,538 (compact), and 4,527 (tools),
  so the limits leave room of about 1,800, 4,400, and 2,500 characters.
- The tool names in the role prompt are the names of the tools of the registry, no more and no fewer.

#### Test Scenarios

**AgentInstructions_RolePrompt_Embedded_IsWithinBudgetAndStatesTheRules**: The embedded role prompt has between 500 and
3,000 characters, no carriage return, and each of the sentences of the rules above by its key words and each of the
seven tool names. This scenario is tested by `AgentInstructions_RolePrompt_Embedded_IsWithinBudgetAndStatesTheRules`.

**AgentInstructions_Constructor_RolePromptAndPrompts_AreCheckedAndUsed**: A given prompt is trimmed and has its line
breaks made the same; a prompt of exactly 3,000 characters is used and one of 3,001, an empty one, and a white space one
are refused, and so is a missing writer of the index. This scenario is tested by
`AgentInstructions_Constructor_RolePromptAndPrompts_AreCheckedAndUsed`.

**AgentInstructions_ChooseTier_ProviderAndWindow_FullFromSixteenThousandTokensOrCopilot**: GitHub Copilot with 4,096 and
32,768 tokens, and Ollama with 32,768 and 16,384, get the full tier; Ollama with 16,383, 8,192, and 512 gets the compact
tier. This scenario is tested by
`AgentInstructions_ChooseTier_ProviderAndWindow_FullFromSixteenThousandTokensOrCopilot`.

**AgentInstructions_Build_FullTier_HasRoleIndexFactsAndStyleInOrder**: For the kit `clean` and a project with three
files the text starts with the role prompt, holds the whole index after it, then the facts (the name, the folder, the
files with a backslash turned into a slash), then the line with the name and id of the kit and the block of the style
guidance with the example images, and has no carriage return and no final line feed. This scenario is tested by
`AgentInstructions_Build_FullTier_HasRoleIndexFactsAndStyleInOrder`.

**AgentInstructions_Build_CompactTier_HasTopLevelIndexAndNoExamples**: For 30 files, the compact text has the index of
the top cards and not the whole index, the palette but no example images, and the first 15 files with the sentence that
says 15 of 30; the full text has the whole index and all 30 files. This scenario is tested by
`AgentInstructions_Build_CompactTier_HasTopLevelIndexAndNoExamples`.

**AgentInstructions_Build_Files_AreLimitedCleanedAndCountedHonestly**: Of 44 files that count (one with a line break,
one of 200 characters, a blank one, and 42 short ones), 40 are listed with the sentence `the first 40 of 44`; the line
break becomes a space, the long name is cut to 120 characters with an ellipsis, and a project with no file says that it
has none. This scenario is tested by `AgentInstructions_Build_Files_AreLimitedCleanedAndCountedHonestly`.

**AgentInstructions_Build_LongNames_StopTheListAtTheCharacterLimit**: Of 50 files with names of 100 characters, the full
tier lists 16 and the compact tier 5 with the sentence that counts them, and a single name of 600 characters is cut to a
line and listed. This scenario is tested by `AgentInstructions_Build_LongNames_StopTheListAtTheCharacterLimit`.

**AgentInstructions_Build_NoStyleOrMissingStyle_SaysSo**: A project with no kit ends with the sentence that it has none;
one that names the kit `team-kit` that cannot be found ends with the sentence that names it. This scenario is tested by
`AgentInstructions_Build_NoStyleOrMissingStyle_SaysSo`.

**AgentInstructions_Build_SameInput_SameTextAndBadArgumentsAreRefused**: Two instances over the same cards write the
same text for the same facts; missing facts and a tier of 7 are refused. This scenario is tested by
`AgentInstructions_Build_SameInput_SameTextAndBadArgumentsAreRefused`.

**AgentInstructions_Build_FullTier_EveryBuiltInKitAndLongestFileList_FitsTwentyThousandCharacters**: The full text of
every kit in the folder of the built-in kits, for the largest project, has at most 20,000 characters, and at least three
kits are measured. This scenario is tested by
`AgentInstructions_Build_FullTier_EveryBuiltInKitAndLongestFileList_FitsTwentyThousandCharacters`.

**AgentInstructions_Build_CompactTier_EveryBuiltInKitAndLongestFileList_FitsElevenThousandCharacters**: The same for the
compact text and 11,000 characters. This scenario is tested by
`AgentInstructions_Build_CompactTier_EveryBuiltInKitAndLongestFileList_FitsElevenThousandCharacters`.

**AgentInstructions_ToolSchemas_AllToolsOfTheCopilot_FitSevenThousandCharacters**: The registry builds seven tools from
the real descriptions and the project tools, and the sum of the lengths of their names, descriptions, and JSON Schemas
is at most 7,000 characters. This scenario is tested by
`AgentInstructions_ToolSchemas_AllToolsOfTheCopilot_FitSevenThousandCharacters`.

**AgentSubsystem_InstructTheCopilot_ProjectWithKit_InstructionsNameExactlyTheToolsThatAreOffered**: The subsystem
scenario that is described in the verification of the Agent subsystem. This scenario is tested by
`AgentSubsystem_InstructTheCopilot_ProjectWithKit_InstructionsNameExactlyTheToolsThatAreOffered`.
