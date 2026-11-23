### ProviderPill

#### Verification Approach

`ProviderPill` is verified at two levels. Unit tests in `ProviderPillTests.cs` drive the view model over a real chat
panel (with a real composer, mark registry, and annotation layer) whose copilot is a hand-written controlled agent that
returns the readiness report that the test chooses, and over real settings on a temporary folder. Nothing of the pill is
substituted. A headless UI test, `MainWindowView_ProviderPill_InTheTitleBar_OpensTheSettingsOnTheProviderPage` in
`MainWindowViewTests.cs` of the main window, shows the real window, finds the pill by automation identifier, uses it
with the pointer, and reads the page of the real settings dialog that opens. The color of the dot for each state follows
from the state classes of the view and is checked by looking at the screens and not by pixels; the tests prove the state
that the dot follows.

#### Test Environment

Unit tests use the standard test environment with temporary folders. The UI test runs under the Avalonia headless
platform with the Skia renderer, serialized with test parallelization disabled; no display is required.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- Before the first answer the pill is checking, with the words "checking"; after a ready answer it shows Copilot and the
  model of the settings, with the sentence of the check as its tooltip and the sentence "AI provider: Copilot, {model}.
  Opens the settings." as its name.
- A ready answer that names the account and the kind of sign-in has that sentence, unchanged, as the tooltip.
- Each of the ten results of the check that are not ready has its own words and state: the state is an error for an
  unavailable runtime and needs attention for the others; the tooltip is the sentence of the check; the words are in the
  name.
- A change of the settings to Ollama with no model names Ollama and the default model and is announced.
- A second check shows checking again and then ready; using the pill runs its command once; a pill over a chat panel
  without a copilot is an error with the tooltip "There is no AI provider in this application."
- A disposed pill announces nothing; a missing chat panel, settings service, or command is refused.
- In the window the pill is in the title bar inside its right edge, 26 pixels high, has a name that starts with
  "AI provider: Copilot, ", and using it opens the settings on the "AI Provider" page.

#### Test Scenarios

**ProviderPill_CheckThenReady_ShowsCheckingThenProviderAndModel**: With a model chosen in the settings and a ready
answer prepared, the pill is checking before the chat panel checks, and after it is ready with `Copilot`, the model, the
separated detail, the tooltip of the check, and the accessible name. This scenario is tested by
`ProviderPill_CheckThenReady_ShowsCheckingThenProviderAndModel`.

**ProviderPill_ReadyWithSignIn_ToolTipNamesTheAccountAndTheKindOfSignIn**: A ready check whose sentence is "GitHub
Copilot CLI 1.0.114 is ready. Signed in as mnixon on github.com (Copilot CLI sign-in)." gives a ready pill whose tooltip
is exactly that sentence, so the user sees the account and the kind of sign-in. This scenario is tested by
`ProviderPill_ReadyWithSignIn_ToolTipNamesTheAccountAndTheKindOfSignIn`.

**ProviderPill_CheckFindsAProblem_SaysItInWordsAndPicksTheState**: For each result of the check that is not ready (not
set up, CLI not found, CLI too old, sign-in needed, unavailable, Ollama not running, model missing, small context, model
not usable, and cannot see pictures) the pill has the state, the words, and the tooltip sentence that belong to it, and
the words are in the name. This scenario is tested by `ProviderPill_CheckFindsAProblem_SaysItInWordsAndPicksTheState`.

**ProviderPill_SettingsChooseOllama_ShowsTheOtherProviderAndTheDefaultModel**: A ready pill is announced a change when
the settings choose Ollama with no model, and it names Ollama and the default model. This scenario is tested by
`ProviderPill_SettingsChooseOllama_ShowsTheOtherProviderAndTheDefaultModel`.

**ProviderPill_RecheckAndNoCopilot_CheckingAgainAndErrorWithoutCopilot**: A second check passes through checking and
ends ready; using the pill runs its command; a pill over a chat panel with no copilot is in error with the sentence that
there is no AI provider. This scenario is tested by
`ProviderPill_RecheckAndNoCopilot_CheckingAgainAndErrorWithoutCopilot`.

**ProviderPill_NullArgumentsAndDispose_RefusesNullAndStopsFollowing**: A disposed pill announces nothing when the chat
panel checks or the settings change, and each missing argument is refused. This scenario is tested by
`ProviderPill_NullArgumentsAndDispose_RefusesNullAndStopsFollowing`.

**MainWindowView_ProviderPill_InTheTitleBar_OpensTheSettingsOnTheProviderPage**: In the shown window the pill is a
descendant of the title bar, 26 pixels high, inside its right edge, with a name that starts with "AI provider: Copilot,
"; it ends with one gear drawn as lines, which is inside the pill and to the right of the words, not a filled glyph;
using it opens the settings dialog on the page titled "AI Provider". This scenario is tested by
`MainWindowView_ProviderPill_InTheTitleBar_OpensTheSettingsOnTheProviderPage`.

The requirements are verified by these scenarios as follows: *ShowProviderAndModel* by the ready and the Ollama
scenarios; *ShowTheStateWithWords* by the ready, the problem, and the recheck scenarios; *ExplainInTooltip* by the same
three and the sign-in scenario; *ReportMissingProvider* by the recheck scenario; *OpenSettings* by the recheck and the
window scenarios; *ShowInTitleBar* by the window scenario; *AnnounceToScreenReader* by the ready, the problem, and the
window scenarios; and *StopFollowingWhenDisposed* by the disposal scenario.
