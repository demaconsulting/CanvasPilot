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

using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.ProjectSubsystem;

/// <summary>
///     RecentProjects keeps the most-recently-used list of project files in the application settings: at most
///     <see cref="MaxCount" /> entries, without case-insensitive duplicates, the most recent first, and without
///     files that no longer exist.
/// </summary>
public sealed class RecentProjects : IRecentProjects
{
    /// <summary>
    ///     The most entries the list holds.
    /// </summary>
    public const int MaxCount = 10;

    /// <summary>
    ///     Where the list is stored.
    /// </summary>
    private readonly ISettingsService _settings;

    /// <summary>
    ///     Creates the list over the application settings.
    /// </summary>
    /// <param name="settings">The settings that hold the list.</param>
    public RecentProjects(ISettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public IReadOnlyList<string> GetRecent()
    {
        var stored = _settings.Current.RecentProjects;
        var existing = stored.Where(File.Exists).Take(MaxCount).ToList();
        if (existing.Count != stored.Count)
        {
            // Drop the missing files from the stored list too; failing to store is not a problem for a read.
            TryUpdate(list => list.Where(File.Exists).Take(MaxCount));
        }

        return existing;
    }

    /// <inheritdoc />
    public bool Add(string projectFile)
    {
        var path = Normalize(projectFile);
        return path is not null && TryUpdate(list => new[] { path }
            .Concat(list.Where(entry => !SamePath(entry, path)))
            .Take(MaxCount));
    }

    /// <inheritdoc />
    public bool Remove(string projectFile)
    {
        var path = Normalize(projectFile);
        return path is not null && TryUpdate(list => list.Where(entry => !SamePath(entry, path)));
    }

    /// <inheritdoc />
    public bool Clear() => TryUpdate(_ => []);

    /// <summary>
    ///     Compares two stored paths ignoring case.
    /// </summary>
    /// <param name="left">The first path.</param>
    /// <param name="right">The second path.</param>
    /// <returns><see langword="true" /> when the paths are the same.</returns>
    private static bool SamePath(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Turns a path into the full path that is stored.
    /// </summary>
    /// <param name="projectFile">The path as given.</param>
    /// <returns>The full path, or <see langword="null" /> when the path is empty or not valid.</returns>
    private static string? Normalize(string? projectFile)
    {
        if (string.IsNullOrWhiteSpace(projectFile))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(projectFile.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Stores a new list built from the latest stored list, and announces the change when it was stored.
    /// </summary>
    /// <param name="change">Builds the new list from the stored list.</param>
    /// <returns><see langword="true" /> when the list was stored.</returns>
    private bool TryUpdate(Func<IReadOnlyList<string>, IEnumerable<string>> change)
    {
        try
        {
            _settings.Update(settings => settings with { RecentProjects = change(settings.RecentProjects).ToList() });
        }
        catch (IOException)
        {
            // The settings service has logged the failure; the list in use keeps working without being stored.
            return false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
