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

using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.SpeechSubsystem;

/// <summary>
///     The talk key that is in effect now, as one observable source for everything that uses or shows it: the window that
///     listens for it, the hint of the composer, the status bar, the welcome screen, and the tips. It follows the settings,
///     so a key that the user chose on the Speech page is in effect, and shown everywhere, as soon as OK is chosen.
/// </summary>
/// <remarks>
///     A setting made with <see cref="Fixed" /> follows nothing and always holds one binding, which is what a unit that was
///     made without settings (a test, a preview) uses. A setting that follows the settings subscribes to them and must be
///     disposed.
/// </remarks>
public sealed class PushToTalkSetting : ObservableObject, IDisposable
{
    /// <summary>
    ///     The settings that are followed, or <see langword="null" /> for a fixed binding.
    /// </summary>
    private readonly ISettingsService? _settings;

    /// <summary>
    ///     The binding in effect.
    /// </summary>
    private PushToTalkBinding _binding;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PushToTalkSetting" /> class that follows the settings.
    /// </summary>
    /// <param name="settings">The settings service.</param>
    /// <exception cref="ArgumentNullException">Thrown when the settings are <see langword="null" />.</exception>
    public PushToTalkSetting(ISettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        _binding = PushToTalkBinding.FromStored(settings.Current.Speech.PushToTalkKey);
        settings.Changed += OnSettingsChanged;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PushToTalkSetting" /> class that holds one binding.
    /// </summary>
    /// <param name="binding">The binding.</param>
    private PushToTalkSetting(PushToTalkBinding binding)
    {
        _binding = binding;
    }

    /// <summary>
    ///     Gets the binding in effect.
    /// </summary>
    public PushToTalkBinding Binding => _binding;

    /// <summary>
    ///     Gets the text of the binding in effect, for example <c>F9</c>, which every hint shows.
    /// </summary>
    public string Text => _binding.Display;

    /// <summary>
    ///     Makes a setting that holds one binding and follows nothing.
    /// </summary>
    /// <param name="binding">The binding, or <see langword="null" /> for the default.</param>
    /// <returns>The setting.</returns>
    public static PushToTalkSetting Fixed(PushToTalkBinding? binding = null) => new(binding ?? PushToTalkBinding.Default);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_settings is not null)
        {
            _settings.Changed -= OnSettingsChanged;
        }
    }

    /// <summary>
    ///     Takes the binding of the settings that came into effect.
    /// </summary>
    /// <param name="sender">The settings service.</param>
    /// <param name="e">The settings now in effect.</param>
    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        var binding = PushToTalkBinding.FromStored(e.Settings.Speech.PushToTalkKey);
        if (binding == _binding)
        {
            return;
        }

        _binding = binding;
        OnPropertyChanged(nameof(Binding));
        OnPropertyChanged(nameof(Text));
    }
}
