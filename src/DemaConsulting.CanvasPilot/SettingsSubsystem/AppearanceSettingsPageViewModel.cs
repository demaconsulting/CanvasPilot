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

namespace DemaConsulting.CanvasPilot.SettingsSubsystem;

/// <summary>
///     The "Appearance" page of the settings dialog: the theme variant of the application (follow the system, dark, or
///     light), whether the project explorer shows a small picture beside each file, and whether its rows are taller.
/// </summary>
/// <remarks>
///     The page edits a working copy and merges only the <see cref="AppSettings.Appearance" /> member into the settings when
///     the user applies the dialog, so every value that another page owns is left as it is. Every entry is valid, so the
///     page never blocks OK. The theme takes effect when the settings change, which the application watches; the page
///     itself knows nothing about windows or themes.
/// </remarks>
public sealed partial class AppearanceSettingsPageViewModel : ObservableObject, ISettingsPage
{
    /// <summary>
    ///     The theme that is chosen.
    /// </summary>
    [ObservableProperty]
    private SettingsChoice<ThemeChoice> _selectedTheme;

    /// <summary>
    ///     Whether the explorer shows thumbnails.
    /// </summary>
    [ObservableProperty]
    private bool _showThumbnails = true;

    /// <summary>
    ///     Whether the rows of the explorer are taller.
    /// </summary>
    [ObservableProperty]
    private bool _comfortableRows;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AppearanceSettingsPageViewModel" /> class with the default look.
    /// </summary>
    public AppearanceSettingsPageViewModel()
    {
        Themes =
        [
            new SettingsChoice<ThemeChoice>(ThemeChoice.FollowSystem, "Follow system"),
            new SettingsChoice<ThemeChoice>(ThemeChoice.Dark, "Dark"),
            new SettingsChoice<ThemeChoice>(ThemeChoice.Light, "Light")
        ];
        _selectedTheme = Themes[0];
    }

    /// <inheritdoc />
    public string Title => "Appearance";

    /// <inheritdoc />
    public string Description => "The theme of the window and how the project explorer looks.";

    /// <inheritdoc />
    public string IconKey => "CpIconImage";

    /// <summary>
    ///     Gets the choices of the theme, in the order in which they are listed.
    /// </summary>
    public IReadOnlyList<SettingsChoice<ThemeChoice>> Themes { get; }

    /// <inheritdoc />
    public bool IsValid => true;

    /// <inheritdoc />
    public void Load(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var appearance = settings.Appearance;
        SelectedTheme = Themes.FirstOrDefault(choice => choice.Value == appearance.Theme) ?? Themes[0];
        ShowThumbnails = appearance.ShowThumbnails;
        ComfortableRows = appearance.ComfortableRows;
    }

    /// <inheritdoc />
    public AppSettings Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings with
        {
            Appearance = settings.Appearance with
            {
                Theme = SelectedTheme.Value,
                ShowThumbnails = ShowThumbnails,
                ComfortableRows = ComfortableRows
            }
        };
    }

    /// <inheritdoc />
    public void OnClosed()
    {
        // The page holds no background work, so there is nothing to cancel.
    }
}
