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

using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OtsSoftwareTests;

/// <summary>
///     Self-validation tests for the Avalonia off-the-shelf software item: the cross-platform UI framework that
///     shows the application's windows, menus, dialogs, bound view models, and pictures. They run the framework
///     headless, with the same calls and kinds of controls the application uses.
/// </summary>
public class AvaloniaTests
{
    /// <summary>
    ///     Test that a modal dialog shown over an owner window completes with the result it is closed with.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [AvaloniaFact]
    public async Task Avalonia_Window_ShowDialog_CompletesWithCloseResult()
    {
        // Arrange: show an owner window and create a dialog hosting a text block
        var owner = new Window { Content = new TextBlock { Text = "owner" } };
        owner.Show();
        var dialog = new Window { Content = new TextBlock { Text = "dialog" } };

        // Act: show the dialog modally, then close it with a result
        var pending = dialog.ShowDialog<string?>(owner);
        var shownBeforeClose = dialog.IsVisible;
        dialog.Close("closed with result");
        var result = await pending;

        // Assert: the dialog was visible while open, and its task completed with the result
        Assert.True(shownBeforeClose);
        Assert.Equal("closed with result", result);
        Assert.False(dialog.IsVisible);

        owner.Close();
    }

    /// <summary>
    ///     Test that a menu item is enabled only when its command can run, keeps its automation identifier, and
    ///     runs its command when chosen.
    /// </summary>
    [AvaloniaFact]
    public void Avalonia_MenuItem_FollowsCommandAndRunsIt()
    {
        // Arrange: a window with a menu whose only item is bound to a command that cannot run yet
        var command = new SwitchableCommand();
        var item = new MenuItem { Header = "Run", Command = command };
        Avalonia.Automation.AutomationProperties.SetAutomationId(item, "RunMenuItem");
        var window = new Window { Width = 300, Height = 200, Content = new Menu { Items = { item } } };
        window.Show();

        // Act and assert: the item is disabled while the command cannot run
        var disabledFirst = !item.IsEffectivelyEnabled;
        command.SetCanRun(true);
        var enabledAfter = item.IsEffectivelyEnabled;

        // Act: choose the item with the mouse
        var center = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), window)
                     ?? throw new InvalidOperationException("The menu item is not part of the window's visual tree.");
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);

        // Assert: the item followed the command, kept its identifier, and ran the command
        Assert.True(disabledFirst);
        Assert.True(enabledAfter);
        Assert.Equal("RunMenuItem", Avalonia.Automation.AutomationProperties.GetAutomationId(item));
        Assert.Equal(1, command.Executions);

        window.Close();
    }

    /// <summary>
    ///     Test that a control property bound to a view model property follows the view model, and that a text box
    ///     edit flows back into the view model.
    /// </summary>
    [AvaloniaFact]
    public void Avalonia_DataBinding_FollowsViewModelInBothDirections()
    {
        // Arrange: a window whose title and a text box are bound to a view model
        var model = new BoundModel { Caption = "first" };
        var box = new TextBox();
        var window = new Window { DataContext = model, Content = box };
        window.Bind(Window.TitleProperty, new Binding(nameof(BoundModel.Caption)));
        box.Bind(TextBox.TextProperty, new Binding(nameof(BoundModel.Caption)) { Mode = BindingMode.TwoWay });
        window.Show();
        var titleFirst = window.Title;

        // Act: change the view model, then edit the text box
        model.Caption = "second";
        var titleSecond = window.Title;
        var boxSecond = box.Text;
        box.Text = "edited";

        // Assert: the title and text box followed the view model, and the edit reached it
        Assert.Equal("first", titleFirst);
        Assert.Equal("second", titleSecond);
        Assert.Equal("second", boxSecond);
        Assert.Equal("edited", model.Caption);

        window.Close();
    }

    /// <summary>
    ///     Test that a writable bitmap in blue, green, red, alpha order with premultiplied alpha keeps the pixels
    ///     written to it and is shown by an image control at its pixel size.
    /// </summary>
    [AvaloniaFact]
    public void Avalonia_WriteableBitmap_KeepsWrittenPixelsAndIsShownByImageControl()
    {
        // Arrange: a 2 x 1 bitmap, one opaque red pixel (BGRA 0,0,255,255) and one half-transparent blue pixel
        // with premultiplied color (BGRA 128,0,0,128)
        byte[] pixels = [0, 0, 255, 255, 128, 0, 0, 128];
        using var bitmap = new WriteableBitmap(
            new PixelSize(2, 1),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        // Act: write the pixels through the framebuffer, show the bitmap, and read the pixels back
        using (var framebuffer = bitmap.Lock())
        {
            Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);
        }

        var image = new Image { Source = bitmap };
        var window = new Window { Content = image };
        window.Show();

        var readBack = new byte[pixels.Length];
        using (var framebuffer = bitmap.Lock())
        {
            Marshal.Copy(framebuffer.Address, readBack, 0, readBack.Length);
        }

        // Assert: the pixels, size, and format round trip, and the image control shows the bitmap
        Assert.Equal(pixels, readBack);
        Assert.Equal(new PixelSize(2, 1), bitmap.PixelSize);
        Assert.Equal(PixelFormat.Bgra8888, bitmap.Format);
        Assert.Same(bitmap, image.Source);

        window.Close();
    }

    /// <summary>
    ///     Test that a bitmap put on the clipboard can be read back with its size.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [AvaloniaFact]
    public async Task Avalonia_Clipboard_Bitmap_RoundTrips()
    {
        // Arrange: a window with a clipboard and a 3 x 2 opaque bitmap
        var window = new Window { Content = new TextBlock { Text = "clipboard" } };
        window.Show();
        var clipboard = window.Clipboard ?? throw new InvalidOperationException("The headless window has no clipboard.");
        using var bitmap = new WriteableBitmap(
            new PixelSize(3, 2),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        using (var framebuffer = bitmap.Lock())
        {
            var buffer = new byte[framebuffer.RowBytes * 2];
            Array.Fill(buffer, (byte)255);
            Marshal.Copy(buffer, 0, framebuffer.Address, buffer.Length);
        }

        // Act: put the bitmap on the clipboard and read it back
        await clipboard.SetBitmapAsync(bitmap);
        using var readBack = await clipboard.TryGetBitmapAsync();

        // Assert: a bitmap of the same size comes back
        Assert.NotNull(readBack);
        Assert.Equal(new PixelSize(3, 2), readBack.PixelSize);

        window.Close();
    }

    /// <summary>
    ///     Test that one data transfer item can carry a file and a bitmap at the same time, and that both come back
    ///     from the transfer, which is what the preview offers to the program a picture is dragged to.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [AvaloniaFact]
    public async Task Avalonia_DataTransfer_ItemWithFileAndBitmap_CarriesBothFormats()
    {
        // Arrange: a window, a PNG file on disk, and a 3 x 2 bitmap
        var window = new Window { Content = new TextBlock { Text = "drag" } };
        window.Show();
        var path = Path.Combine(Path.GetTempPath(), $"canvaspilot-ots-drag-{Guid.NewGuid():N}.png");
        using var bitmap = new WriteableBitmap(
            new PixelSize(3, 2),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        await File.WriteAllBytesAsync(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], TestContext.Current.CancellationToken);
        try
        {
            // Act: get the storage file for the path and put it and the bitmap into one item of a transfer
            var file = await window.StorageProvider.TryGetFileFromPathAsync(new Uri(path));
            var item = new DataTransferItem();
            item.SetFile(file!);
            item.SetBitmap(bitmap);
            var transfer = new DataTransfer();
            transfer.Add(item);

            // Assert: the item and the transfer offer the file format and the bitmap format, and give both back
            Assert.NotNull(file);
            Assert.Contains(DataFormat.File, item.Formats);
            Assert.Contains(DataFormat.Bitmap, item.Formats);
            Assert.Contains(DataFormat.File, transfer.Formats);
            Assert.Contains(DataFormat.Bitmap, transfer.Formats);
            Assert.Equal(Path.GetFileName(path), transfer.TryGetFile()?.Name);
            Assert.Equal(new PixelSize(3, 2), transfer.TryGetBitmap()?.PixelSize);
        }
        finally
        {
            File.Delete(path);
            window.Close();
        }
    }

    /// <summary>
    ///     Command whose ability to run can be switched, for the menu item test.
    /// </summary>
    private sealed class SwitchableCommand : ICommand
    {
        /// <summary>
        ///     Whether the command can run.
        /// </summary>
        private bool _canRun;

        /// <inheritdoc />
        public event EventHandler? CanExecuteChanged;

        /// <summary>
        ///     Gets the number of times the command ran.
        /// </summary>
        public int Executions { get; private set; }

        /// <summary>
        ///     Sets whether the command can run, raising <see cref="CanExecuteChanged" />.
        /// </summary>
        /// <param name="canRun">Whether the command can run.</param>
        public void SetCanRun(bool canRun)
        {
            _canRun = canRun;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public bool CanExecute(object? parameter)
        {
            return _canRun;
        }

        /// <inheritdoc />
        public void Execute(object? parameter)
        {
            Executions++;
        }
    }
}

/// <summary>
///     Minimal view model with one observable text property, bound to controls by the Avalonia tests.
/// </summary>
internal sealed partial class BoundModel : ObservableObject
{
    /// <summary>
    ///     Backing field for the generated <c>Caption</c> property.
    /// </summary>
    [ObservableProperty]
    private string _caption = string.Empty;
}
