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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     A dictation service for UI tests that is always ready and never listens, because the editor tests only need the
///     view model to exist.
/// </summary>
internal sealed class IdleDictationService : IDictationService
{
    /// <inheritdoc />
    public DictationAvailability CheckAvailability() => DictationAvailability.Ready;

    /// <inheritdoc />
    public Task<DictationAvailability> PrepareAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(DictationAvailability.Ready);

    /// <inheritdoc />
    public Task<DictationStart> BeginAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new DictationStart(null, DictationAvailability.Ready));
}

/// <summary>
///     A message builder for UI tests that builds a message with a text as its payload.
/// </summary>
internal sealed class SimpleMessageBuilder : IMessageBuilder
{
    /// <inheritdoc />
    public Task<SentMessage> BuildAsync(
        ComposerSnapshot message,
        IReadOnlyList<Mark> marks,
        IReadOnlyList<StagedAttachment> attachments,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new SentMessage(message, marks, attachments, "payload", DateTimeOffset.UtcNow));
}

/// <summary>
///     A provider of chip pictures for UI tests that gives one picture for every chip and records what it was asked for.
/// </summary>
internal sealed class FixedThumbnailProvider : IChipThumbnailProvider
{
    /// <summary>
    ///     Gets the picture that every chip gets.
    /// </summary>
    public WriteableBitmap Bitmap { get; } = new(new PixelSize(8, 8), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

    /// <summary>
    ///     Gets the chips that a picture was asked for, in order.
    /// </summary>
    public List<ChipSegment> Requested { get; } = [];

    /// <inheritdoc />
    public Task<Bitmap?> GetThumbnailAsync(ChipSegment chip, CancellationToken cancellationToken)
    {
        Requested.Add(chip);
        return Task.FromResult<Bitmap?>(Bitmap);
    }
}

/// <summary>
///     A window with the composer editor, its view model, and a project folder, for the headless tests of the editor.
///     It offers the keystrokes and the layout that the tests need, and checks the one rule that must hold after every
///     operation: the editing surface and the message hold the same text.
/// </summary>
internal sealed class ComposerHarness : IDisposable
{
    /// <summary>
    ///     Gets the modifier of the platform's command shortcuts, such as copy, paste, and undo: the Command key on
    ///     macOS and the Control key elsewhere, as the editing control itself chooses it.
    /// </summary>
    public static RawInputModifiers Command => OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ComposerHarness" /> class and shows the window.
    /// </summary>
    /// <param name="openProject"><see langword="true" /> to open the project, which makes the message editable.</param>
    public ComposerHarness(bool openProject = true)
    {
        Folder = Directory.CreateTempSubdirectory("canvaspilot-ui-composer-").FullName;
        Project = Path.Combine(Folder, "project");
        Directory.CreateDirectory(Project);
        Marks = new MarkRegistry();
        ViewModel = new ComposerViewModel(new SimpleMessageBuilder(), Marks, new IdleDictationService());
        View = new ComposerEditorView { DataContext = ViewModel };
        Window = new Window { Width = 520, Height = 320, Content = View };
        Window.Show();
        if (openProject)
        {
            ViewModel.SetProject(Project);
        }

        View.FocusEditor();
        Layout();
    }

    /// <summary>
    ///     Gets the temporary folder that holds the project and the files of the test.
    /// </summary>
    public string Folder { get; }

    /// <summary>
    ///     Gets the folder of the project.
    /// </summary>
    public string Project { get; }

    /// <summary>
    ///     Gets the marks of the draft.
    /// </summary>
    public MarkRegistry Marks { get; }

    /// <summary>
    ///     Gets the view model of the message.
    /// </summary>
    public ComposerViewModel ViewModel { get; }

    /// <summary>
    ///     Gets the view under test.
    /// </summary>
    public ComposerEditorView View { get; }

    /// <summary>
    ///     Gets the window that shows the view.
    /// </summary>
    public Window Window { get; }

    /// <summary>
    ///     Gets the editing surface inside the view.
    /// </summary>
    public AvaloniaEdit.TextEditor Editor => View.FindControl<AvaloniaEdit.TextEditor>("Editor")!;

    /// <summary>
    ///     Gets the chip controls that are drawn in the editing surface now.
    /// </summary>
    public IReadOnlyList<ChipView> Chips => [.. Editor.GetVisualDescendants().OfType<ChipView>()];

    /// <summary>
    ///     Gets the text of the message with a placeholder character for each chip.
    /// </summary>
    public string Message => ViewModel.Document.FlatText;

    /// <summary>
    ///     Gets the text of the message with a hash for each chip, so that assertions can be read.
    /// </summary>
    public string ReadableMessage => Message.Replace(ComposerSnapshot.ChipPlaceholder, '#');

    /// <summary>
    ///     Writes a file in the test folder.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <returns>The full path of the file.</returns>
    public string MakeFile(string name)
    {
        var path = Path.Combine(Folder, name);
        File.WriteAllText(path, "content of " + name);
        return path;
    }

    /// <summary>
    ///     Makes the window lay out and run the work that was queued, so that drawn lines and chips are current.
    /// </summary>
    public void Layout()
    {
        Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
    }

    /// <summary>
    ///     Types text as the keyboard or an input method delivers it, as one text input.
    /// </summary>
    /// <param name="text">The text.</param>
    public void Type(string text)
    {
        Window.KeyTextInput(text);
        Layout();
    }

    /// <summary>
    ///     Types each character of a text as its own key press, as a person typing does.
    /// </summary>
    /// <param name="text">The text.</param>
    public void TypeEach(string text)
    {
        foreach (var character in text)
        {
            Window.KeyTextInput(character.ToString());
        }

        Layout();
    }

    /// <summary>
    ///     Presses and releases a key.
    /// </summary>
    /// <param name="key">The physical key.</param>
    /// <param name="modifiers">The modifiers held down.</param>
    public void Press(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(key, modifiers);
        Window.KeyReleaseQwerty(key, modifiers);
        Layout();
    }

    /// <summary>
    ///     Puts the caret at an offset of the message and clears the selection.
    /// </summary>
    /// <param name="offset">The offset.</param>
    public void MoveCaret(int offset)
    {
        Editor.TextArea.ClearSelection();
        Editor.CaretOffset = offset;
        Layout();
    }

    /// <summary>
    ///     Selects part of the message.
    /// </summary>
    /// <param name="start">The offset of the first selected position.</param>
    /// <param name="length">The number of selected positions.</param>
    public void Select(int start, int length)
    {
        Editor.Select(start, length);
        Layout();
    }

    /// <summary>
    ///     Waits, while running the work queued on the window, until a condition holds or two seconds have passed.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <returns><see langword="true" /> when the condition held in time.</returns>
    public bool WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        Layout();
        return condition();
    }

    /// <summary>
    ///     Sends a drop to the window at a point of the editing surface, as the operating system does.
    /// </summary>
    /// <param name="data">What is dropped.</param>
    /// <param name="inEditor">The point in the coordinates of the editing surface.</param>
    public void Drop(IDataTransfer data, Point inEditor)
    {
        var point = Editor.TranslatePoint(inEditor, Window)!.Value;
        Window.DragDrop(point, RawDragEventType.DragEnter, data, DragDropEffects.Copy);
        Window.DragDrop(point, RawDragEventType.DragOver, data, DragDropEffects.Copy);
        Window.DragDrop(point, RawDragEventType.Drop, data, DragDropEffects.Copy);
        Layout();
    }

    /// <summary>
    ///     Makes a data transfer that carries files, one item for each, as a file manager does.
    /// </summary>
    /// <param name="paths">The full paths of the files and folders.</param>
    /// <returns>The data transfer.</returns>
    public DataTransfer FilesTransfer(params string[] paths)
    {
        var data = new DataTransfer();
        foreach (var path in paths)
        {
            IStorageItem? item = Directory.Exists(path)
                ? Window.StorageProvider.TryGetFolderFromPathAsync(new Uri(path)).GetAwaiter().GetResult()
                : Window.StorageProvider.TryGetFileFromPathAsync(new Uri(path)).GetAwaiter().GetResult();
            var entry = new DataTransferItem();
            entry.SetFile(item!);
            data.Add(entry);
        }

        return data;
    }

    /// <summary>
    ///     Makes a data transfer that carries a picture.
    /// </summary>
    /// <returns>The data transfer.</returns>
    public static DataTransfer BitmapTransfer()
    {
        var data = new DataTransfer();
        var entry = new DataTransferItem();
        entry.SetBitmap(new WriteableBitmap(new PixelSize(6, 4), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul));
        data.Add(entry);
        return data;
    }

    /// <summary>
    ///     Makes a data transfer that carries text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The data transfer.</returns>
    public static DataTransfer TextTransfer(string text)
    {
        var data = new DataTransfer();
        var entry = new DataTransferItem();
        entry.SetText(text);
        data.Add(entry);
        return data;
    }

    /// <summary>
    ///     Checks the rule that holds after every operation: the surface and the message hold the same text, and the
    ///     number of placeholder characters is the number of chips.
    /// </summary>
    public void AssertConsistent()
    {
        Assert.Equal(ViewModel.Document.FlatText, Editor.Text);
        Assert.Equal(ViewModel.Document.Chips.Count(), Editor.Text.Count(character => character == ComposerSnapshot.ChipPlaceholder));
        Assert.True(View.IsConsistent);
    }

    /// <summary>
    ///     Closes the window and removes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        Window.Close();
        ViewModel.Dispose();

        // A preview that is still being decoded in the background can hold a file for a moment, so deleting retries
        for (var attempt = 0; attempt < 40 && Directory.Exists(Folder); attempt++)
        {
            try
            {
                Directory.Delete(Folder, recursive: true);
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }
    }
}
