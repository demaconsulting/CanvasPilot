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

using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Hand-written <see cref="ISettingsPage" /> used by the settings dialog tests. It records how the dialog drives
///     it and lets a test change its validity.
/// </summary>
/// <param name="title">The page title.</param>
internal sealed class FakeSettingsPage(string title) : ObservableObject, ISettingsPage
{
    /// <summary>
    ///     Whether the page currently reports valid entries.
    /// </summary>
    private bool _isValid = true;

    /// <inheritdoc />
    public string Title { get; } = title;

    /// <inheritdoc />
    public bool IsValid
    {
        get => _isValid;
        set => SetProperty(ref _isValid, value);
    }

    /// <summary>
    ///     Gets or sets the recent-project entry that <see cref="Apply" /> appends, standing in for the values a real
    ///     page owns.
    /// </summary>
    public string? EntryToApply { get; set; }

    /// <summary>
    ///     Gets the settings most recently passed to <see cref="Load" />.
    /// </summary>
    public AppSettings? LoadedSettings { get; private set; }

    /// <summary>
    ///     Gets how many times <see cref="OnClosed" /> was called.
    /// </summary>
    public int ClosedCount { get; private set; }

    /// <summary>
    ///     Raises <see cref="INotifyPropertyChanged.PropertyChanged" /> for the validity property.
    /// </summary>
    /// <param name="isValid">The new validity.</param>
    public void SetValidity(bool isValid)
    {
        IsValid = isValid;
    }

    /// <inheritdoc />
    public void Load(AppSettings settings)
    {
        LoadedSettings = settings;
    }

    /// <inheritdoc />
    public AppSettings Apply(AppSettings settings)
    {
        return EntryToApply is null
            ? settings
            : settings with { RecentProjects = [.. settings.RecentProjects, EntryToApply] };
    }

    /// <inheritdoc />
    public void OnClosed()
    {
        ClosedCount++;
    }
}
