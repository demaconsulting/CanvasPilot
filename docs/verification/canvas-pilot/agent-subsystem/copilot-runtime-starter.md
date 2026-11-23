### CopilotRuntimeStarter

#### Verification Approach

`CopilotRuntimeStarter` is verified with unit tests defined in `CopilotRuntimeStarterTests.cs`. The runtime of the AI
library is sealed and starts the Copilot CLI as a process, so the tests use the one seam that the unit offers: the
function that starts a runtime from the options. A stand-in function records the options that the library would have
got and gives a fake source of agents (`FakeCopilotAgentSource`, which counts how often it is disposed); other stand-ins
throw the exceptions that the library throws, wait for the token, count their calls, or fail once. The options are real
objects of the library, so the tests also see what the library itself accepts and refuses: the installed CLI as the
source, the full path of the CLI, the data folder, the logger, no token, no token provider, no host, and the sign-in of
the Copilot CLI left on. No Copilot CLI is run, no network is used,
and no credentials are needed. The messages of the library that the starter must recognize are written into the tests in
the words of the library (for example "is not signed in to GitHub", and the multi-line explanation with its numbered
findings), so a change of the library that changes those
words is found by the OTS tests of the library and by the sentences here.

Whether a real runtime can start on the installed CLI of a machine is not shown by these tests; it is shown by the live
tests that are recorded in the manual verification of the system, and the library's own behavior is the subject of the
OTS tests of AgentKit.

#### Test Environment

N/A - standard test environment. The tests use paths below the temporary folder only as names (nothing is created
there), no network, no process, and no display. The test of the cancellation waits a tenth of a second for its token,
and the test of the shared runtime waits fifty milliseconds in its start function so that the eight asks overlap.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A start passes the installed CLI as the source of the runtime, the full path of the CLI that the locator found, the
  data folder, and the logger of the request to the library, and no token, no token provider, and no host, and leaves
  the sign-in of the Copilot CLI on; it answers started with "GitHub Copilot CLI 1.0.114 is running." and the runtime
  as the source of agents, and the source carries the sign-in that the runtime reported.
- The sentence about a sign-in names the login, the host without its scheme and closing slash, and the kind of sign-in
  ("GitHub token", "token variable", "Copilot CLI sign-in", "stored sign-in", or "GitHub CLI sign-in"), leaves out
  whatever is not known, and is empty when nothing is known.
- The data folder of the request is used when there is one, otherwise the folder of the starter, otherwise the
  folder of the application below the local data of the user.
- Eight asks at once for the same CLI and folder start one runtime and all get it; a later ask gets it too; it is not
  disposed by being handed out.
- An ask for another CLI, or for another folder, stops the old runtime once and starts a new one; the last runtime runs.
- A start that fails is not remembered: the next ask starts again and can succeed.
- A message of the library that says "is not signed in" gives `NotSignedIn` with the documented sign-in sentence
  (run `copilot` and type `/login`, with `gh auth login` and the token variables as alternatives); when the message has
  the numbered findings of the library, the sentence is followed by " What was found: " and the findings of the places
  after the first, on one line, ending with a full stop, with none of the advice of the library and no line break; one
  that says "too old" or "protocol version" gives `CliTooOld` with the message of the library; any other message gives
  `Failed` with the message of the library, and a blank message gives `Failed` with the documented sentence to try
  again. No agent source is handed out with any of them.
- A relative path of the CLI is refused by the library when the options are made, and the answer is `Failed` with
  "The Copilot CLI path in Settings cannot be used: " and the cause without the name of the parameter; nothing is
  started.
- A cancelled start throws a cancellation exception and is not remembered.
- Disposing stops the runtime once, disposing again does nothing, and a start afterwards throws
  `ObjectDisposedException`.
- A missing request is refused with an argument null exception and a blank path of the CLI with an argument exception.

#### Test Scenarios

**CopilotRuntimeStarter_Constants_Rule_AreTheDocumentedSentences**: The sign-in sentence is exactly "Copilot is not
signed in for CanvasPilot. Run `copilot`, then type `/login` to sign in (or sign in with `gh auth login`, or set the
COPILOT_GITHUB_TOKEN, GH_TOKEN, or GITHUB_TOKEN environment variable), then choose Check connection."; the beginning of
the sentence for a path that cannot be used is "The Copilot CLI path in Settings cannot be used: "; and the sentence for
a start that failed without a reason is "The Copilot runtime could not be started. Choose Check connection to try
again." This scenario is tested by
`CopilotRuntimeStarter_Constants_Rule_AreTheDocumentedSentences`.

**CopilotRuntimeStarter_StartAsync_InstalledCli_PassesThePathTheFolderAndNoToken**: A starter with a data folder, a
recording start function, and a request for CLI 1.0.114 with a logger. The answer is started with the sentence "GitHub
Copilot CLI 1.0.114 is running." and the one fake runtime as its agents. The one set of options has the installed CLI as
its source, the path of the request as the CLI, the folder of the starter, the logger of the request, no token, no token
provider, no host, and the sign-in of the Copilot CLI allowed. This
scenario is tested by `CopilotRuntimeStarter_StartAsync_InstalledCli_PassesThePathTheFolderAndNoToken`.

**CopilotRuntimeStarter_StartAsync_RuntimeSignedIn_TheSourceCarriesTheSignIn**: A runtime that signed in as `mnixon` on
`https://github.com` through the Copilot CLI. The answer of the start carries that sign-in on its source of agents,
unchanged. This scenario is tested by `CopilotRuntimeStarter_StartAsync_RuntimeSignedIn_TheSourceCarriesTheSignIn`.

**CopilotSignInInfo_Describe_KnownParts_AreNamedAndUnknownPartsAreLeftOut**: Eight combinations of source, login, and
host: the Copilot CLI sign-in gives "Signed in as mnixon on github.com (Copilot CLI sign-in)."; a token variable with a
host that has a scheme and a closing slash gives "Signed in as mnixon on example.ghe.com (token variable)."; a host
without a scheme and the GitHub CLI give "... (GitHub CLI sign-in)."; a token and a token provider give "(GitHub token)"
with the login or the host missing; a stored sign-in with blank login and host gives "Signed in (stored sign-in).";
an unknown source leaves out the parentheses; and nothing known gives an empty string. This scenario is tested by
`CopilotSignInInfo_Describe_KnownParts_AreNamedAndUnknownPartsAreLeftOut`.

**CopilotRuntimeStarter_StartAsync_RequestNamesAFolder_TheFolderOfTheRequestIsUsed**: A request that names a folder
while the starter has another. The options carry the folder of the request. This scenario is tested by
`CopilotRuntimeStarter_StartAsync_RequestNamesAFolder_TheFolderOfTheRequestIsUsed`.

**CopilotRuntimeStarter_StartAsync_NoFolderAnywhere_UsesTheApplicationFolder**: A starter without a folder and a request
without one. The options carry the folder that `AgentService.GetCopilotDataDirectory` gives, so the library never
chooses a folder of its own. This scenario is tested by
`CopilotRuntimeStarter_StartAsync_NoFolderAnywhere_UsesTheApplicationFolder`.

**CopilotRuntimeStarter_StartAsync_SameCliAndFolder_SharesOneRuntime**: A start function that takes fifty milliseconds
and counts its calls, eight asks at once for the same CLI and folder, and one ask afterwards. The function was called
once, every answer has the same runtime, and the runtime was not disposed. This scenario is tested by
`CopilotRuntimeStarter_StartAsync_SameCliAndFolder_SharesOneRuntime`.

**CopilotRuntimeStarter_StartAsync_OtherCliOrOtherFolder_ReplacesTheRuntime**: A start, an ask for another CLI, and an
ask for the other CLI with another folder. Three runtimes were made, each answer has its own, and the first two were
disposed once each and the third not. This scenario is tested by
`CopilotRuntimeStarter_StartAsync_OtherCliOrOtherFolder_ReplacesTheRuntime`.

**CopilotRuntimeStarter_StartAsync_FailureThenSuccess_TriesAgain**: A start function that throws an
`InvalidOperationException` with the text "Not this time." on its first call. The first answer is `Failed` with that
text and no agents; the second ask starts and answers started with the runtime. This scenario is tested by
`CopilotRuntimeStarter_StartAsync_FailureThenSuccess_TriesAgain`.

**CopilotRuntimeStarter_StartAsync_LibraryFailure_BecomesTheMatchingStatusAndSentence**: Five messages of the library,
each thrown by the start function: "not signed in" (with no numbered findings) gives `NotSignedIn` and the sign-in
sentence alone; "version 1.0.106, which
is too old" and "protocol version mismatch" give `CliTooOld` with the message of the library; a message that the starter
does not recognize gives `Failed` with the message; a blank message gives `Failed` with the sentence to try again. No
runtime is handed out in any case. This scenario is tested by
`CopilotRuntimeStarter_StartAsync_LibraryFailure_BecomesTheMatchingStatusAndSentence`.

**CopilotRuntimeStarter_StartAsync_NotSignedInWithFindings_AddsAOneLineDigestOfTheFindings**: The explanation of the
library for a runtime that is not signed in, as it words it with its line breaks: a first line, four indented numbered
findings (the token of the host, the variables, the Copilot CLI sign-in, and the GitHub CLI), and its own advice. The
answer is `NotSignedIn`, and the message is the sign-in sentence followed by " What was found: " and the findings of
places two to four joined with semicolons on one line, ending with a full stop, with no line break and no "To sign in"
advice, and no agents. This scenario is tested by
`CopilotRuntimeStarter_StartAsync_NotSignedInWithFindings_AddsAOneLineDigestOfTheFindings`.

**CopilotRuntimeStarter_StartAsync_PathTheLibraryRefuses_ReportsItAsASentenceAndStartsNothing**: A request whose CLI is
the relative path `copilot.exe`. The answer is `Failed`, starts with the beginning of the sentence for a path that
cannot be used, holds the words "not fully qualified", and does not hold "(Parameter"; the start function was not
called. This scenario is tested by
`CopilotRuntimeStarter_StartAsync_PathTheLibraryRefuses_ReportsItAsASentenceAndStartsNothing`.

**CopilotRuntimeStarter_StartAsync_Cancelled_ThrowsAndTheNextAskStartsAgain**: A start function that waits for its token
on the first call and works on the second, and a token that is cancelled after a tenth of a second. The first ask throws
a cancellation exception; the second ask starts and answers started. This scenario is tested by
`CopilotRuntimeStarter_StartAsync_Cancelled_ThrowsAndTheNextAskStartsAgain`.

**CopilotRuntimeStarter_DisposeAsync_StopsTheRuntimeOnceAndRefusesFurtherStarts**: A started runtime and two disposals
of the starter. The runtime was disposed once, and a start afterwards throws `ObjectDisposedException`. This scenario is
tested by `CopilotRuntimeStarter_DisposeAsync_StopsTheRuntimeOnceAndRefusesFurtherStarts`.

**CopilotRuntimeStarter_StartAsync_BadArguments_AreRefused**: A missing request throws an argument null exception, and a
request whose CLI path is white space throws an argument exception. This scenario is tested by
`CopilotRuntimeStarter_StartAsync_BadArguments_AreRefused`.
