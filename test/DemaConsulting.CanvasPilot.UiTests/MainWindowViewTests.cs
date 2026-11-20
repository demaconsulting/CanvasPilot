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

using Avalonia.Headless.XUnit;
using DemaConsulting.CanvasPilot.AppShellSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for the main window.
/// </summary>
public class MainWindowViewTests
{
    /// <summary>
    ///     Test that the main window opens headless and shows the shell title.
    /// </summary>
    [AvaloniaFact]
    public void MainWindowView_Show_OpensWithShellTitle()
    {
        // Arrange: create the main window over a new shell
        var shell = new MainWindowShell();
        var window = new MainWindowView(shell);

        // Act: show the window headless
        window.Show();

        // Assert: the window is visible and bound to the shell title
        Assert.True(window.IsVisible);
        Assert.Equal(shell.Title, window.Title);

        window.Close();
    }
}
