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

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OtsSoftwareTests;

/// <summary>
///     Self-validation tests for the Avalonia and CommunityToolkit.Mvvm off-the-shelf software items.
/// </summary>
public class AvaloniaTests
{
    /// <summary>
    ///     Test that Avalonia can show a window headless.
    /// </summary>
    [AvaloniaFact]
    public void Avalonia_HeadlessWindow_ShowsAndHostsContent()
    {
        // Arrange: create a window hosting a text block
        var text = new TextBlock { Text = "OTS" };
        var window = new Window { Content = text };

        // Act: show the window headless
        window.Show();

        // Assert: the window is visible and hosts the content
        Assert.True(window.IsVisible);
        Assert.Same(text, window.Content);

        window.Close();
    }

    /// <summary>
    ///     Test that CommunityToolkit.Mvvm raises property change notifications.
    /// </summary>
    [Fact]
    public void CommunityToolkitMvvm_ObservableObject_RaisesPropertyChanged()
    {
        // Arrange: create an observable object and capture notifications
        var sample = new SampleObservable();
        var raised = new List<string?>();
        sample.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Act: change the property
        sample.Value = "changed";

        // Assert: the notification names the property
        Assert.Equal([nameof(SampleObservable.Value)], raised);
    }
}

/// <summary>
///     Minimal observable object used to exercise CommunityToolkit.Mvvm.
/// </summary>
internal sealed partial class SampleObservable : ObservableObject
{
    /// <summary>
    ///     Backing field for the generated <c>Value</c> property.
    /// </summary>
    [ObservableProperty]
    private string _value = string.Empty;
}
