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
using System.Windows.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     A recently opened project, as the Recent Projects menu and the welcome screen show it: its name, where it is, when it
///     was opened, how many pictures it holds, and a small picture of its first one.
/// </summary>
public sealed class RecentProjectItem : ObservableObject, IDisposable
{
    /// <summary>
    ///     The picture on the card, or null until it is drawn or when the project has no picture.
    /// </summary>
    private Bitmap? _thumbnail;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecentProjectItem" /> class.
    /// </summary>
    /// <param name="name">The project name shown in the menu.</param>
    /// <param name="filePath">The full path of the project file.</param>
    /// <param name="openCommand">The command that opens the project.</param>
    /// <param name="lastOpened">When the project was last opened, or <see langword="null" /> when that is not known.</param>
    /// <param name="pictureCount">How many pictures the project holds.</param>
    /// <param name="removeCommand">The command that removes the project from the list, or <see langword="null" /> for none.</param>
    /// <param name="now">The time that the opening is told from, or <see langword="null" /> for the present.</param>
    public RecentProjectItem(
        string name,
        string filePath,
        IAsyncRelayCommand openCommand,
        DateTimeOffset? lastOpened = null,
        int pictureCount = 0,
        ICommand? removeCommand = null,
        DateTimeOffset? now = null)
    {
        Name = name;
        Header = name.Replace("_", "__", StringComparison.Ordinal);
        FilePath = filePath;
        FolderPath = Path.GetDirectoryName(filePath) ?? filePath;
        OpenCommand = openCommand;
        RemoveCommand = removeCommand;
        LastOpened = lastOpened;
        PictureCount = pictureCount;
        OpenedText = OpenedTextFor(lastOpened, now ?? DateTimeOffset.Now);
        PictureCountText = PictureCountTextFor(pictureCount);
        DetailText = $"{OpenedText} \u00B7 {PictureCountText}";
        AccessibleName = $"{name}, {OpenedText.ToLowerInvariant()}, {PictureCountText}";
    }

    /// <summary>
    ///     Gets the project name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the menu header: the project name with underscores doubled so the menu shows them as typed
    ///     instead of treating them as access keys.
    /// </summary>
    public string Header { get; }

    /// <summary>
    ///     Gets the full path of the project file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    ///     Gets the folder of the project, which the card shows as the place of the project.
    /// </summary>
    public string FolderPath { get; }

    /// <summary>
    ///     Gets when the project was last opened, or <see langword="null" /> when that is not known.
    /// </summary>
    public DateTimeOffset? LastOpened { get; }

    /// <summary>
    ///     Gets how many pictures the project holds.
    /// </summary>
    public int PictureCount { get; }

    /// <summary>
    ///     Gets the words that tell when the project was opened, such as <c>Opened 2 hours ago</c>.
    /// </summary>
    public string OpenedText { get; }

    /// <summary>
    ///     Gets the words that tell how many pictures the project holds, such as <c>3 pictures</c>.
    /// </summary>
    public string PictureCountText { get; }

    /// <summary>
    ///     Gets the line under the project path: when it was opened and how many pictures it holds.
    /// </summary>
    public string DetailText { get; }

    /// <summary>
    ///     Gets the sentence that a screen reader reads for the card.
    /// </summary>
    public string AccessibleName { get; }

    /// <summary>
    ///     Gets the command that opens the project.
    /// </summary>
    public IAsyncRelayCommand OpenCommand { get; }

    /// <summary>
    ///     Gets the command that removes the project from the list without touching its files, or <see langword="null" />.
    /// </summary>
    public ICommand? RemoveCommand { get; }

    /// <summary>
    ///     Gets the picture on the card, or <see langword="null" />.
    /// </summary>
    public Bitmap? Thumbnail => _thumbnail;

    /// <summary>
    ///     Gets a value indicating whether the card has a picture.
    /// </summary>
    public bool HasThumbnail => _thumbnail is not null;

    /// <summary>
    ///     Gets a value indicating whether the card has no picture to show, which it says with an icon instead.
    /// </summary>
    public bool HasNoThumbnail => _thumbnail is null;

    /// <summary>
    ///     Tells when a project was opened, in words.
    /// </summary>
    /// <param name="opened">When the project was opened, or <see langword="null" /> when that is not known.</param>
    /// <param name="now">The present time.</param>
    /// <returns>The words, such as <c>Opened yesterday</c>.</returns>
    public static string OpenedTextFor(DateTimeOffset? opened, DateTimeOffset now)
    {
        if (opened is not { } stored)
        {
            return "Opened earlier";
        }

        var time = stored.ToOffset(now.Offset);
        var span = now - time;
        if (span < TimeSpan.FromMinutes(1))
        {
            return "Opened just now";
        }

        if (span < TimeSpan.FromHours(1))
        {
            return $"Opened {(int)span.TotalMinutes} min ago";
        }

        if (span < TimeSpan.FromHours(24) && time.Date == now.Date)
        {
            var hours = (int)span.TotalHours;
            return $"Opened {hours} {(hours == 1 ? "hour" : "hours")} ago";
        }

        var days = (now.Date - time.Date).Days;
        return days switch
        {
            <= 0 => "Opened today",
            1 => "Opened yesterday",
            < 7 => $"Opened {days} days ago",
            < 14 => "Opened last week",
            < 30 => $"Opened {days / 7} weeks ago",
            < 60 => "Opened last month",
            _ => "Opened " + time.ToString("MMM d, yyyy", CultureInfo.CurrentCulture),
        };
    }

    /// <summary>
    ///     Tells how many pictures a project holds, in words.
    /// </summary>
    /// <param name="count">The number of pictures.</param>
    /// <returns>The words, such as <c>3 pictures</c>.</returns>
    public static string PictureCountTextFor(int count)
    {
        return count switch
        {
            <= 0 => "no pictures yet",
            1 => "1 picture",
            _ => $"{count.ToString(CultureInfo.CurrentCulture)} pictures",
        };
    }

    /// <summary>
    ///     Gives the card its picture; the item owns the bitmap and disposes it with the previous one or with itself.
    /// </summary>
    /// <param name="bitmap">The bitmap, or <see langword="null" /> for none.</param>
    public void SetThumbnail(Bitmap? bitmap)
    {
        var previous = _thumbnail;
        _thumbnail = bitmap;
        OnPropertyChanged(nameof(Thumbnail));
        OnPropertyChanged(nameof(HasThumbnail));
        OnPropertyChanged(nameof(HasNoThumbnail));
        previous?.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        SetThumbnail(null);
    }
}
