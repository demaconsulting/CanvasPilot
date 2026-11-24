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

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.SpeechSubsystem;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     View model of the welcome screen that fills the window while no project is open: the two ways to start (new project
///     and open project), the cards of the recent projects, and, when the AI provider cannot be used yet, a notice with a
///     button that opens the settings. It follows the recent projects list and the provider pill of the
///     <see cref="MainWindowShell" /> and owns nothing of them.
/// </summary>
public sealed class WelcomeViewModel : ObservableObject, IDisposable
{
    /// <summary>
    ///     The most recent projects that the screen shows as cards.
    /// </summary>
    public const int MaximumCards = 6;

    /// <summary>
    ///     The width in pixels of the picture that a card shows, twice the 284 units of its width.
    /// </summary>
    public const int CardPictureWidth = 568;

    /// <summary>
    ///     The height in pixels of the picture that a card shows, twice the 160 units of its height.
    /// </summary>
    public const int CardPictureHeight = 320;

    /// <summary>
    ///     The shell whose recent projects, provider pill, and commands the screen shows.
    /// </summary>
    private readonly MainWindowShell _shell;

    /// <summary>
    ///     The cards that are shown.
    /// </summary>
    private readonly ObservableCollection<RecentProjectItem> _cards = [];

    /// <summary>
    ///     The provider pill that is followed now, or null.
    /// </summary>
    private ProviderPillViewModel? _pill;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WelcomeViewModel" /> class.
    /// </summary>
    /// <param name="shell">The shell whose recent projects, provider pill, and commands the screen shows.</param>
    public WelcomeViewModel(MainWindowShell shell)
    {
        ArgumentNullException.ThrowIfNull(shell);

        _shell = shell;
        Cards = new ReadOnlyObservableCollection<RecentProjectItem>(_cards);
        ((INotifyCollectionChanged)_shell.RecentProjects).CollectionChanged += OnRecentChanged;
        _shell.PropertyChanged += OnShellPropertyChanged;
        _shell.TalkKey.PropertyChanged += OnTalkKeyChanged;
        FollowPill();
        RebuildCards();
    }

    /// <summary>
    ///     Gets the sentence of the first step of how it works, which names the talk key that is in effect, for example
    ///     <c>Type a request, or hold F9 and speak it. The copilot draws the picture.</c>
    /// </summary>
    public string DescribeStepText => $"Type a request, or hold {_shell.TalkKey.Text} and speak it. The copilot draws the picture.";

    /// <summary>
    ///     Gets the cards of the most recent projects, the most recent first, at most <see cref="MaximumCards" />.
    /// </summary>
    public ReadOnlyObservableCollection<RecentProjectItem> Cards { get; }

    /// <summary>
    ///     Gets a value indicating whether there is any recent project.
    /// </summary>
    public bool HasRecent => _cards.Count > 0;

    /// <summary>
    ///     Gets a value indicating whether there is no recent project, which the screen answers with a card that starts one.
    /// </summary>
    public bool HasNoRecent => _cards.Count == 0;

    /// <summary>
    ///     Gets the words that say how many of the recent projects are shown, such as <c>6 of 10</c>, or nothing when all are.
    /// </summary>
    public string CountText => _shell.RecentProjects.Count > _cards.Count
        ? string.Create(CultureInfo.CurrentCulture, $"{_cards.Count} of {_shell.RecentProjects.Count}")
        : string.Empty;

    /// <summary>
    ///     Gets the command that asks for the folder and the name of a new project.
    /// </summary>
    public ICommand NewProjectCommand => _shell.NewProjectCommand;

    /// <summary>
    ///     Gets the command that asks for a project file and opens it.
    /// </summary>
    public ICommand OpenProjectCommand => _shell.OpenProjectCommand;

    /// <summary>
    ///     Gets the command that opens the settings on the AI provider page.
    /// </summary>
    public ICommand OpenSettingsCommand => _shell.OpenSettingsCommand;

    /// <summary>
    ///     Gets a value indicating whether the notice about the AI provider shows: the provider needs something from the user
    ///     or cannot be used. It is not shown while the provider is being checked or when it can be used.
    /// </summary>
    public bool ShowProviderBanner => _pill is { NeedsAttention: true } or { IsError: true };

    /// <summary>
    ///     Gets the headline of the notice about the AI provider.
    /// </summary>
    public string ProviderBannerTitle => _pill is null ? string.Empty : $"{_pill.ProviderName} is not ready";

    /// <summary>
    ///     Gets what the check of the AI provider found, which is the text of the notice.
    /// </summary>
    public string ProviderBannerText => _pill?.ToolTipText ?? string.Empty;

    /// <inheritdoc />
    public void Dispose()
    {
        ((INotifyCollectionChanged)_shell.RecentProjects).CollectionChanged -= OnRecentChanged;
        _shell.PropertyChanged -= OnShellPropertyChanged;
        _shell.TalkKey.PropertyChanged -= OnTalkKeyChanged;
        if (_pill is not null)
        {
            _pill.PropertyChanged -= OnPillPropertyChanged;
            _pill = null;
        }
    }

    /// <summary>
    ///     Says that the sentence of the first step changed when the talk key changed.
    /// </summary>
    /// <param name="sender">The setting of the talk key.</param>
    /// <param name="e">The change details.</param>
    private void OnTalkKeyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PushToTalkSetting.Text))
        {
            OnPropertyChanged(nameof(DescribeStepText));
        }
    }

    /// <summary>
    ///     Fills the cards again from the recent projects of the shell.
    /// </summary>
    private void RebuildCards()
    {
        _cards.Clear();
        foreach (var item in _shell.RecentProjects.Take(MaximumCards))
        {
            _cards.Add(item);
        }

        OnPropertyChanged(nameof(HasRecent));
        OnPropertyChanged(nameof(HasNoRecent));
        OnPropertyChanged(nameof(CountText));
    }

    /// <summary>
    ///     Follows the provider pill that the shell has now.
    /// </summary>
    private void FollowPill()
    {
        if (ReferenceEquals(_pill, _shell.ProviderPill))
        {
            return;
        }

        if (_pill is not null)
        {
            _pill.PropertyChanged -= OnPillPropertyChanged;
        }

        _pill = _shell.ProviderPill;
        if (_pill is not null)
        {
            _pill.PropertyChanged += OnPillPropertyChanged;
        }

        RaiseBanner();
    }

    /// <summary>
    ///     Announces a change of the notice about the AI provider.
    /// </summary>
    private void RaiseBanner()
    {
        OnPropertyChanged(nameof(ShowProviderBanner));
        OnPropertyChanged(nameof(ProviderBannerTitle));
        OnPropertyChanged(nameof(ProviderBannerText));
    }

    /// <summary>
    ///     Follows the recent projects of the shell.
    /// </summary>
    /// <param name="sender">The recent projects.</param>
    /// <param name="e">The change details.</param>
    private void OnRecentChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildCards();
    }

    /// <summary>
    ///     Follows the provider pill that the shell has.
    /// </summary>
    /// <param name="sender">The shell.</param>
    /// <param name="e">The change details.</param>
    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowShell.ProviderPill))
        {
            FollowPill();
        }
    }

    /// <summary>
    ///     Follows the check of the AI provider that the pill reports.
    /// </summary>
    /// <param name="sender">The pill.</param>
    /// <param name="e">The change details.</param>
    private void OnPillPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RaiseBanner();
    }
}
