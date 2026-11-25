// Copyright (c) DEMA Consulting
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Globalization;
using Avalonia.Input;

namespace DemaConsulting.CanvasPilot.SpeechSubsystem;

/// <summary>
///     The key, or the chord of modifiers and a key, that the user holds to dictate a message: an immutable value with
///     the rules that tell which keys are safe to use, how it is written for the settings file and for the user, and
///     whether a key press is the chord.
/// </summary>
/// <remarks>
///     <para>
///         <b>Default.</b> The default is <c>F9</c>. It was <c>F8</c>, which many laptops use for brightness, media, or
///         screen-capture functions, so the window never saw it.
///     </para>
///     <para>
///         <b>Rules.</b> A binding is refused when it would take away a key that people need or that the system owns: a
///         modifier alone, the keys of editing and navigation (Esc, Enter, Tab, Backspace, Delete, the cursor keys, and
///         the like), a letter, a digit, or a space without Ctrl or Alt (it would type), Ctrl+Space (the input method and
///         PowerToys), Alt+Space (the system menu), Alt with a letter or a digit alone (the menus), Ctrl+Alt (AltGr on many
///         layouts), every chord with the Windows key, the shortcuts of editing and of CanvasPilot (Ctrl+A, C, V, X, Y, Z,
///         N, O, S, P, F, W, Q), Alt+F4, and the function keys F1 (help), F2 (changing the note of a mark in the
///         message, and renaming a file), F8 (the old default, which laptops swallow), F10 (menu), F11 (full screen), and
///         F12 (the debugger). The function keys F3 to F7, F9, and F13 to F24 are welcome alone or with Ctrl, Alt, or
///         Shift.
///     </para>
///     <para>
///         <b>Text.</b> The text of a binding is the modifiers in the order <c>Ctrl+Alt+Shift</c> and then the key, for
///         example <c>F9</c> or <c>Ctrl+Shift+Space</c>. <see cref="Parse" /> reads it back, ignoring case, and refuses a
///         text that is not a safe binding, so a bad value in a file never takes effect: a stored key that is refused,
///         such as <c>F8</c> or <c>F2</c> that earlier releases allowed, is read as the default.
///     </para>
/// </remarks>
public sealed record PushToTalkBinding
{
    /// <summary>
    ///     The modifiers that a binding may have.
    /// </summary>
    private const KeyModifiers AllowedModifiers = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift;

    /// <summary>
    ///     The keys that are modifiers themselves.
    /// </summary>
    private static readonly HashSet<Key> ModifierKeys =
        [Key.LeftCtrl, Key.RightCtrl, Key.LeftShift, Key.RightShift, Key.LeftAlt, Key.RightAlt, Key.LWin, Key.RWin];

    /// <summary>
    ///     The keys that are needed to edit text and to move about, and that a talk key must not take away.
    /// </summary>
    private static readonly HashSet<Key> EditingKeys =
    [
        Key.Escape, Key.Enter, Key.Tab, Key.Back, Key.Delete, Key.Insert, Key.Home, Key.End, Key.PageUp, Key.PageDown,
        Key.Up, Key.Down, Key.Left, Key.Right, Key.CapsLock, Key.NumLock, Key.Scroll, Key.Pause, Key.PrintScreen, Key.Apps,
        Key.Sleep, Key.Clear, Key.Select,
    ];

    /// <summary>
    ///     The function keys that the system, laptops, or the debugger take, with the reason.
    /// </summary>
    private static readonly Dictionary<Key, string> ReservedFunctionKeys = new()
    {
        [Key.F1] = "F1 opens help in most programs.",
        [Key.F2] = "F2 changes the note of the mark beside the caret in the message, and renames the selected file.",
        [Key.F8] = "F8 is the brightness, media, or screen-capture key of many laptops, so the window may never see it.",
        [Key.F10] = "F10 opens the menu.",
        [Key.F11] = "F11 is the full-screen key of many programs.",
        [Key.F12] = "F12 starts the debugger on many computers.",
    };

    /// <summary>
    ///     The letters that Ctrl alone must not be combined with, because they are the shortcuts of editing and of the
    ///     application.
    /// </summary>
    private static readonly HashSet<Key> CtrlLetterShortcuts =
        [Key.A, Key.C, Key.V, Key.X, Key.Y, Key.Z, Key.N, Key.O, Key.S, Key.P, Key.F, Key.W, Key.Q];

    /// <summary>
    ///     Initializes a new instance of the <see cref="PushToTalkBinding" /> record without checking the rules, which
    ///     <see cref="Validate" /> applies.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers; only Ctrl, Alt, and Shift are kept.</param>
    private PushToTalkBinding(Key key, KeyModifiers modifiers)
    {
        Key = key;
        Modifiers = modifiers & AllowedModifiers;
    }

    /// <summary>
    ///     Gets the binding that is used until the user chooses another: <c>F9</c>.
    /// </summary>
    public static PushToTalkBinding Default { get; } = new(Key.F9, KeyModifiers.None);

    /// <summary>
    ///     Gets the key that is held.
    /// </summary>
    public Key Key { get; }

    /// <summary>
    ///     Gets the modifiers that are held with the key: any of Ctrl, Alt, and Shift.
    /// </summary>
    public KeyModifiers Modifiers { get; }

    /// <summary>
    ///     Gets the text of the binding, for the settings file and for the user, for example <c>F9</c> or
    ///     <c>Ctrl+Shift+Space</c>.
    /// </summary>
    public string Display
    {
        get
        {
            var parts = new List<string>(4);
            if (Modifiers.HasFlag(KeyModifiers.Control))
            {
                parts.Add("Ctrl");
            }

            if (Modifiers.HasFlag(KeyModifiers.Alt))
            {
                parts.Add("Alt");
            }

            if (Modifiers.HasFlag(KeyModifiers.Shift))
            {
                parts.Add("Shift");
            }

            parts.Add(NameOf(Key));
            return string.Join('+', parts);
        }
    }

    /// <summary>
    ///     Tells whether a key and modifiers are safe to use as the talk key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers that are held with it.</param>
    /// <returns>A sentence that says why not and what to do, or <see langword="null" /> when the binding is safe.</returns>
    public static string? Validate(Key key, KeyModifiers modifiers)
    {
        if (key == Key.None)
        {
            return "Press the key that you want to hold to talk.";
        }

        if (ModifierKeys.Contains(key))
        {
            return "A modifier key alone cannot be the talk key. Press it together with another key.";
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            return "Combinations with the Windows key belong to the system. Choose another key.";
        }

        if (EditingKeys.Contains(key))
        {
            return "Esc, Enter, Tab, Backspace, the cursor keys, and the like are needed for typing. Choose another key.";
        }

        if (ReservedFunctionKeys.TryGetValue(key, out var reason))
        {
            return reason + " Choose another key, for example F9.";
        }

        var held = modifiers & AllowedModifiers;
        if (IsFunctionKey(key))
        {
            return key == Key.F4 && held.HasFlag(KeyModifiers.Alt)
                ? "Alt+F4 closes the window. Choose another key."
                : null;
        }

        return ValidateTypingKey(key, held);
    }

    /// <summary>
    ///     Makes a binding for a key and modifiers when they are safe to use.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers that are held with it; Caps Lock and the like are ignored.</param>
    /// <param name="problem">When the binding is refused, the sentence that says why; otherwise <see langword="null" />.</param>
    /// <returns>The binding, or <see langword="null" /> when it is refused.</returns>
    public static PushToTalkBinding? TryCreate(Key key, KeyModifiers modifiers, out string? problem)
    {
        problem = Validate(key, modifiers);
        return problem is null ? new PushToTalkBinding(key, modifiers) : null;
    }

    /// <summary>
    ///     Reads the text of a binding, as <see cref="Display" /> writes it.
    /// </summary>
    /// <param name="text">The text; may be <see langword="null" />.</param>
    /// <returns>The binding, or <see langword="null" /> when the text is empty, not a binding, or not safe.</returns>
    public static PushToTalkBinding? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        var modifiers = KeyModifiers.None;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToUpperInvariant())
            {
                case "CTRL" or "CONTROL":
                    modifiers |= KeyModifiers.Control;
                    break;
                case "ALT":
                    modifiers |= KeyModifiers.Alt;
                    break;
                case "SHIFT":
                    modifiers |= KeyModifiers.Shift;
                    break;
                default:
                    return null;
            }
        }

        return TryParseKey(parts[^1], out var key) ? TryCreate(key, modifiers, out _) : null;
    }

    /// <summary>
    ///     Reads the binding that the settings hold.
    /// </summary>
    /// <param name="stored">The text in the settings; empty for none.</param>
    /// <returns>The binding of the text, or <see cref="Default" /> when there is none or it is not a safe binding.</returns>
    public static PushToTalkBinding FromStored(string? stored) => Parse(stored) ?? Default;

    /// <summary>
    ///     Gives the text that the settings keep for a binding: empty for <see cref="Default" />, so that the default can
    ///     follow a later release, and otherwise <see cref="Display" />.
    /// </summary>
    /// <param name="binding">The binding.</param>
    /// <returns>The text to store.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the binding is <see langword="null" />.</exception>
    public static string ToStored(PushToTalkBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        return binding == Default ? string.Empty : binding.Display;
    }

    /// <summary>
    ///     Cleans the text that a settings file holds: a binding that is not safe (including the old default <c>F8</c>)
    ///     becomes empty, which means the default, and a safe one is written in its normal form.
    /// </summary>
    /// <param name="stored">The text in the file; may be <see langword="null" />.</param>
    /// <returns>The text to keep.</returns>
    public static string Normalize(string? stored) => ToStored(FromStored(stored));

    /// <summary>
    ///     Tells whether a key press is this binding: the key with exactly these modifiers.
    /// </summary>
    /// <param name="key">The key that went down.</param>
    /// <param name="modifiers">The modifiers that are held.</param>
    /// <returns><see langword="true" /> when the press is the binding.</returns>
    public bool Matches(Key key, KeyModifiers modifiers) => key == Key && (modifiers & AllowedModifiers) == Modifiers;

    /// <summary>
    ///     Tells whether a key belongs to the chord: its key or the key of one of its modifiers. Letting go of any of them
    ///     ends the talking.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true" /> when the key is part of the chord.</returns>
    public bool IsPartOf(Key key)
    {
        if (key == Key)
        {
            return true;
        }

        return key switch
        {
            Key.LeftCtrl or Key.RightCtrl => Modifiers.HasFlag(KeyModifiers.Control),
            Key.LeftAlt or Key.RightAlt => Modifiers.HasFlag(KeyModifiers.Alt),
            Key.LeftShift or Key.RightShift => Modifiers.HasFlag(KeyModifiers.Shift),
            _ => false,
        };
    }

    /// <summary>
    ///     Tells whether a key is only a modifier, which does not finish a chord by itself.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true" /> when the key is Ctrl, Alt, Shift, or the Windows key.</returns>
    public static bool IsModifierKey(Key key) => ModifierKeys.Contains(key);

    /// <summary>
    ///     Applies the rules for a key that types a character: a letter, a digit, a space, or a sign.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="held">The modifiers that are held, among Ctrl, Alt, and Shift.</param>
    /// <returns>The sentence that says why the binding is refused, or <see langword="null" /> when it is safe.</returns>
    private static string? ValidateTypingKey(Key key, KeyModifiers held)
    {
        var ctrl = held.HasFlag(KeyModifiers.Control);
        var alt = held.HasFlag(KeyModifiers.Alt);
        if (!ctrl && !alt)
        {
            return "A key that types a character needs Ctrl or Alt with it, or use a function key such as F9. Choose another key.";
        }

        if (ctrl && alt)
        {
            return "Ctrl+Alt is AltGr on many keyboards. Choose another key.";
        }

        if (key == Key.Space)
        {
            if (alt)
            {
                return "Alt+Space opens the system menu. Choose another key.";
            }

            if (held == KeyModifiers.Control)
            {
                return "Ctrl+Space belongs to the input method and to tools such as PowerToys. Choose another key, for example Ctrl+Shift+Space.";
            }
        }

        if (held == KeyModifiers.Alt && (IsLetter(key) || IsDigit(key)))
        {
            return "Alt with a letter or a digit opens a menu. Choose another key.";
        }

        if (ctrl && IsLetter(key) && (held == KeyModifiers.Control ? CtrlLetterShortcuts.Contains(key) : key == Key.Z))
        {
            return $"{NameOf(key)} with Ctrl is a shortcut of editing or of CanvasPilot. Choose another key.";
        }

        return null;
    }

    /// <summary>
    ///     Tells whether a key is one of the function keys F1 to F24.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true" /> for a function key.</returns>
    private static bool IsFunctionKey(Key key) => key is >= Key.F1 and <= Key.F24;

    /// <summary>
    ///     Tells whether a key is a letter.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true" /> for A to Z.</returns>
    private static bool IsLetter(Key key) => key is >= Key.A and <= Key.Z;

    /// <summary>
    ///     Tells whether a key is a digit of the main keyboard.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true" /> for 0 to 9.</returns>
    private static bool IsDigit(Key key) => key is >= Key.D0 and <= Key.D9;

    /// <summary>
    ///     Gives the name of a key as it is shown and stored: a digit is its digit, and any other key its name.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The name.</returns>
    private static string NameOf(Key key) =>
        IsDigit(key) ? (key - Key.D0).ToString(CultureInfo.InvariantCulture) : key.ToString();

    /// <summary>
    ///     Reads the name of a key, as <see cref="NameOf" /> writes it.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true" /> when the name is a key.</returns>
    private static bool TryParseKey(string name, out Key key)
    {
        if (name.Length == 1 && char.IsAsciiDigit(name[0]))
        {
            key = Key.D0 + (name[0] - '0');
            return true;
        }

        // A number is not a key name of the enumeration
        if (name.Length == 0 || !name.All(char.IsLetterOrDigit) || char.IsDigit(name[0]))
        {
            key = Key.None;
            return false;
        }

        return Enum.TryParse(name, ignoreCase: true, out key) && Enum.IsDefined(key) && key != Key.None;
    }
}
