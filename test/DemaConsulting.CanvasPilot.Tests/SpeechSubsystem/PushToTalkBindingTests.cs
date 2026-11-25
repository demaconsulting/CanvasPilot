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

using Avalonia.Input;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="PushToTalkBinding" />, the rules of the talk key, and for <see cref="PushToTalkSetting" />,
///     the one source that every hint and the window read.
/// </summary>
public sealed class PushToTalkBindingTests
{
    /// <summary>
    ///     Test that the default is F9 and that it is a safe binding.
    /// </summary>
    [Fact]
    public void PushToTalkBinding_Default_IsF9AndSafe()
    {
        // Arrange and act
        var binding = PushToTalkBinding.Default;

        // Assert
        Assert.Equal((Key.F9, KeyModifiers.None, "F9"), (binding.Key, binding.Modifiers, binding.Display));
        Assert.Null(PushToTalkBinding.Validate(Key.F9, KeyModifiers.None));
    }

    /// <summary>
    ///     Test that the keys that are safe are accepted: function keys alone and with modifiers, and a letter or a space
    ///     with the modifiers that do not collide with the system.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers.</param>
    [Theory]
    [InlineData(Key.F3, KeyModifiers.None)]
    [InlineData(Key.F7, KeyModifiers.None)]
    [InlineData(Key.F9, KeyModifiers.Control)]
    [InlineData(Key.F13, KeyModifiers.None)]
    [InlineData(Key.F24, KeyModifiers.Shift)]
    [InlineData(Key.Space, KeyModifiers.Control | KeyModifiers.Shift)]
    [InlineData(Key.T, KeyModifiers.Control | KeyModifiers.Shift)]
    [InlineData(Key.M, KeyModifiers.Alt | KeyModifiers.Shift)]
    [InlineData(Key.D5, KeyModifiers.Control)]
    public void PushToTalkBinding_Validate_SafeKeys_AreAccepted(Key key, KeyModifiers modifiers)
    {
        // Act and assert
        Assert.Null(PushToTalkBinding.Validate(key, modifiers));
        Assert.NotNull(PushToTalkBinding.TryCreate(key, modifiers, out var problem));
        Assert.Null(problem);
    }

    /// <summary>
    ///     Test that the keys that are not safe are refused with a sentence: a modifier alone, the keys of typing and
    ///     moving, a key that types without Ctrl or Alt, Ctrl+Space, Alt+Space, Ctrl+Alt, the Windows key, the shortcuts
    ///     of editing, Alt+F4, the function keys that the system or laptops take, F8 among them, and F2, which changes the
    ///     note of a mark in the message.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers.</param>
    [Theory]
    [InlineData(Key.None, KeyModifiers.None)]
    [InlineData(Key.RightCtrl, KeyModifiers.Control)]
    [InlineData(Key.LeftShift, KeyModifiers.Shift)]
    [InlineData(Key.LWin, KeyModifiers.Meta)]
    [InlineData(Key.Escape, KeyModifiers.None)]
    [InlineData(Key.Enter, KeyModifiers.Control)]
    [InlineData(Key.Tab, KeyModifiers.None)]
    [InlineData(Key.Back, KeyModifiers.None)]
    [InlineData(Key.Left, KeyModifiers.Control)]
    [InlineData(Key.A, KeyModifiers.None)]
    [InlineData(Key.A, KeyModifiers.Shift)]
    [InlineData(Key.D1, KeyModifiers.None)]
    [InlineData(Key.Space, KeyModifiers.None)]
    [InlineData(Key.Space, KeyModifiers.Control)]
    [InlineData(Key.Space, KeyModifiers.Alt)]
    [InlineData(Key.T, KeyModifiers.Control | KeyModifiers.Alt)]
    [InlineData(Key.F9, KeyModifiers.Meta)]
    [InlineData(Key.T, KeyModifiers.Control | KeyModifiers.Meta)]
    [InlineData(Key.C, KeyModifiers.Control)]
    [InlineData(Key.V, KeyModifiers.Control)]
    [InlineData(Key.Z, KeyModifiers.Control | KeyModifiers.Shift)]
    [InlineData(Key.F, KeyModifiers.Alt)]
    [InlineData(Key.F4, KeyModifiers.Alt)]
    [InlineData(Key.F1, KeyModifiers.None)]
    [InlineData(Key.F2, KeyModifiers.None)]
    [InlineData(Key.F2, KeyModifiers.Control)]
    [InlineData(Key.F8, KeyModifiers.None)]
    [InlineData(Key.F10, KeyModifiers.None)]
    [InlineData(Key.F11, KeyModifiers.None)]
    [InlineData(Key.F12, KeyModifiers.Shift)]
    public void PushToTalkBinding_Validate_UnsafeKeys_AreRefusedWithASentence(Key key, KeyModifiers modifiers)
    {
        // Act
        var problem = PushToTalkBinding.Validate(key, modifiers);
        var binding = PushToTalkBinding.TryCreate(key, modifiers, out var reported);

        // Assert: a sentence that says what to do, and no binding
        Assert.False(string.IsNullOrWhiteSpace(problem));
        Assert.Null(binding);
        Assert.Equal(problem, reported);
    }

    /// <summary>
    ///     Test that a binding is written as <c>Ctrl+Alt+Shift+Key</c> and read back, that the reading ignores case and
    ///     accepts a digit, and that a text that is not a safe binding is not read.
    /// </summary>
    [Fact]
    public void PushToTalkBinding_DisplayAndParse_RoundTripAndRefuseUnsafeText()
    {
        // Arrange
        var chord = PushToTalkBinding.TryCreate(Key.Space, KeyModifiers.Shift | KeyModifiers.Control, out _)!;
        var digit = PushToTalkBinding.TryCreate(Key.D5, KeyModifiers.Control, out _)!;

        // Act and assert: written in one order, and read back whatever the case
        Assert.Equal("Ctrl+Shift+Space", chord.Display);
        Assert.Equal("Ctrl+5", digit.Display);
        Assert.Equal(chord, PushToTalkBinding.Parse("ctrl + SHIFT + space"));
        Assert.Equal(digit, PushToTalkBinding.Parse("Ctrl+5"));
        Assert.Equal(PushToTalkBinding.Default, PushToTalkBinding.Parse(" f9 "));

        // Assert: nothing that is empty, unknown, unsafe, or has a modifier that does not exist is read
        Assert.Null(PushToTalkBinding.Parse(null));
        Assert.Null(PushToTalkBinding.Parse(""));
        Assert.Null(PushToTalkBinding.Parse("Banana"));
        Assert.Null(PushToTalkBinding.Parse("65"));
        Assert.Null(PushToTalkBinding.Parse("Ctrl+Ctrl"));
        Assert.Null(PushToTalkBinding.Parse("Hyper+F9"));
        Assert.Null(PushToTalkBinding.Parse("Ctrl+Space"));
        Assert.Null(PushToTalkBinding.Parse("F8"));
        Assert.Null(PushToTalkBinding.Parse("Ctrl+"));
        Assert.Null(PushToTalkBinding.Parse("+"));
        Assert.Null(PushToTalkBinding.Parse("Ctrl+Shift+"));
    }

    /// <summary>
    ///     Test that the settings keep nothing for the default (so that it can follow a later release), that an unsafe
    ///     stored value, F8 the old default among them, is read as the default, and that a safe value is kept in its normal
    ///     form.
    /// </summary>
    [Fact]
    public void PushToTalkBinding_Stored_DefaultIsEmptyAndOldF8MeansTheDefault()
    {
        // Act and assert
        Assert.Equal(string.Empty, PushToTalkBinding.ToStored(PushToTalkBinding.Default));
        Assert.Equal("Ctrl+Shift+Space", PushToTalkBinding.Normalize("shift+ctrl+space"));
        Assert.Equal(string.Empty, PushToTalkBinding.Normalize("F8"));
        Assert.Equal(string.Empty, PushToTalkBinding.Normalize("Escape"));
        Assert.Equal(string.Empty, PushToTalkBinding.Normalize(null));
        Assert.Equal(string.Empty, PushToTalkBinding.Normalize("F2"));
        Assert.Equal(PushToTalkBinding.Default, PushToTalkBinding.FromStored("F8"));
        Assert.Equal(PushToTalkBinding.Default, PushToTalkBinding.FromStored("F2"));
        Assert.Equal(PushToTalkBinding.Default, PushToTalkBinding.FromStored(string.Empty));
        Assert.Equal("F6", PushToTalkBinding.FromStored("F6").Display);
    }

    /// <summary>
    ///     Test that a press is the binding only with the key and exactly its modifiers, and that letting go of the key or
    ///     of a modifier of the chord is part of the chord while another key is not.
    /// </summary>
    [Fact]
    public void PushToTalkBinding_MatchesAndIsPartOf_FollowTheChord()
    {
        // Arrange
        var chord = PushToTalkBinding.TryCreate(Key.Space, KeyModifiers.Control | KeyModifiers.Shift, out _)!;

        // Act and assert: the press
        Assert.True(chord.Matches(Key.Space, KeyModifiers.Control | KeyModifiers.Shift));
        Assert.False(chord.Matches(Key.Space, KeyModifiers.Control));
        Assert.False(chord.Matches(Key.Space, KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt));
        Assert.False(chord.Matches(Key.A, KeyModifiers.Control | KeyModifiers.Shift));
        Assert.True(PushToTalkBinding.Default.Matches(Key.F9, KeyModifiers.None));
        Assert.False(PushToTalkBinding.Default.Matches(Key.F9, KeyModifiers.Shift));

        // Act and assert: the release of any part ends the talking
        Assert.True(chord.IsPartOf(Key.Space));
        Assert.True(chord.IsPartOf(Key.LeftCtrl));
        Assert.True(chord.IsPartOf(Key.RightShift));
        Assert.False(chord.IsPartOf(Key.LeftAlt));
        Assert.False(chord.IsPartOf(Key.A));
        Assert.False(PushToTalkBinding.Default.IsPartOf(Key.LeftCtrl));
    }

    /// <summary>
    ///     Test that the setting holds the default without settings, follows the settings when they change (and says so
    ///     once), and stops following when it is disposed.
    /// </summary>
    [Fact]
    public void PushToTalkSetting_FollowsTheSettingsAndStopsWhenDisposed()
    {
        // Arrange: a settings service in a temporary folder
        var folder = Directory.CreateTempSubdirectory("canvaspilot-tests-").FullName;
        try
        {
            var settings = new SettingsService(folder);
            settings.Load();
            using var setting = new PushToTalkSetting(settings);
            var changes = new List<string?>();
            setting.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
            var atStart = setting.Text;

            // Act: another key is saved, the same key again, an unsafe one, and then the setting is disposed
            settings.Update(current => current with { Speech = current.Speech with { PushToTalkKey = "Ctrl+Shift+Space" } });
            var afterChord = setting.Text;
            var changesAfterChord = changes.Count;
            settings.Update(current => current with { Speech = current.Speech with { PushToTalkKey = "Ctrl+Shift+Space" } });
            settings.Update(current => current with { Speech = current.Speech with { PushToTalkKey = "F8" } });
            var afterOldDefault = setting.Text;
            setting.Dispose();
            settings.Update(current => current with { Speech = current.Speech with { PushToTalkKey = "F6" } });

            // Assert
            Assert.Equal("F9", atStart);
            Assert.Equal("Ctrl+Shift+Space", afterChord);
            Assert.Equal(["Binding", "Text"], changes.Take(changesAfterChord));
            Assert.Equal("F9", afterOldDefault);
            Assert.Equal("F9", setting.Text);
            Assert.Equal("F9", PushToTalkSetting.Fixed().Text);
            Assert.Equal("F6", PushToTalkSetting.Fixed(PushToTalkBinding.FromStored("F6")).Text);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
