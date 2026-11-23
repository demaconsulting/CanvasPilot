### PushToTalkBinding

#### Verification Approach

`PushToTalkBinding` and `PushToTalkSetting` are verified with unit tests in `PushToTalkBindingTests.cs` (one test class
for both, because the setting is the observable holder of the binding). The binding is a pure value, so its tests call
the real code directly with keys and modifiers from Avalonia's `Key` and `KeyModifiers` and need no substitute. The
setting is tested over a real `SettingsService` on a temporary settings folder, so the test changes the talk key through
the same `Update` call that the Speech page's OK button leads to and checks what the setting announces. No test double
is used. The other places that use the binding are verified in their own units: the cleaning of the stored key in
*SettingsService Verification*, the capture box in *SpeechSettingsPage Verification*, the key events of the window in
*MainWindowShell Verification*, and the hints in *StatusBar Verification* and *Welcome Verification*. The unit does not
know what other programs have registered as shortcuts, so that a key such as F9 is swallowed by a particular computer
cannot be tested; it is checked by hand on a laptop whose function keys have other functions (see *Manual Verification*
of the Speech subsystem).

#### Test Environment

Unit tests use the standard test environment. The setting test uses a temporary folder that is removed afterwards; no
display, network, audio hardware, or window is required.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The default binding is `F9` with no modifiers, its text is `F9`, and it is safe.
- Function keys F2 to F7, F9, and F13 to F24, alone and with Ctrl, Alt, or Shift, `Ctrl+Shift+Space`, `Ctrl+Shift+T`,
  `Alt+Shift+M`, and `Ctrl+5` are accepted and give a binding and no problem.
- Each of these is refused with a sentence that says what to do, gives no binding, and reports the same sentence from
  `Validate` and `TryCreate`: no key; a modifier alone; the Windows key held with any key; Escape, Enter, Tab,
  Backspace, and the cursor keys, also with Ctrl; a letter, a digit, or Space without a modifier, and a letter with
  Shift only; Ctrl+Space; Alt+Space; Ctrl+Alt with a letter; Ctrl+C and Ctrl+V; Ctrl+Shift+Z; Alt with a letter; Alt+F4;
  and F1, F8, F10, F11, and F12, also with Shift.
- A binding is written as `Ctrl+Alt+Shift+Key` in that order (a digit as the digit) and read back equal, whatever the
  case and the spaces of the text; `Parse` answers nothing for no text, an empty text, an unknown key, a number, a
  modifier in place of the key (`Ctrl+Ctrl`), an unknown modifier, and a binding that is not safe (`Ctrl+Space`, `F8`).
- The default is stored as the empty string; a safe chord is normalized to its normal form (`shift+ctrl+space` becomes
  `Ctrl+Shift+Space`); a stored `F8`, an unsafe key, or no text is read as the default and normalized to the empty
  string; a safe stored key such as `F6` is read as that key.
- A press matches the binding only with its key and exactly its modifiers among Ctrl, Alt, and Shift; the key and the
  keys of its modifiers (left or right) are part of the chord, and another key or the key of a modifier that the chord
  does not have is not.
- The setting shows the default at the start, follows a key that is saved in the settings (text, announcing `Binding`
  and then `Text`), announces nothing for a save of the same key, falls back to the default when the settings hold the
  old `F8`, stops following after disposal, and a fixed setting holds the default or the binding that it was given.

#### Test Scenarios

**PushToTalkBinding_Default_IsF9AndSafe**: The default binding has the key F9, no modifiers, and the text `F9`, and
`Validate` accepts it. This scenario is tested by `PushToTalkBinding_Default_IsF9AndSafe`.

**PushToTalkBinding_Validate_SafeKeys_AreAccepted**: A theory over nine safe cases: F2, F7, F13, `Ctrl+F9`,
`Shift+F24`, `Ctrl+Shift+Space`, `Ctrl+Shift+T`, `Alt+Shift+M`, and `Ctrl+5`. Each is accepted by `Validate`, and
`TryCreate` gives a binding and no problem. This scenario is tested by
`PushToTalkBinding_Validate_SafeKeys_AreAccepted`.

**PushToTalkBinding_Validate_UnsafeKeys_AreRefusedWithASentence**: A theory over twenty-eight unsafe cases, which cover
every rule: no key, a lone Ctrl or Shift or the Windows key, Escape, Enter with Ctrl, Tab, Backspace, Left with Ctrl,
`A` alone and with Shift, `1` alone, Space alone, with Ctrl, and with Alt, `Ctrl+Alt+T`, F9 and `Ctrl+T` with the
Windows key, `Ctrl+C`, `Ctrl+V`, `Ctrl+Shift+Z`, `Alt+F`, `Alt+F4`, F1, F8, F10, F11, and `Shift+F12`. Each gives a
sentence that is not blank, no binding from `TryCreate`, and the same sentence from `Validate` and from `TryCreate`.
This scenario is tested by `PushToTalkBinding_Validate_UnsafeKeys_AreRefusedWithASentence`.

**PushToTalkBinding_DisplayAndParse_RoundTripAndRefuseUnsafeText**: A chord made from Shift and Control is written as
`Ctrl+Shift+Space`, and a digit chord as `Ctrl+5`. The text `ctrl + SHIFT + space` is read as the chord, `Ctrl+5` as the
digit chord, and `f9` as the default. No binding is read from nothing, an empty text, `Banana`, `65`, `Ctrl+Ctrl`,
`Hyper+F9`, `Ctrl+Space`, or `F8`. This scenario is tested by
`PushToTalkBinding_DisplayAndParse_RoundTripAndRefuseUnsafeText`.

**PushToTalkBinding_Stored_DefaultIsEmptyAndOldF8MeansTheDefault**: The default is stored as the empty string;
`shift+ctrl+space` normalizes to `Ctrl+Shift+Space`; `F8`, `Escape`, and nothing normalize to the empty string; stored
`F8` and an empty string are read as the default; stored `F6` is read as `F6`. This scenario is tested by
`PushToTalkBinding_Stored_DefaultIsEmptyAndOldF8MeansTheDefault`.

**PushToTalkBinding_MatchesAndIsPartOf_FollowTheChord**: For `Ctrl+Shift+Space`, a press of Space with Ctrl and Shift
matches, and Space with Ctrl only, Space with Ctrl, Shift, and Alt, and `A` with Ctrl and Shift do not; the default
matches F9 without modifiers and not F9 with Shift. For the release, Space, left Ctrl, and right Shift are part of the
chord, and left Alt and `A` are not; left Ctrl is not part of the default. This scenario is tested by
`PushToTalkBinding_MatchesAndIsPartOf_FollowTheChord`.

**PushToTalkSetting_FollowsTheSettingsAndStopsWhenDisposed**: Over a real settings service, the setting shows `F9` at
the start. When `Ctrl+Shift+Space` is saved it shows that text and announces `Binding` and then `Text`; saving the same
key again announces nothing more; saving the old `F8` makes it show `F9` again. After it is disposed a saved `F6` does
not change it. A fixed setting shows `F9`, and one that was given the binding read from `F6` shows `F6`. This scenario
is tested by `PushToTalkSetting_FollowsTheSettingsAndStopsWhenDisposed`.
