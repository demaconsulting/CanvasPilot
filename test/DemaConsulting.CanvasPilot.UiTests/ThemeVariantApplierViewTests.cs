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
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests of <see cref="ThemeVariantApplier" />: it shows the theme that the settings choose at startup and
///     follows every change, also one that is made on another thread, and stops when it is disposed. Every test puts the
///     theme of the shared test application back to the default at its end.
/// </summary>
public sealed class ThemeVariantApplierViewTests : IDisposable
{
    /// <summary>
    ///     Temporary settings folder created fresh for each test and removed on disposal.
    /// </summary>
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("canvaspilot-ui-tests-").FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        // The application is shared by all tests of the assembly, so the variant that a test chose is taken back
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = ThemeVariant.Default;
        }

        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that each theme choice means the variant that it names, and that following the system is the default
    ///     variant of Avalonia.
    /// </summary>
    [AvaloniaFact]
    public void ThemeVariantApplier_ToVariant_EachChoice_IsTheVariantItNames()
    {
        // Arrange: the choices of the theme setting
        var choices = new[] { ThemeChoice.Dark, ThemeChoice.Light, ThemeChoice.FollowSystem };

        // Act: translate each choice
        var variants = choices.Select(ThemeVariantApplier.ToVariant).ToArray();

        // Assert: the three choices
        Assert.Equal([ThemeVariant.Dark, ThemeVariant.Light, ThemeVariant.Default], variants);
    }

    /// <summary>
    ///     Test that the choice that is in effect when the applier is created is shown at once.
    /// </summary>
    [AvaloniaFact]
    public void ThemeVariantApplier_New_StoredChoice_IsAppliedAtOnce()
    {
        // Arrange: settings that pin the dark theme, loaded
        var service = new SettingsService(_tempRoot);
        service.Save(new AppSettings { Appearance = new AppearanceSettings { Theme = ThemeChoice.Dark } });
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;

        // Act: create the applier
        using var applier = new ThemeVariantApplier(Application.Current, service);

        // Assert: the application is dark
        Assert.Equal(ThemeVariant.Dark, Application.Current.RequestedThemeVariant);
    }

    /// <summary>
    ///     Test that a change of the choice is followed, from a pinned theme to the other and back to following the
    ///     system.
    /// </summary>
    [AvaloniaFact]
    public void ThemeVariantApplier_SettingsChanged_FollowsTheChoice()
    {
        // Arrange: an applier over default settings (follow the system)
        var service = new SettingsService(_tempRoot);
        using var applier = new ThemeVariantApplier(Application.Current!, service);
        Assert.Equal(ThemeVariant.Default, Application.Current!.RequestedThemeVariant);

        // Act / Assert: dark, light, and following the system again
        service.Update(s => s with { Appearance = s.Appearance with { Theme = ThemeChoice.Dark } });
        Assert.Equal(ThemeVariant.Dark, Application.Current.RequestedThemeVariant);
        service.Update(s => s with { Appearance = s.Appearance with { Theme = ThemeChoice.Light } });
        Assert.Equal(ThemeVariant.Light, Application.Current.RequestedThemeVariant);
        service.Update(s => s with { Appearance = s.Appearance with { Theme = ThemeChoice.FollowSystem } });
        Assert.Equal(ThemeVariant.Default, Application.Current.RequestedThemeVariant);
    }

    /// <summary>
    ///     Test that a change made on another thread is applied on the user interface thread, once it runs its work.
    /// </summary>
    [AvaloniaFact]
    public async Task ThemeVariantApplier_SettingsChangedOnAnotherThread_IsAppliedOnTheUserInterfaceThread()
    {
        // Arrange: an applier over default settings
        var service = new SettingsService(_tempRoot);
        using var applier = new ThemeVariantApplier(Application.Current!, service);

        // Act: change the choice on a thread of the pool, then let the user interface thread run its work
        await Task.Run(() => service.Update(s => s with { Appearance = s.Appearance with { Theme = ThemeChoice.Dark } }));
        Dispatcher.UIThread.RunJobs();

        // Assert: the application is dark
        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
    }

    /// <summary>
    ///     Test that a disposed applier no longer follows the settings.
    /// </summary>
    [AvaloniaFact]
    public void ThemeVariantApplier_Dispose_StopsFollowingTheSettings()
    {
        // Arrange: an applier that has followed a change to dark
        var service = new SettingsService(_tempRoot);
        var applier = new ThemeVariantApplier(Application.Current!, service);
        service.Update(s => s with { Appearance = s.Appearance with { Theme = ThemeChoice.Dark } });

        // Act: dispose it twice, and change the choice
        applier.Dispose();
        applier.Dispose();
        service.Update(s => s with { Appearance = s.Appearance with { Theme = ThemeChoice.Light } });

        // Assert: the application is still dark
        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
    }

    /// <summary>
    ///     Test that missing arguments are refused.
    /// </summary>
    [AvaloniaFact]
    public void ThemeVariantApplier_New_NullArguments_Throw()
    {
        // Arrange
        var service = new SettingsService(_tempRoot);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new ThemeVariantApplier(null!, service));
        Assert.Throws<ArgumentNullException>(() => new ThemeVariantApplier(Application.Current!, null!));
    }
}
