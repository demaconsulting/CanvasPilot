### PushToTalkBinding

#### Purpose

`PushToTalkBinding` is the key, or the chord of modifiers and a key, that the user holds to dictate a message. It is an
immutable value with the rules that decide which keys are safe to take over, the two text forms of a key (the text that
is shown and stored, and the stored form that leaves the default empty), and the two questions that the main window asks
of a key event: is this press the talk key, and does this release belong to the chord. The unit also holds
`PushToTalkSetting`, the one observable source of the talk key that is in effect, which follows the settings, so that
the window that listens for the key and every place that names it (the status bar, the welcome screen, and the chat
panel) read the same key and change together. The unit has one responsibility: to say what the talk key is and whether
a key may be the talk key.

#### Data Model

**PushToTalkBinding** (`SpeechSubsystem/PushToTalkBinding.cs`): a sealed `record` with a private constructor, so that a
binding exists only when it came from `Default`, `TryCreate`, `Parse`, or `FromStored`, which means that every binding
that exists is a safe one. Two bindings are equal when their key and modifiers are equal.

- *Key*: `Avalonia.Input.Key` — the key that is held.
- *Modifiers*: `KeyModifiers` — the modifiers held with the key. Only Control, Alt, and Shift are kept; any other flag
  that is passed in (the Windows key, a lock key) is masked off, and a Windows key chord is refused before a binding
  is made.
- *Default*: the static binding `F9` with no modifiers.
- *Display*: the text of the binding: the modifiers in the fixed order `Ctrl`, `Alt`, `Shift`, then the key, joined by
  `+`, for example `F9` or `Ctrl+Shift+Space`. The name of a key is its name in the `Key` enumeration, except that a
  digit of the main keyboard is written as the digit (`Ctrl+5`, not `Ctrl+D5`). The text is the form that is shown to
  the user and written to the settings file.

**Rule tables** (private, static): `ModifierKeys` (left and right Ctrl, Shift, and Alt, and the two Windows keys);
`EditingKeys`, the keys that are needed to edit and move about: Escape, Enter, Tab, Backspace, Delete, Insert, Home,
End, Page Up, Page Down, the four cursor keys, Caps Lock, Num Lock, Scroll Lock, Pause, Print Screen, Apps (the menu
key), Sleep, Clear, and Select; `ReservedFunctionKeys`, the function keys that are refused with a reason, each a
sentence: F1 (opens help in most programs), F2 (changes the note of the mark beside the caret in the message, and
renames the selected file; the key of the message would never be reached, because the window handles the talk key
first), F8 (the brightness, media, or screen-capture key of many laptops, so the
window may never see it), F10 (opens the menu), F11 (the full-screen key of many programs), and F12 (starts the debugger
on many computers); and `CtrlLetterShortcuts`, the letters that Ctrl alone must not be combined with because they are
the shortcuts of editing and of the application: A, C, V, X, Y, Z, N, O, S, P, F, W, and Q.

**The rules** (`Validate(key, modifiers)` answers a sentence that says why and what to do, or `null` when the binding is
safe). They are applied in this order, and the first that holds decides:

1. No key (`Key.None`): "Press the key that you want to hold to talk."
2. The key is a modifier itself (Ctrl, Shift, Alt, or the Windows key; this includes Right Ctrl): "A modifier key alone
   cannot be the talk key. Press it together with another key."
3. The Windows key is held (the `Meta` modifier), with any key: "Combinations with the Windows key belong to the
   system. Choose another key."
4. The key is one of the editing and moving keys, with any modifiers: "Esc, Enter, Tab, Backspace, the cursor keys, and
   the like are needed for typing. Choose another key."
5. The key is F1, F2, F8, F10, F11, or F12, with any modifiers: the reason of that key followed by "Choose another key,
   for example F9."
6. The key is one of the other function keys, F3 to F7, F9, or F13 to F24: it is safe alone and with any of Ctrl, Alt,
   and Shift, except Alt+F4 (with Alt held, whatever else is), which is refused with "Alt+F4 closes the window. Choose
   another key."
7. Any other key, which is a key that types (a letter, a digit, Space, a sign, or a key of the numeric pad), is judged
   by what is held with it:
   - Neither Ctrl nor Alt: "A key that types a character needs Ctrl or Alt with it, or use a function key such as F9.
     Choose another key." (Shift alone is not enough.)
   - Ctrl and Alt together: "Ctrl+Alt is AltGr on many keyboards. Choose another key."
   - Space with Alt (alone or with Shift): "Alt+Space opens the system menu. Choose another key."
   - Space with Ctrl alone: "Ctrl+Space belongs to the input method and to tools such as PowerToys. Choose another key,
     for example Ctrl+Shift+Space."
   - A letter or a digit with Alt alone: "Alt with a letter or a digit opens a menu. Choose another key."
   - A letter with Ctrl alone when it is one of `CtrlLetterShortcuts`, and Z with Ctrl and Shift (the redo of editing):
     "{Key} with Ctrl is a shortcut of editing or of CanvasPilot. Choose another key."
   - Anything else is safe, for example `Ctrl+Shift+Space`, `Ctrl+Shift+T`, `Alt+Shift+M`, and `Ctrl+5`.

**The default and why**: the default is `F9`. It was `F8`, which is the brightness, media, or screen-capture key of many
laptops, so that the window never saw it; F8 is therefore refused now, and a stored F8 is read as the default (see
*Stored form* below). The refusals above are what the code enforces. The reasons behind them, and behind the choice of a
bare function key as the default, are these: `Ctrl+Space` is registered by other tools (the Peek of PowerToys uses it
as a global shortcut) and by input methods; chords with the Windows key belong to the system (the Game Bar uses
`Win+Alt+R` and `Win+Alt+G`); `Alt+Space` opens the system menu; `Ctrl+Alt` with a letter is AltGr on many keyboard
layouts, so it types characters; and a lone modifier such as Right Ctrl would be taken over by keyboards with a
dedicated key for it. These other tools are not detected: the application cannot know what another program has
registered, and F9 can be swallowed by a computer too. That is why the key is a setting and not a constant.

**Text forms**:

- *Display / parse*: `Parse(text)` reads what `Display` writes. Spaces around the parts are ignored, and so is the case
  (`ctrl + SHIFT + space` is `Ctrl+Shift+Space`); `Ctrl` and `Control` both name Control. All parts but the last must be
  modifiers (`Ctrl`, `Alt`, `Shift`); the last is the key: a single digit, or the name of a key that is all letters and
  digits, does not start with a digit, and is a defined value of the `Key` enumeration other than none. A text that is
  empty, has no key after its modifiers (`Ctrl+`), is not a key (`Banana`, `65`), has an unknown modifier (`Hyper+F9`),
  or names a binding that `Validate` refuses
  (`Ctrl+Space`, `F8`) gives `null`, so that a bad value in a file never takes effect.
- *Stored form*: the settings keep the text of the binding, and the empty string for the default, so that the default
  can follow a later release. `ToStored(binding)` gives the empty string for `Default` and `Display` otherwise.
  `FromStored(text)` gives `Parse(text)` or, when that is `null` (empty, not a key, or not safe, which includes the old
  default `F8` and `F2`, which earlier releases allowed), `Default`. `Normalize(text)` is `ToStored(FromStored(text))`:
  it cleans a value of a settings file, writing a safe binding in its normal form and a bad or empty one as the empty
  string.

**Questions about a key event**:

- *Matches(key, modifiers)*: true when the key is the key of the binding and the modifiers held, among Ctrl, Alt, and
  Shift, are exactly the modifiers of the binding. A modifier that the binding does not have, held with the key, is
  therefore not a match (`Shift+F9` does not match `F9`), and the Windows key and the lock keys are not compared.
- *IsPartOf(key)*: true when the key is the key of the binding or, for a modifier key (left or right), when the binding
  has that modifier. Letting go of any such key ends the talking. A key of a modifier that the binding does not have
  (Alt for `Ctrl+Shift+Space`) is not part of it.
- *IsModifierKey(key)* (static): true for Ctrl, Alt, Shift, and the Windows key. The Speech page uses it to wait out a
  modifier that is pressed before the key it goes with.

**PushToTalkSetting** (`SpeechSubsystem/PushToTalkSetting.cs`): an observable, disposable holder of the binding in
effect. `Binding` is the binding, and `Text` is its `Display`, which every hint shows.

- A setting made over an `ISettingsService` reads the binding of `Current.Speech.PushToTalkKey` through `FromStored`
  when it is made, and subscribes to `Changed`. Whenever the settings change it reads the key again; when the binding is
  not the same it keeps it and announces a change of `Binding` and then of `Text`, and when it is the same it announces
  nothing. A setting must be disposed, which unsubscribes.
- `Fixed(binding)` makes a setting that follows nothing and always holds one binding (the default when none is given);
  it is what a unit that was made without settings, such as a test or a preview, uses, and it needs no disposal.

#### Key Methods

**Validate(key, modifiers)**: applies the rules above in order and returns the sentence of the first that holds or
`null`. Preconditions: none; every key and every combination of flags is judged. Postconditions: no state changes.
Parameter types: `Avalonia.Input.Key` and `KeyModifiers`.

**TryCreate(key, modifiers, out problem)**: calls `Validate`; returns a new binding with the key and the Ctrl, Alt, and
Shift of the modifiers when the answer is `null`, and otherwise `null` with the sentence in `problem`. The Speech page
uses it to accept or refuse a key that was pressed.

**Parse(text)** and **FromStored(stored)**: as described under *Text forms*; `Parse` answers `null` for anything that is
not a safe binding, and `FromStored` answers the default instead.

**ToStored(binding)** and **Normalize(stored)**: as described under *Stored form*. `ToStored` compares the binding with
`Default` by value, so a binding that was made from `F9` is also stored as the empty string.

**Matches(key, modifiers)**, **IsPartOf(key)**, and **IsModifierKey(key)**: as described under *Questions about a key
event*.

**Display** (property): builds the text from the modifiers in the order Ctrl, Alt, Shift and the name of the key, with
the digit rule.

**PushToTalkSetting constructor, OnSettingsChanged, Fixed, and Dispose**: as described under *PushToTalkSetting*.

#### Error Handling

No operation of the binding throws for a bad value. A key that is not safe is a result (a sentence or `null`), not an
exception: `Validate` and `TryCreate` report it, `Parse` answers `null`, and `FromStored` and `Normalize` fall back to
the default, so that a settings file that was edited by hand, or written by an earlier build with the old default, never
gives the window a key that takes away typing. `ToStored` throws `ArgumentNullException` for a missing binding. The
constructor of `PushToTalkSetting` throws `ArgumentNullException` for missing settings. The rules are about the key
alone: the unit does not know what other programs have registered, so a safe key that another program swallows is not
detected, and the user chooses another key on the Speech page.

#### Dependencies

- **Avalonia** — `Key` and `KeyModifiers`, the types of keys; no control and no window of Avalonia is used.
- **SettingsService** (through `ISettingsService`, `AppSettings`, and `SettingsChangedEventArgs`, of the `Settings`
  subsystem) — `PushToTalkSetting` reads `SpeechSettings.PushToTalkKey` and follows `Changed`.
- **CommunityToolkit.Mvvm** — `ObservableObject`, the base of `PushToTalkSetting`.

#### Callers

- **SettingsService** — `Normalize` cleans the stored talk key whenever the settings are loaded, saved, or updated.
- **SpeechSettingsPage** — `TryCreate` accepts or refuses a key that was pressed in its box, `IsModifierKey` waits out a
  modifier, `FromStored` and `ToStored` load and store the choice, and `Default` and `Display` give the reset button
  and the box their text.
- **MainWindowShell** — makes the `PushToTalkSetting` over the settings service (its `TalkKey`), gives it to the status
  bar and to the chat panel, and the welcome screen reads it from the shell.
- **MainWindowView** — matches each key press against `Binding.Matches` to start dictation, and each key release against
  `Binding.IsPartOf` to end it.
- **StatusBar**, **Welcome**, and **ChatPanel** — show `Text` and follow its change.
