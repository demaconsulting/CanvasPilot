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
using System.Diagnostics;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     Shows a folder in the file manager of the system. The Help menu uses it to open the folder of the log, and does not
///     know how a system does it, so that a test can stand in for the file manager.
/// </summary>
public interface IFolderLauncher
{
    /// <summary>
    ///     Shows a folder in the file manager of the system.
    /// </summary>
    /// <param name="folder">The full path of the folder.</param>
    /// <param name="failure">What went wrong, in a sentence, when the folder could not be shown; otherwise <see langword="null" />.</param>
    /// <returns><see langword="true" /> when the file manager was asked to show the folder.</returns>
    /// <exception cref="ArgumentException">Thrown when the folder is empty.</exception>
    bool TryOpen(string folder, out string? failure);
}

/// <summary>
///     The <see cref="IFolderLauncher" /> of the real system: Explorer on Windows, <c>open</c> on macOS, and
///     <c>xdg-open</c> elsewhere. The folder is made when it does not exist, and is passed as an argument and not through a
///     command line, so that its name is never read as anything else.
/// </summary>
public sealed class SystemFolderLauncher : IFolderLauncher
{
    /// <inheritdoc />
    public bool TryOpen(string folder, out string? failure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        try
        {
            Directory.CreateDirectory(folder);
            var start = new ProcessStartInfo
            {
                FileName = FileManagerName(),
                UseShellExecute = false,
            };
            start.ArgumentList.Add(folder);

            // The file manager is not waited for: Explorer, in particular, answers with codes that mean nothing
            using var process = Process.Start(start);
            failure = process is null ? "The file manager did not start." : null;
            return process is not null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            failure = ex.Message;
            return false;
        }
    }

    /// <summary>
    ///     Gives the program that shows a folder on the system that runs the application.
    /// </summary>
    /// <returns>The name of the program.</returns>
    private static string FileManagerName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "explorer.exe";
        }

        return OperatingSystem.IsMacOS() ? "open" : "xdg-open";
    }
}
