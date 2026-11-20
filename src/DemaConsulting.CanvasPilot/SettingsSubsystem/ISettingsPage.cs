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

namespace DemaConsulting.CanvasPilot.SettingsSubsystem;

/// <summary>
///     One page of the settings dialog. A page edits its own working state and merges it into an
///     <see cref="AppSettings" /> only when the user applies the dialog. The view model type name must end in
///     <c>ViewModel</c> so the view locator can find its view.
/// </summary>
public interface ISettingsPage : INotifyPropertyChanged
{
    /// <summary>
    ///     Gets the title shown in the dialog's page list.
    /// </summary>
    string Title { get; }

    /// <summary>
    ///     Gets a value indicating whether the page's current entries can be applied. A change of this value is
    ///     reported through <see cref="INotifyPropertyChanged.PropertyChanged" />.
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    ///     Replaces the page's working state with the values in <paramref name="settings" />.
    /// </summary>
    /// <param name="settings">The settings to show.</param>
    void Load(AppSettings settings);

    /// <summary>
    ///     Merges the page's working state into <paramref name="settings" />, leaving every value the page does
    ///     not own unchanged.
    /// </summary>
    /// <param name="settings">The settings to update.</param>
    /// <returns>The updated settings.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="IsValid" /> is <see langword="false" />.</exception>
    AppSettings Apply(AppSettings settings);

    /// <summary>
    ///     Called once when the dialog closes by any route (OK, Cancel, or closing the window), so the page can
    ///     cancel background work such as downloads.
    /// </summary>
    void OnClosed();
}
