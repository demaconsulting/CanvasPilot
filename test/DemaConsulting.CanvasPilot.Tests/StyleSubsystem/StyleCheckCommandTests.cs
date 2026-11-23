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

using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Hand-written note sink for tests: it records every group and list of notes that it was given.
/// </summary>
public sealed class RecordingNoteSink : IRenderNoteSink
{
    /// <summary>
    ///     Gets the calls, in order.
    /// </summary>
    public List<(string Group, IReadOnlyList<RenderDiagnostic> Notes)> Calls { get; } = [];

    /// <summary>
    ///     Gets the synchronization context that was current at each call, in the order of the calls.
    /// </summary>
    public List<SynchronizationContext?> Contexts { get; } = [];

    /// <inheritdoc />
    public void ShowNotes(string group, IReadOnlyList<RenderDiagnostic> notes)
    {
        Calls.Add((group, notes));
        Contexts.Add(SynchronizationContext.Current);
    }
}

/// <summary>
///     A palette that holds the measure of the picture at the point where it reads the palette until the test lets it go,
///     and notes the synchronization context of the thread that reads it. It makes the order of events of a check
///     deterministic: the check cannot finish before the caller has awaited it.
/// </summary>
public sealed class GatedPalette : IReadOnlyDictionary<string, string>
{
    /// <summary>
    ///     The colors that are held.
    /// </summary>
    private readonly IReadOnlyDictionary<string, string> _inner;

    /// <summary>
    ///     The gate that holds the reader of the palette.
    /// </summary>
    private readonly ManualResetEventSlim _gate = new(false);

    /// <summary>
    ///     The token that frees a reader that waits on the gate when the test is cancelled.
    /// </summary>
    private readonly CancellationToken _cancellation;

    /// <summary>
    ///     Creates a palette that holds its reader.
    /// </summary>
    /// <param name="inner">The colors.</param>
    /// <param name="cancellation">The token that frees a held reader when the test is cancelled.</param>
    public GatedPalette(IReadOnlyDictionary<string, string> inner, CancellationToken cancellation)
    {
        _inner = inner;
        _cancellation = cancellation;
    }

    /// <summary>
    ///     Gets the synchronization context that was current on the thread that read the palette.
    /// </summary>
    public SynchronizationContext? ReaderContext { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether the palette was read.
    /// </summary>
    public bool WasRead { get; private set; }

    /// <inheritdoc />
    public int Count => _inner.Count;

    /// <inheritdoc />
    public IEnumerable<string> Keys => _inner.Keys;

    /// <inheritdoc />
    public IEnumerable<string> Values => _inner.Values;

    /// <inheritdoc />
    public string this[string key] => _inner[key];

    /// <summary>
    ///     Lets the held reader go on.
    /// </summary>
    public void Release() => _gate.Set();

    /// <inheritdoc />
    public bool ContainsKey(string key) => _inner.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out string value) => _inner.TryGetValue(key, out value!);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        ReaderContext = SynchronizationContext.Current;
        WasRead = true;
        _gate.Wait(_cancellation);
        return _inner.GetEnumerator();
    }

    /// <inheritdoc />
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
///     A synchronization context that counts how often work was posted to it and runs the work on the pool, so that a test
///     can tell that a continuation went back through the context of its owner.
/// </summary>
public sealed class CountingSynchronizationContext : SynchronizationContext
{
    /// <summary>
    ///     How many times work was posted.
    /// </summary>
    private int _posts;

    /// <summary>
    ///     Gets how many times work was posted to the context.
    /// </summary>
    public int Posts => Volatile.Read(ref _posts);

    /// <inheritdoc />
    public override void Post(SendOrPostCallback d, object? state)
    {
        Interlocked.Increment(ref _posts);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            // The work runs as if on the owner of the context
            SetSynchronizationContext(this);
            d(state);
        });
    }
}

/// <summary>
///     Unit tests for <see cref="StyleCheckCommand" />, the check of the shown picture against the style kit of the
///     project: the cases in which nothing is checked and the user is told why, a picture on and off the palette with the
///     real checker, a picture that is replaced while it is checked, the announcement of a picture, and that the result
///     comes back through the context of the caller.
/// </summary>
public sealed class StyleCheckCommandTests
{
    /// <summary>
    ///     Gets the token that cancels the running test.
    /// </summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Test that with no picture nothing is checked, and the user is told to select one.
    /// </summary>
    [Fact]
    public async Task StyleCheckCommand_RunAsync_NoPicture_ReturnsMessageAndShowsNothing()
    {
        // Arrange: a style kit and no picture
        var (command, images, active, sink) = Create();
        active.Use(Entry());

        // Act
        var canCheck = command.CanCheck;
        var message = await command.RunAsync();

        // Assert
        Assert.False(canCheck);
        Assert.Contains("no picture", message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(sink.Calls);
        Assert.Null(images.CurrentImage);
    }

    /// <summary>
    ///     Test that with no style kit, and with a kit that cannot be found, nothing is checked and the message says which.
    /// </summary>
    [Fact]
    public async Task StyleCheckCommand_RunAsync_NoKitOrMissingKit_ReturnsMessageAndShowsNothing()
    {
        // Arrange: a picture and a project with no style
        var (command, images, active, sink) = Create();
        images.CurrentImage = Picture(Colors.White, Colors.White);

        // Act: check with no style, then with a kit that cannot be found
        var withoutKit = await command.RunAsync();
        active.UseMissing("team-kit");
        var withMissingKit = await command.RunAsync();

        // Assert: each message says what to do, the second names the kit, and nothing was shown
        Assert.Contains("no style kit", withoutKit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("title bar", withoutKit, StringComparison.Ordinal);
        Assert.Contains("'team-kit'", withMissingKit, StringComparison.Ordinal);
        Assert.Contains("cannot be found", withMissingKit, StringComparison.Ordinal);
        Assert.Empty(sink.Calls);
    }

    /// <summary>
    ///     Test that a picture of the colors of the kit is checked with the real checker and its notes say it passed, with
    ///     no color off the palette.
    /// </summary>
    [Fact]
    public async Task StyleCheckCommand_RunAsync_PictureOnThePalette_ShowsPassedNotes()
    {
        // Arrange: a picture of two palette colors
        var (command, images, active, sink) = Create();
        active.Use(Entry());
        images.CurrentImage = Picture(Colors.White, Colors.Blue);

        // Act
        var message = await command.RunAsync();

        // Assert: the notes of the group were shown once, passed, and name no color off the palette
        Assert.Null(message);
        var (group, notes) = Assert.Single(sink.Calls);
        Assert.Equal("style", group);
        Assert.Equal("style-summary", notes[0].Code);
        Assert.Contains("PASSED", notes[0].Message, StringComparison.Ordinal);
        Assert.DoesNotContain(notes, note => note.Code == "style-off-palette");
        Assert.All(notes, note => Assert.StartsWith("style-", note.Code, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that a picture with a color that is not in the palette is checked with the real checker and its notes say it
    ///     failed and name the color and the nearest role.
    /// </summary>
    [Fact]
    public async Task StyleCheckCommand_RunAsync_PictureOffThePalette_ShowsFailedNotesWithTheColor()
    {
        // Arrange: a picture that is a quarter of a color that the palette does not have
        var (command, images, active, sink) = Create();
        active.Use(Entry());
        images.CurrentImage = Picture(Colors.White, Colors.Red);

        // Act
        var message = await command.RunAsync();

        // Assert: the summary says failed, and a warning names the color
        Assert.Null(message);
        var notes = Assert.Single(sink.Calls).Notes;
        Assert.Contains("FAILED", notes[0].Message, StringComparison.Ordinal);
        var warning = Assert.Single(notes, note => note.Code == "style-off-palette");
        Assert.Equal(RenderDiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("#D32F2F", warning.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Test that the result of a check of a picture that was replaced while the check ran is dropped.
    /// </summary>
    [Fact]
    public async Task StyleCheckCommand_RunAsync_PictureReplacedDuringTheCheck_ShowsNothing()
    {
        // Arrange: a picture, and a sink that replaces the picture when the check starts to show notes is not possible, so
        // the picture is replaced from the kit that the active style gives
        var (command, images, active, sink) = Create();
        var replacing = new ReplacingActiveStyle(active, () => images.CurrentImage = Picture(Colors.White, Colors.White));
        var replacingCommand = new StyleCheckCommand(images, replacing, sink);
        active.Use(Entry());
        images.CurrentImage = Picture(Colors.White, Colors.Blue);

        // Act: the picture is replaced as soon as the command has asked for the kit
        var message = await replacingCommand.RunAsync();

        // Assert: the check was made and its result dropped, because it was about the picture that is gone
        Assert.Null(message);
        Assert.Empty(sink.Calls);
        command.Dispose();
        replacingCommand.Dispose();
    }

    /// <summary>
    ///     Test that a failure of the check is logged to the caller as a message and not thrown, and that the notes are not
    ///     shown.
    /// </summary>
    [Fact]
    public async Task StyleCheckCommand_RunAsync_CheckThrows_ReturnsMessageAndShowsNothing()
    {
        // Arrange: a kit that has no background, which the checker cannot flatten a picture over
        var (command, images, active, sink) = Create();
        var kit = StyleKitPlaces.Kit("broken") with { Palette = new Dictionary<string, string>() };
        active.Use(new StyleKitEntry(kit, StyleKitLocation.User, "folder", null, false, false, null, []));
        images.CurrentImage = Picture(Colors.White, Colors.Blue);

        // Act
        var message = await command.RunAsync();

        // Assert: the user is told that the picture could not be checked, and nothing was shown
        Assert.StartsWith("The picture could not be checked", message, StringComparison.Ordinal);
        Assert.Empty(sink.Calls);
    }

    /// <summary>
    ///     Test that the command says when a picture appears or goes away, and stops after it is disposed.
    /// </summary>
    [Fact]
    public void StyleCheckCommand_CanCheck_PictureComesAndGoes_IsAnnouncedUntilDisposed()
    {
        // Arrange: a command and a handler that counts
        var (command, images, _, _) = Create();
        var count = 0;
        command.CanCheckChanged += (_, _) => count++;

        // Act: a picture appears, goes away, and appears after disposal
        images.CurrentImage = Picture(Colors.White, Colors.White);
        var canCheckWithPicture = command.CanCheck;
        images.CurrentImage = null;
        var canCheckWithout = command.CanCheck;
        var countBeforeDispose = count;
        command.Dispose();
        command.Dispose();
        images.CurrentImage = Picture(Colors.White, Colors.White);

        // Assert
        Assert.True(canCheckWithPicture);
        Assert.False(canCheckWithout);
        Assert.Equal(2, countBeforeDispose);
        Assert.Equal(2, count);
    }

    /// <summary>
    ///     Test that the result of the check is applied through the synchronization context of the caller and that the colors
    ///     are measured off that context.
    /// </summary>
    [Fact]
    public async Task StyleCheckCommand_RunAsync_CalledUnderAContext_AppliesTheResultThroughIt()
    {
        // Arrange: a command whose measure is held at the palette until the test lets it go, so that the check cannot
        // finish before it was awaited and its continuation must be posted, and a caller that counts what is posted to it
        var (command, images, active, sink) = Create();
        var kit = Entry().Kit;
        var palette = new GatedPalette(kit.Palette, Ct);
        active.Use(new StyleKitEntry(kit with { Palette = palette }, StyleKitLocation.BuiltIn, "folder", null, true, false, null, []));
        images.CurrentImage = Picture(Colors.White, Colors.Blue);
        var context = new CountingSynchronizationContext();
        var previous = SynchronizationContext.Current;
        Task<string?> run;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            run = command.RunAsync();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        // Act: let the held measure go and wait for the check to finish
        var heldUntilReleased = run.IsCompleted;
        palette.Release();
        var message = await run.WaitAsync(TimeSpan.FromSeconds(30), Ct);

        // Assert: the check was still running when it was awaited, the colors were measured off the context of the caller,
        // the continuation went through the context of the caller once, and the notes were shown on it
        Assert.False(heldUntilReleased);
        Assert.Null(message);
        Assert.True(palette.WasRead);
        Assert.Null(palette.ReaderContext);
        Assert.Equal(1, context.Posts);
        Assert.Single(sink.Calls);
        Assert.Same(context, Assert.Single(sink.Contexts));
    }

    /// <summary>
    ///     Test that missing collaborators are refused.
    /// </summary>
    [Fact]
    public void StyleCheckCommand_Constructor_NullArguments_Throw()
    {
        // Arrange: valid collaborators
        var images = new FakeRenderedImageSource();
        var active = new FakeActiveStyle();
        var sink = new RecordingNoteSink();

        // Act / Assert: each missing collaborator is refused
        Assert.Throws<ArgumentNullException>(() => new StyleCheckCommand(null!, active, sink));
        Assert.Throws<ArgumentNullException>(() => new StyleCheckCommand(images, null!, sink));
        Assert.Throws<ArgumentNullException>(() => new StyleCheckCommand(images, active, null!));
    }

    /// <summary>
    ///     Makes a command over a fake source of pictures, a fake active style, and a recording sink.
    /// </summary>
    /// <returns>The command and what it works with.</returns>
    private static (StyleCheckCommand Command, FakeRenderedImageSource Images, FakeActiveStyle Active, RecordingNoteSink Sink) Create()
    {
        var images = new FakeRenderedImageSource();
        var active = new FakeActiveStyle();
        var sink = new RecordingNoteSink();
        return (new StyleCheckCommand(images, active, sink), images, active, sink);
    }

    /// <summary>
    ///     Makes the entry of a kit with the palette white, dark blue-gray, and blue.
    /// </summary>
    /// <returns>The entry.</returns>
    private static StyleKitEntry Entry() =>
        new(StyleKitPlaces.Kit("check-kit", name: "Check Kit"), StyleKitLocation.BuiltIn, "folder", null, true, false, null, []);

    /// <summary>
    ///     Makes a 40 by 40 picture whose left three quarters are one color and whose right quarter is another.
    /// </summary>
    /// <param name="main">The main color.</param>
    /// <param name="side">The color of the right quarter.</param>
    /// <returns>The picture.</returns>
    private static RenderedImage Picture(Colors main, Colors side)
    {
        const int size = 40;
        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var color = x < 30 ? main : side;
                var offset = ((y * size) + x) * 4;
                (pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]) = color switch
                {
                    Colors.Blue => ((byte)0x25, (byte)0x63, (byte)0xEB, (byte)255),
                    Colors.Red => ((byte)0xD3, (byte)0x2F, (byte)0x2F, (byte)255),
                    _ => ((byte)255, (byte)255, (byte)255, (byte)255)
                };
            }
        }

        return new RenderedImage("logo.svg", "svg", size, size, pixels);
    }

    /// <summary>
    ///     The colors of the test pictures.
    /// </summary>
    private enum Colors
    {
        /// <summary>White, the background of the kit.</summary>
        White,

        /// <summary>A blue of the palette.</summary>
        Blue,

        /// <summary>A red that the palette does not have.</summary>
        Red
    }

    /// <summary>
    ///     An active style that runs an action when its kit is asked for, to change the picture while the check starts.
    /// </summary>
    /// <param name="inner">The style to ask.</param>
    /// <param name="onActive">The action to run once the kit has been asked for.</param>
    private sealed class ReplacingActiveStyle(IActiveStyle inner, Action onActive) : IActiveStyle
    {
        /// <inheritdoc />
        public event EventHandler? Changed
        {
            add => inner.Changed += value;
            remove => inner.Changed -= value;
        }

        /// <inheritdoc />
        public StyleKitEntry? Active
        {
            get
            {
                var entry = inner.Active;
                onActive();
                return entry;
            }
        }

        /// <inheritdoc />
        public string? ActiveId => inner.ActiveId;

        /// <inheritdoc />
        public bool IsMissing => inner.IsMissing;

        /// <inheritdoc />
        public Task SetActiveAsync(string? id) => inner.SetActiveAsync(id);
    }
}
