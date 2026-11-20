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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.AnnotationSubsystem;

/// <summary>
///     The tool the user has chosen for the pointer on the picture.
/// </summary>
public enum AnnotationTool
{
    /// <summary>
    ///     No tool: the pointer does what it does without the overlay, for example it drags the picture out.
    /// </summary>
    None,

    /// <summary>
    ///     Draws a rectangle.
    /// </summary>
    Rectangle,

    /// <summary>
    ///     Draws an ellipse.
    /// </summary>
    Ellipse,

    /// <summary>
    ///     Draws a line that follows the pointer.
    /// </summary>
    Freehand,

    /// <summary>
    ///     Draws an arrow from where the pointer was pressed to where it is released.
    /// </summary>
    Arrow
}

/// <summary>
///     Tells that a mark was made on the picture.
/// </summary>
public sealed class MarkAddedEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MarkAddedEventArgs" /> class.
    /// </summary>
    /// <param name="mark">The mark that was made.</param>
    /// <exception cref="ArgumentNullException">Thrown when the mark is <see langword="null" />.</exception>
    public MarkAddedEventArgs(Mark mark)
    {
        ArgumentNullException.ThrowIfNull(mark);
        Mark = mark;
    }

    /// <summary>
    ///     Gets the mark that was made. It is already in the mark registry.
    /// </summary>
    public Mark Mark { get; }
}

/// <summary>
///     The logic of the layer on which the user marks the shown picture, without a window: which tool is chosen, the
///     shape that is being drawn in the pixels of the picture, the optional note of a finished shape, the marks that are
///     shown, and what is said to the user. The view of the layer reports the pointer and the keys here, and draws what
///     this class holds, so every rule is tested without a window.
/// </summary>
/// <remarks>
///     <para>
///         <b>Drawing.</b> A press starts a shape on the picture that is shown, moves reshape it, and the release ends
///         it. A shape that is smaller than a pixel is a click and makes no mark. Any other shape waits for a note: a
///         short remark that is entered or skipped, and only then is the mark made in the registry, pinned to the picture
///         that was shown when the drawing started, and reported through <see cref="MarkAdded" />. The note is the last
///         step because a mark never changes after it is made. Starting the next shape, or choosing another tool, keeps
///         the mark with the note typed so far; Escape, a new picture, or a message that can no longer take the mark
///         throws the unfinished shape away.
///     </para>
///     <para>
///         <b>Which marks are shown.</b> A mark belongs to the message while its chip is in the composer, which is told
///         here by <see cref="SetActiveMarkIds" />; only those of them that were drawn on exactly the shown picture are
///         shown, and the others stay in the registry and in the message but are hidden here, with a count that says
///         so. This class does not know the composer: whoever joins the two passes the identities of the mark chips in,
///         and adds the chip of every mark that <see cref="MarkAdded" /> reports.
///     </para>
///     <para>
///         <b>The pointer elsewhere.</b> While a tool is chosen the picture cannot be dragged out, so the host is told
///         through the switch given at construction, and gets the switch back on when no tool is chosen. The pointer is
///         the only way to make a mark for the user without a keyboard; the keyboard way, the arrow keys to move a cursor
///         over the picture and Enter to start and finish the shape, makes the same marks. Marks are never made by voice.
///     </para>
///     <para>
///         The class is owned by the user interface thread and is not safe for concurrent use.
///     </para>
/// </remarks>
public sealed class AnnotationOverlayViewModel : ObservableObject, IDisposable
{
    /// <summary>
    ///     The most points that a freehand shape keeps while it is drawn; the last point is replaced after that.
    /// </summary>
    public const int MaximumDraftPoints = 20000;

    /// <summary>
    ///     How far, in picture pixels, the pointer must move before another point is added to a freehand shape.
    /// </summary>
    public const double MinimumPointDistance = 0.5;

    /// <summary>
    ///     How far, in picture pixels, one press of an arrow key moves the cursor of the keyboard.
    /// </summary>
    public const double CursorStep = 8;

    /// <summary>
    ///     How far, in picture pixels, one press of an arrow key moves the cursor with the Control key held down.
    /// </summary>
    public const double LargeCursorStep = 40;

    /// <summary>
    ///     How far, in picture pixels, one press of an arrow key moves the cursor with the Shift key held down.
    /// </summary>
    public const double SmallCursorStep = 1;

    /// <summary>
    ///     The marks of the message.
    /// </summary>
    private readonly MarkRegistry _marks;

    /// <summary>
    ///     The source of the picture that is shown.
    /// </summary>
    private readonly IRenderedImageSource _pictures;

    /// <summary>
    ///     Tells the host whether the picture may be dragged out, or <see langword="null" /> when nobody asks.
    /// </summary>
    private readonly Action<bool>? _dragOutSwitch;

    /// <summary>
    ///     Optional logger for diagnostics.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     The points of the shape that is being drawn, or that waits for its note.
    /// </summary>
    private readonly List<RenderPoint> _draft = [];

    /// <summary>
    ///     The identities of the mark chips that are in the message.
    /// </summary>
    private HashSet<Guid> _activeIds = [];

    /// <summary>
    ///     The picture the shape under construction is drawn on, or <see langword="null" /> when none is.
    /// </summary>
    private RenderedImage? _stroke;

    /// <summary>
    ///     The tool that is chosen.
    /// </summary>
    private AnnotationTool _currentTool;

    /// <summary>
    ///     Whether the host allows marks to be made.
    /// </summary>
    private bool _isDrawingAllowed = true;

    /// <summary>
    ///     Whether a shape is being drawn.
    /// </summary>
    private bool _isDrawing;

    /// <summary>
    ///     Whether a finished shape waits for its note.
    /// </summary>
    private bool _isNoteEditing;

    /// <summary>
    ///     The note being typed.
    /// </summary>
    private string _noteText = string.Empty;

    /// <summary>
    ///     The kind of the shape that is drawn or waits for its note.
    /// </summary>
    private MarkKind _draftKind;

    /// <summary>
    ///     The number the mark under construction will get.
    /// </summary>
    private int _pendingNumber = 1;

    /// <summary>
    ///     The marks that are shown on the picture.
    /// </summary>
    private IReadOnlyList<Mark> _visibleMarks = [];

    /// <summary>
    ///     How many marks of the message are not shown here.
    /// </summary>
    private int _hiddenMarkCount;

    /// <summary>
    ///     The cursor of the keyboard, or <see langword="null" /> when the keyboard is not in use.
    /// </summary>
    private RenderPoint? _keyboardCursor;

    /// <summary>
    ///     A sentence about what just happened that replaces the usual hint until the next action, or empty.
    /// </summary>
    private string _notice = string.Empty;

    /// <summary>
    ///     Whether the view model has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AnnotationOverlayViewModel" /> class.
    /// </summary>
    /// <param name="marks">The marks of the message; every mark that is drawn is made here.</param>
    /// <param name="pictures">The source of the picture that is shown, which the marks are drawn on.</param>
    /// <param name="dragOutSwitch">
    ///     Called with <see langword="false" /> when a tool is chosen and with <see langword="true" /> when none is, so
    ///     that the host can keep the pointer from dragging the picture out while it draws; <see langword="null" /> for none.
    /// </param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when the marks or the source of the picture is <see langword="null" />.</exception>
    public AnnotationOverlayViewModel(
        MarkRegistry marks,
        IRenderedImageSource pictures,
        Action<bool>? dragOutSwitch = null,
        RollingFileLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentNullException.ThrowIfNull(pictures);

        _marks = marks;
        _pictures = pictures;
        _dragOutSwitch = dragOutSwitch;
        _logger = logger;
        SelectToolCommand = new RelayCommand<AnnotationTool>(SelectTool);
        CommitNoteCommand = new RelayCommand(CommitNote, () => IsNoteEditing);
        SkipNoteCommand = new RelayCommand(SkipNote, () => IsNoteEditing);
        CancelCommand = new RelayCommand(() => Cancel(), () => IsDrawing || IsNoteEditing);
        _pictures.CurrentImageChanged += OnPictureChanged;
        RefreshMarks();
    }

    /// <summary>
    ///     Raised after a mark was made, with the mark, which is already in the registry. Whoever joins the overlay to the
    ///     message adds the mark's chip to the message when this is raised.
    /// </summary>
    public event EventHandler<MarkAddedEventArgs>? MarkAdded;

    /// <summary>
    ///     Gets the command that chooses a tool; its parameter is an <see cref="AnnotationTool" />.
    /// </summary>
    public IRelayCommand<AnnotationTool> SelectToolCommand { get; }

    /// <summary>
    ///     Gets the command that finishes the note and makes the mark, with the note typed so far.
    /// </summary>
    public IRelayCommand CommitNoteCommand { get; }

    /// <summary>
    ///     Gets the command that makes the mark without a note.
    /// </summary>
    public IRelayCommand SkipNoteCommand { get; }

    /// <summary>
    ///     Gets the command that throws the shape that is drawn, or that waits for its note, away.
    /// </summary>
    public IRelayCommand CancelCommand { get; }

    /// <summary>
    ///     Gets the tool that is chosen.
    /// </summary>
    public AnnotationTool CurrentTool => _currentTool;

    /// <summary>
    ///     Gets a value indicating whether a tool that draws is chosen.
    /// </summary>
    public bool IsToolActive => _currentTool != AnnotationTool.None;

    /// <summary>
    ///     Gets or sets a value indicating whether the host allows marks to be made, which it does while the message can be
    ///     edited. Marks that are made while the message cannot take them would be lost, so the tools are off.
    /// </summary>
    public bool IsDrawingAllowed
    {
        get => _isDrawingAllowed;
        set
        {
            if (!SetProperty(ref _isDrawingAllowed, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ToolsEnabled));
            AfterAvailabilityChanged();
        }
    }

    /// <summary>
    ///     Gets a value indicating whether a picture is shown, which the marks can be drawn on.
    /// </summary>
    public bool HasPicture => _pictures.CurrentImage is not null;

    /// <summary>
    ///     Gets a value indicating whether the tools can be used: a picture is shown and the host allows marks.
    /// </summary>
    public bool ToolsEnabled => HasPicture && _isDrawingAllowed;

    /// <summary>
    ///     Gets the width in pixels of the picture that is shown, or zero when there is none.
    /// </summary>
    public int PictureWidth => _pictures.CurrentImage?.Width ?? 0;

    /// <summary>
    ///     Gets the height in pixels of the picture that is shown, or zero when there is none.
    /// </summary>
    public int PictureHeight => _pictures.CurrentImage?.Height ?? 0;

    /// <summary>
    ///     Gets a value indicating whether a shape is being drawn.
    /// </summary>
    public bool IsDrawing => _isDrawing;

    /// <summary>
    ///     Gets a value indicating whether a finished shape waits for its note.
    /// </summary>
    public bool IsNoteEditing => _isNoteEditing;

    /// <summary>
    ///     Gets a value indicating whether there is a shape under construction to show.
    /// </summary>
    public bool HasDraft => _isDrawing || _isNoteEditing;

    /// <summary>
    ///     Gets or sets the note being typed for the finished shape. It is cut to the length that a mark keeps.
    /// </summary>
    public string NoteText
    {
        get => _noteText;
        set
        {
            var text = value ?? string.Empty;
            if (text.Length > MarkRegistry.MaximumNoteLength)
            {
                text = text[..MarkRegistry.MaximumNoteLength];
            }

            SetProperty(ref _noteText, text);
        }
    }

    /// <summary>
    ///     Gets the shape of the shape under construction.
    /// </summary>
    public MarkKind DraftKind => _draftKind;

    /// <summary>
    ///     Gets the points of the shape under construction, in the pixels of the picture, as the pointer visited them.
    ///     A rectangle, an ellipse, and an arrow have two: where they started and where they are now.
    /// </summary>
    public IReadOnlyList<RenderPoint> DraftPoints => _draft;

    /// <summary>
    ///     Gets the number that the mark under construction will get.
    /// </summary>
    public int PendingNumber => _pendingNumber;

    /// <summary>
    ///     Gets the marks that are shown on the picture: those of the message that were drawn on exactly this picture.
    /// </summary>
    public IReadOnlyList<Mark> VisibleMarks => _visibleMarks;

    /// <summary>
    ///     Gets how many marks of the message are not shown, because they were drawn on another picture.
    /// </summary>
    public int HiddenMarkCount => _hiddenMarkCount;

    /// <summary>
    ///     Gets a value indicating whether some marks of the message are not shown.
    /// </summary>
    public bool HasHiddenMarks => _hiddenMarkCount > 0;

    /// <summary>
    ///     Gets the sentence that says that marks are hidden, or an empty text when none are.
    /// </summary>
    public string HiddenMarksText => _hiddenMarkCount switch
    {
        0 => string.Empty,
        1 => "1 mark of the message was made on another picture and is hidden here.",
        _ => string.Create(CultureInfo.InvariantCulture, $"{_hiddenMarkCount} marks of the message were made on another picture and are hidden here."),
    };

    /// <summary>
    ///     Gets the cursor of the keyboard in picture pixels, or <see langword="null" /> when the keyboard is not in use.
    /// </summary>
    public RenderPoint? KeyboardCursor => _keyboardCursor;

    /// <summary>
    ///     Gets what is said to the user: what to do next, or why no mark can be made.
    /// </summary>
    public string StatusText
    {
        get
        {
            if (_isNoteEditing)
            {
                return string.Create(CultureInfo.InvariantCulture, $"Mark {_pendingNumber}: add a short note, or skip it.");
            }

            if (_isDrawing)
            {
                return "Release to finish the mark. Escape cancels it.";
            }

            if (_notice.Length > 0)
            {
                return _notice;
            }

            if (!HasPicture)
            {
                return "Show a picture to mark it.";
            }

            if (!_isDrawingAllowed)
            {
                return "Open a project to mark the picture.";
            }

            return _currentTool == AnnotationTool.None
                ? "Choose a tool to mark the picture."
                : $"Drag on the picture to draw {WithArticle(_currentTool)}, or use the arrow keys and Enter. Escape stops.";
        }
    }

    /// <summary>
    ///     Gets or sets whether no tool is chosen, so that the pointer keeps its own function on the picture. It is
    ///     the choice that the tool bar calls Select; it cannot be switched off except by choosing a tool.
    /// </summary>
    public bool IsPointerSelected
    {
        get => _currentTool == AnnotationTool.None;
        set => SetToolFlag(AnnotationTool.None, value);
    }

    /// <summary>
    ///     Gets or sets whether the rectangle tool is chosen.
    /// </summary>
    public bool IsRectangleSelected
    {
        get => _currentTool == AnnotationTool.Rectangle;
        set => SetToolFlag(AnnotationTool.Rectangle, value);
    }

    /// <summary>
    ///     Gets or sets whether the ellipse tool is chosen.
    /// </summary>
    public bool IsEllipseSelected
    {
        get => _currentTool == AnnotationTool.Ellipse;
        set => SetToolFlag(AnnotationTool.Ellipse, value);
    }

    /// <summary>
    ///     Gets or sets whether the freehand tool is chosen.
    /// </summary>
    public bool IsFreehandSelected
    {
        get => _currentTool == AnnotationTool.Freehand;
        set => SetToolFlag(AnnotationTool.Freehand, value);
    }

    /// <summary>
    ///     Gets or sets whether the arrow tool is chosen.
    /// </summary>
    public bool IsArrowSelected
    {
        get => _currentTool == AnnotationTool.Arrow;
        set => SetToolFlag(AnnotationTool.Arrow, value);
    }

    /// <summary>
    ///     Chooses a tool. An unfinished shape is finished first, with the note typed so far. A tool that draws cannot be
    ///     chosen while <see cref="ToolsEnabled" /> is false; no tool always can.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the tool is not defined.</exception>
    public void SelectTool(AnnotationTool tool)
    {
        if (!Enum.IsDefined(tool))
        {
            throw new ArgumentOutOfRangeException(nameof(tool), tool, "The tool is not known.");
        }

        if (tool != AnnotationTool.None && !ToolsEnabled)
        {
            // Tell the check box that was just ticked to go back
            RaiseToolFlags();
            return;
        }

        if (_isNoteEditing)
        {
            FinishNote(withNote: true);
        }
        else if (_isDrawing)
        {
            Cancel();
        }

        _keyboardCursor = null;
        _notice = string.Empty;
        if (_currentTool != tool)
        {
            _currentTool = tool;
            _dragOutSwitch?.Invoke(tool == AnnotationTool.None);
        }

        RaiseToolFlags();
        OnPropertyChanged(nameof(KeyboardCursor));
        OnPropertyChanged(nameof(StatusText));
    }

    /// <summary>
    ///     Tells which mark chips are in the message, which decides which marks are part of it and so which are shown.
    /// </summary>
    /// <param name="markIds">The identities of the mark chips that are in the message.</param>
    /// <exception cref="ArgumentNullException">Thrown when the identities are <see langword="null" />.</exception>
    public void SetActiveMarkIds(IEnumerable<Guid> markIds)
    {
        ArgumentNullException.ThrowIfNull(markIds);

        _activeIds = [.. markIds];
        RefreshMarks();
    }

    /// <summary>
    ///     Starts a shape at a point of the picture, as the press of the pointer does.
    /// </summary>
    /// <param name="point">The point, in the pixels of the picture.</param>
    /// <returns><see langword="true" /> when a shape was started; <see langword="false" /> when no tool is chosen or none can be used.</returns>
    public bool BeginStroke(RenderPoint point)
    {
        _keyboardCursor = null;
        OnPropertyChanged(nameof(KeyboardCursor));
        return StartShape(point);
    }

    /// <summary>
    ///     Moves the end of the shape that is being drawn, as the move of the pointer does.
    /// </summary>
    /// <param name="point">The point, in the pixels of the picture.</param>
    public void ContinueStroke(RenderPoint point)
    {
        if (!_isDrawing || !IsFinite(point))
        {
            return;
        }

        var inside = ClampToPicture(point);
        if (_draftKind == MarkKind.Freehand)
        {
            var last = _draft[^1];
            if (_draft.Count > 1 && MarkGeometry.Distance(last, inside) < MinimumPointDistance)
            {
                return;
            }

            if (_draft.Count >= MaximumDraftPoints)
            {
                _draft[^1] = inside;
            }
            else
            {
                _draft.Add(inside);
            }
        }
        else
        {
            _draft[1] = inside;
        }

        OnPropertyChanged(nameof(DraftPoints));
    }

    /// <summary>
    ///     Finishes the shape at a point of the picture, as the release of the pointer does. A shape smaller than a pixel
    ///     is a click and makes no mark; any other waits for its note.
    /// </summary>
    /// <param name="point">The point, in the pixels of the picture.</param>
    public void EndStroke(RenderPoint point)
    {
        if (!_isDrawing)
        {
            return;
        }

        ContinueStroke(point);
        var picture = _stroke!;
        try
        {
            // The geometry is made only to see whether the shape is a mark; the registry makes it again
            _ = MarkGeometry.Create(_draftKind, _draft, picture.Width, picture.Height);
        }
        catch (ArgumentException)
        {
            Discard();
            _notice = "Drag to draw a mark; a click makes none.";
            RaiseStateChanged();
            return;
        }

        _isDrawing = false;
        _isNoteEditing = true;
        _noteText = string.Empty;
        _notice = string.Empty;
        OnPropertyChanged(nameof(NoteText));
        RaiseStateChanged();
    }

    /// <summary>
    ///     Makes the mark of the finished shape with the note typed so far, as the Enter key in the note does.
    /// </summary>
    public void CommitNote() => FinishNote(withNote: true);

    /// <summary>
    ///     Makes the mark of the finished shape without a note.
    /// </summary>
    public void SkipNote() => FinishNote(withNote: false);

    /// <summary>
    ///     Throws away the shape that is being drawn, or that waits for its note, and makes no mark.
    /// </summary>
    /// <returns><see langword="true" /> when there was a shape to throw away.</returns>
    public bool Cancel()
    {
        if (!_isDrawing && !_isNoteEditing)
        {
            return false;
        }

        Discard();
        _notice = string.Empty;
        RaiseStateChanged();
        return true;
    }

    /// <summary>
    ///     Does what the Escape key does: throws the shape away when there is one, and otherwise chooses no tool.
    /// </summary>
    /// <returns><see langword="true" /> when the key had an effect.</returns>
    public bool Escape()
    {
        if (Cancel())
        {
            return true;
        }

        if (_currentTool == AnnotationTool.None)
        {
            return false;
        }

        SelectTool(AnnotationTool.None);
        return true;
    }

    /// <summary>
    ///     Moves the cursor of the keyboard over the picture, which reshapes the shape that is being drawn. The cursor
    ///     starts in the middle of the picture.
    /// </summary>
    /// <param name="dx">The horizontal move in picture pixels, positive to the right.</param>
    /// <param name="dy">The vertical move in picture pixels, positive downward.</param>
    /// <returns><see langword="true" /> when the cursor moved or appeared.</returns>
    public bool MoveCursor(double dx, double dy)
    {
        if (!IsToolActive || !ToolsEnabled || _isNoteEditing || _pictures.CurrentImage is not { } picture)
        {
            return false;
        }

        var from = _keyboardCursor ?? (_isDrawing ? _draft[^1] : new RenderPoint(picture.Width / 2.0, picture.Height / 2.0));
        var to = ClampToPicture(new RenderPoint(from.X + dx, from.Y + dy), picture);
        _keyboardCursor = to;
        OnPropertyChanged(nameof(KeyboardCursor));
        if (_isDrawing)
        {
            ContinueStroke(to);
        }

        return true;
    }

    /// <summary>
    ///     Starts the shape at the cursor of the keyboard, or finishes it there when one is being drawn, as the Enter key does.
    /// </summary>
    /// <returns><see langword="true" /> when the key had an effect.</returns>
    public bool ConfirmCursor()
    {
        if (!IsToolActive || !ToolsEnabled || _isNoteEditing || _pictures.CurrentImage is not { } picture)
        {
            return false;
        }

        _keyboardCursor ??= new RenderPoint(picture.Width / 2.0, picture.Height / 2.0);
        OnPropertyChanged(nameof(KeyboardCursor));
        if (_isDrawing)
        {
            EndStroke(_keyboardCursor.Value);
            return true;
        }

        return StartShape(_keyboardCursor.Value);
    }

    /// <summary>
    ///     Hides the cursor of the keyboard, for example when the pointer is used.
    /// </summary>
    public void HideCursor()
    {
        if (_keyboardCursor is null)
        {
            return;
        }

        _keyboardCursor = null;
        OnPropertyChanged(nameof(KeyboardCursor));
    }

    /// <summary>
    ///     Gets the name of a tool as it is spoken in a sentence.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <returns>The name in lower case.</returns>
    public static string ToolName(AnnotationTool tool) => tool switch
    {
        AnnotationTool.Rectangle => "rectangle",
        AnnotationTool.Ellipse => "ellipse",
        AnnotationTool.Freehand => "freehand line",
        AnnotationTool.Arrow => "arrow",
        _ => "pointer",
    };

    /// <summary>
    ///     Stops following the picture and gives the picture back to the pointer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pictures.CurrentImageChanged -= OnPictureChanged;
        if (_currentTool != AnnotationTool.None)
        {
            _currentTool = AnnotationTool.None;
            _dragOutSwitch?.Invoke(true);
        }
    }

    /// <summary>
    ///     Gets the name of a tool with its indefinite article, as in "an arrow".
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <returns>The article and the name.</returns>
    private static string WithArticle(AnnotationTool tool)
    {
        var article = tool is AnnotationTool.Ellipse or AnnotationTool.Arrow ? "an" : "a";
        return $"{article} {ToolName(tool)}";
    }

    /// <summary>
    ///     Gets the shape of a tool.
    /// </summary>
    /// <param name="tool">A tool that draws.</param>
    /// <returns>The shape that the tool draws.</returns>
    private static MarkKind KindOf(AnnotationTool tool) => tool switch
    {
        AnnotationTool.Rectangle => MarkKind.Rectangle,
        AnnotationTool.Ellipse => MarkKind.Ellipse,
        AnnotationTool.Arrow => MarkKind.Arrow,
        _ => MarkKind.Freehand,
    };

    /// <summary>
    ///     Tells whether both coordinates of a point are numbers.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns><see langword="true" /> when neither coordinate is infinite or not a number.</returns>
    private static bool IsFinite(RenderPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

    /// <summary>
    ///     Starts a shape at a point.
    /// </summary>
    /// <param name="point">The point, in the pixels of the picture.</param>
    /// <returns><see langword="true" /> when a shape was started.</returns>
    private bool StartShape(RenderPoint point)
    {
        if (!IsToolActive || !ToolsEnabled || !IsFinite(point) || _pictures.CurrentImage is not { } picture)
        {
            return false;
        }

        // The next shape keeps the one that waits for its note, with the note typed so far
        if (_isNoteEditing)
        {
            FinishNote(withNote: true);
        }

        var start = ClampToPicture(point, picture);
        _stroke = picture;
        _draftKind = KindOf(_currentTool);
        _draft.Clear();
        _draft.Add(start);
        if (_draftKind != MarkKind.Freehand)
        {
            _draft.Add(start);
        }

        _pendingNumber = (_marks.Marks.Length == 0 ? 0 : _marks.Marks.Max(mark => mark.Number)) + 1;
        _isDrawing = true;
        _notice = string.Empty;
        RaiseStateChanged();
        return true;
    }

    /// <summary>
    ///     Makes the mark of the shape that waits for its note.
    /// </summary>
    /// <param name="withNote">Whether to keep the note that was typed.</param>
    private void FinishNote(bool withNote)
    {
        if (!_isNoteEditing)
        {
            return;
        }

        var picture = _stroke!;
        var kind = _draftKind;
        var points = _draft.ToArray();
        var note = withNote ? _noteText : null;
        Discard();
        Mark mark;
        try
        {
            mark = _marks.Add(picture, kind, points, note);
        }
        catch (ArgumentException ex)
        {
            // The shape was checked when it was finished, so this should not happen; the user loses only the shape
            _logger?.Log(LogLevel.Warning, $"A mark could not be made: {ex.Message}");
            RaiseStateChanged();
            return;
        }

        RaiseStateChanged();
        MarkAdded?.Invoke(this, new MarkAddedEventArgs(mark));
    }

    /// <summary>
    ///     Forgets the shape under construction without telling the observers.
    /// </summary>
    private void Discard()
    {
        _isDrawing = false;
        _isNoteEditing = false;
        _noteText = string.Empty;
        _stroke = null;
        _draft.Clear();
    }

    /// <summary>
    ///     Tells the observers that the shape under construction, the note, or what is said changed.
    /// </summary>
    private void RaiseStateChanged()
    {
        OnPropertyChanged(nameof(IsDrawing));
        OnPropertyChanged(nameof(IsNoteEditing));
        OnPropertyChanged(nameof(HasDraft));
        OnPropertyChanged(nameof(DraftKind));
        OnPropertyChanged(nameof(DraftPoints));
        OnPropertyChanged(nameof(PendingNumber));
        OnPropertyChanged(nameof(NoteText));
        OnPropertyChanged(nameof(StatusText));
        CommitNoteCommand.NotifyCanExecuteChanged();
        SkipNoteCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///     Tells the observers that the tool that is chosen changed.
    /// </summary>
    private void RaiseToolFlags()
    {
        OnPropertyChanged(nameof(CurrentTool));
        OnPropertyChanged(nameof(IsToolActive));
        OnPropertyChanged(nameof(IsPointerSelected));
        OnPropertyChanged(nameof(IsRectangleSelected));
        OnPropertyChanged(nameof(IsEllipseSelected));
        OnPropertyChanged(nameof(IsFreehandSelected));
        OnPropertyChanged(nameof(IsArrowSelected));
    }

    /// <summary>
    ///     Applies a change of a tool's check box.
    /// </summary>
    /// <param name="tool">The tool of the check box.</param>
    /// <param name="isChecked">Whether the check box is now checked.</param>
    private void SetToolFlag(AnnotationTool tool, bool isChecked)
    {
        if (isChecked)
        {
            SelectTool(tool);
        }
        else if (_currentTool == tool && tool != AnnotationTool.None)
        {
            SelectTool(AnnotationTool.None);
        }
        else
        {
            // The pointer cannot be turned off, and a tool that is not chosen cannot be unchecked: show the truth again
            RaiseToolFlags();
        }
    }

    /// <summary>
    ///     Brings everything up to date after the picture or what the host allows changed. A shape that is under
    ///     construction cannot go on without a picture or without permission; the chosen tool is kept while only the
    ///     picture is missing, because the picture is gone only for as long as the next file is drawn, and is given up
    ///     when the host does not allow marks.
    /// </summary>
    private void AfterAvailabilityChanged()
    {
        if (!ToolsEnabled)
        {
            Cancel();
            _keyboardCursor = null;
            OnPropertyChanged(nameof(KeyboardCursor));
            if (!_isDrawingAllowed && _currentTool != AnnotationTool.None)
            {
                SelectTool(AnnotationTool.None);
            }
        }

        OnPropertyChanged(nameof(StatusText));
    }

    /// <summary>
    ///     Reacts to a new picture: a shape that was started on another picture is thrown away, and the marks that are
    ///     shown are those of the new picture.
    /// </summary>
    /// <param name="sender">The source of the picture.</param>
    /// <param name="e">The event data.</param>
    private void OnPictureChanged(object? sender, EventArgs e)
    {
        var now = _pictures.CurrentImage;
        if (_stroke is not null && (now is null || !string.Equals(now.SnapshotKey, _stroke.SnapshotKey, StringComparison.Ordinal)))
        {
            Cancel();
        }

        _notice = string.Empty;
        OnPropertyChanged(nameof(HasPicture));
        OnPropertyChanged(nameof(PictureWidth));
        OnPropertyChanged(nameof(PictureHeight));
        OnPropertyChanged(nameof(ToolsEnabled));
        RefreshMarks();
        AfterAvailabilityChanged();
    }

    /// <summary>
    ///     Works out which marks are shown and how many are hidden.
    /// </summary>
    private void RefreshMarks()
    {
        var key = _pictures.CurrentImage?.SnapshotKey;
        _visibleMarks = key is null ? [] : _marks.VisibleMarks(_activeIds, key);
        _hiddenMarkCount = _marks.ActiveMarks(_activeIds).Count - _visibleMarks.Count;
        OnPropertyChanged(nameof(VisibleMarks));
        OnPropertyChanged(nameof(HiddenMarkCount));
        OnPropertyChanged(nameof(HasHiddenMarks));
        OnPropertyChanged(nameof(HiddenMarksText));
    }

    /// <summary>
    ///     Moves a point into the shown picture.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The nearest point inside the picture, or the point itself when no picture is shown.</returns>
    private RenderPoint ClampToPicture(RenderPoint point)
    {
        return _stroke is { } picture ? ClampToPicture(point, picture) : point;
    }

    /// <summary>
    ///     Moves a point into a picture.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="picture">The picture.</param>
    /// <returns>The nearest point inside the picture.</returns>
    private static RenderPoint ClampToPicture(RenderPoint point, RenderedImage picture)
    {
        return new RenderPoint(Math.Clamp(point.X, 0, picture.Width), Math.Clamp(point.Y, 0, picture.Height));
    }
}
