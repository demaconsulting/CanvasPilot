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

using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     Makes the application show the theme variant that the settings choose: dark, light, or the one of the operating
///     system. It applies the choice when it is created and again every time the settings change, so a choice made in
///     the settings dialog takes effect in every open window at once.
/// </summary>
/// <remarks>
///     <para>
///         The choice to follow the system is the Avalonia variant <see cref="ThemeVariant.Default" />, which makes the
///         application dark or light as the system is and follows the system while it runs; a pinned choice is the dark
///         or light variant.
///     </para>
///     <para>
///         The settings service raises its change event on the thread that changed the settings, which need not be the user
///         interface thread, while the requested theme variant of the application may be set on that thread only. A change
///         from another thread is therefore posted to the user interface thread. The class is created and disposed on the
///         user interface thread; it is not otherwise thread-safe.
///     </para>
/// </remarks>
public sealed class ThemeVariantApplier : IDisposable
{
    /// <summary>
    ///     The application whose theme variant is set.
    /// </summary>
    private readonly Application _application;

    /// <summary>
    ///     The settings that say which variant is wanted.
    /// </summary>
    private readonly ISettingsService _settings;

    /// <summary>
    ///     Whether the applier has been disposed, after which a late change is ignored.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ThemeVariantApplier" /> class, applies the choice that is in
    ///     effect, and starts following the settings.
    /// </summary>
    /// <param name="application">The application whose theme variant is set.</param>
    /// <param name="settings">The settings service; its current appearance is applied at once.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    public ThemeVariantApplier(Application application, ISettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(settings);

        _application = application;
        _settings = settings;

        // Apply what is in effect now, then follow every later change of the settings
        Apply(settings.Current.Appearance.Theme);
        _settings.Changed += OnSettingsChanged;
    }

    /// <summary>
    ///     Gets the theme variant that a theme choice means.
    /// </summary>
    /// <param name="choice">The choice.</param>
    /// <returns>
    ///     <see cref="ThemeVariant.Dark" /> or <see cref="ThemeVariant.Light" /> for a pinned choice, and
    ///     <see cref="ThemeVariant.Default" />, which follows the operating system, for every other choice.
    /// </returns>
    public static ThemeVariant ToVariant(ThemeChoice choice)
    {
        return choice switch
        {
            ThemeChoice.Dark => ThemeVariant.Dark,
            ThemeChoice.Light => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settings.Changed -= OnSettingsChanged;
    }

    /// <summary>
    ///     Applies the choice of the settings that were just changed.
    /// </summary>
    /// <param name="sender">The settings service.</param>
    /// <param name="e">The settings now in effect.</param>
    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        Apply(e.Settings.Appearance.Theme);
    }

    /// <summary>
    ///     Sets the requested theme variant of the application on the user interface thread.
    /// </summary>
    /// <param name="choice">The choice to show.</param>
    private void Apply(ThemeChoice choice)
    {
        var variant = ToVariant(choice);

        // The variant may be set on the user interface thread only, so a change from another thread is posted to it
        if (Dispatcher.UIThread.CheckAccess())
        {
            Set(variant);
        }
        else
        {
            Dispatcher.UIThread.Post(() => Set(variant));
        }
    }

    /// <summary>
    ///     Sets the variant unless the applier is gone or the variant is already requested, so a repeated notice does not
    ///     make the windows restyle for nothing.
    /// </summary>
    /// <param name="variant">The variant to request.</param>
    private void Set(ThemeVariant variant)
    {
        if (!_disposed && _application.RequestedThemeVariant != variant)
        {
            _application.RequestedThemeVariant = variant;
        }
    }
}
