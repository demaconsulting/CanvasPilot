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
using CommunityToolkit.Mvvm.Input;

namespace DemaConsulting.CanvasPilot.SettingsSubsystem;

/// <summary>
///     Event data for <see cref="SettingsDialogViewModel.CloseRequested" />.
/// </summary>
/// <param name="accepted">
///     <see langword="true" /> when the dialog closed through OK; <see langword="false" /> when it was cancelled.
/// </param>
public sealed class DialogCloseRequestedEventArgs(bool accepted) : EventArgs
{
    /// <summary>
    ///     Gets a value indicating whether the dialog closed through OK.
    /// </summary>
    public bool Accepted { get; } = accepted;
}

/// <summary>
///     View model of the settings dialog: a list of injected pages on the left, the selected page on the right,
///     and OK, Cancel, and Apply commands. The pages edit working copies, so nothing is saved until the user
///     chooses OK or Apply.
/// </summary>
public sealed partial class SettingsDialogViewModel : ObservableObject
{
    /// <summary>
    ///     Source and destination of the persisted settings.
    /// </summary>
    private readonly ISettingsService _settingsService;

    /// <summary>
    ///     Whether the pages have already been told that the dialog closed.
    /// </summary>
    private bool _closed;

    /// <summary>
    ///     Page currently shown on the right.
    /// </summary>
    [ObservableProperty]
    private ISettingsPage? _selectedPage;

    /// <summary>
    ///     Message describing the last failed save, or an empty string.
    /// </summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    ///     Creates the view model and loads every page from the settings in effect.
    /// </summary>
    /// <param name="settingsService">Source and destination of the settings.</param>
    /// <param name="pages">The pages to host, in display order.</param>
    public SettingsDialogViewModel(ISettingsService settingsService, IReadOnlyList<ISettingsPage> pages)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(pages);

        _settingsService = settingsService;
        Pages = pages;

        var settings = settingsService.Current;
        foreach (var page in Pages)
        {
            page.Load(settings);
            page.PropertyChanged += OnPagePropertyChanged;
        }

        _selectedPage = Pages.FirstOrDefault();
    }

    /// <summary>
    ///     Raised when the dialog asks its host to close.
    /// </summary>
    public event EventHandler<DialogCloseRequestedEventArgs>? CloseRequested;

    /// <summary>
    ///     Gets the dialog title.
    /// </summary>
    public string Title { get; } = "Settings";

    /// <summary>
    ///     Gets the hosted pages in display order.
    /// </summary>
    public IReadOnlyList<ISettingsPage> Pages { get; }

    /// <summary>
    ///     Gets a value indicating whether every page's entries can be applied.
    /// </summary>
    public bool AllPagesValid => Pages.All(page => page.IsValid);

    /// <summary>
    ///     Tells every page that the dialog has closed. Only the first call has an effect, so the view can call
    ///     it from its closed handler regardless of how the dialog was closed.
    /// </summary>
    public void NotifyClosed()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        foreach (var page in Pages)
        {
            page.PropertyChanged -= OnPagePropertyChanged;
            page.OnClosed();
        }
    }

    /// <summary>
    ///     Saves the page entries without closing the dialog.
    /// </summary>
    [RelayCommand(CanExecute = nameof(AllPagesValid))]
    private void Apply()
    {
        TryApply();
    }

    /// <summary>
    ///     Saves the page entries and closes the dialog when the save succeeds.
    /// </summary>
    [RelayCommand(CanExecute = nameof(AllPagesValid))]
    private void Ok()
    {
        if (TryApply())
        {
            RequestClose(accepted: true);
        }
    }

    /// <summary>
    ///     Closes the dialog without saving.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        RequestClose(accepted: false);
    }

    /// <summary>
    ///     Merges every page into the latest settings and saves the result.
    /// </summary>
    /// <returns><see langword="true" /> when the settings were saved.</returns>
    private bool TryApply()
    {
        if (!AllPagesValid)
        {
            return false;
        }

        try
        {
            var updated = _settingsService.Current;
            foreach (var page in Pages)
            {
                updated = page.Apply(updated);
            }

            _settingsService.Save(updated);
            ErrorMessage = string.Empty;
            return true;
        }
        catch (IOException ex)
        {
            ErrorMessage = $"The settings could not be saved: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    ///     Notifies the pages and asks the host to close.
    /// </summary>
    /// <param name="accepted">Whether the dialog closed through OK.</param>
    private void RequestClose(bool accepted)
    {
        NotifyClosed();
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs(accepted));
    }

    /// <summary>
    ///     Re-evaluates the commands when a page's validity changes.
    /// </summary>
    /// <param name="sender">The page.</param>
    /// <param name="e">The change details.</param>
    private void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != nameof(ISettingsPage.IsValid))
        {
            return;
        }

        OnPropertyChanged(nameof(AllPagesValid));
        ApplyCommand.NotifyCanExecuteChanged();
        OkCommand.NotifyCanExecuteChanged();
    }
}
