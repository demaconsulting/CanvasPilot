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

using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     The view model of the chat panel: the transcript of the messages that were sent, the message being composed, the
///     buttons that send it and that listen while they are held, and the joining of the marks drawn on the picture to the
///     message. It holds no rule of editing, sending, or dictation, which belong to the composer; it connects what the
///     composer and the annotation layer do to what the panel shows.
/// </summary>
/// <remarks>
///     <para>
///         <b>The transcript is only what was sent.</b> The panel appends one <see cref="TranscriptEntryViewModel" /> for
///         every <see cref="ComposerViewModel.MessageSent" />, with the immutable message, so the transcript never shows a
///         draft and nothing in it changes. It is kept in memory only: it is dropped when the project changes or closes, and
///         when the application ends.
///     </para>
///     <para>
///         <b>Send.</b> <see cref="SendCommand" /> is the composer's own command, so what makes sending possible (a project
///         is open, the message has content, nobody holds the talk button, nothing is being sent) is decided in one place.
///         When sending fails, the composer keeps the draft and says why in its status text, which the panel shows.
///     </para>
///     <para>
///         <b>Hold to talk.</b> <see cref="BeginPushToTalkAsync" /> and <see cref="EndPushToTalkAsync" /> follow the talk
///         button (and, later, the talk key) and hand the press and the release to the composer, which listens, shows the
///         words as they come in <see cref="ComposerViewModel.LivePartialText" />, and adds them as one block. When dictation
///         cannot work the composer says why and what to do in its status text and nothing changes in the draft; the panel
///         only adds a label for the button that follows the state.
///     </para>
///     <para>
///         <b>Marks.</b> The annotation layer and the composer do not know each other, because the composer already uses
///         the annotation subsystem. The panel joins them: every mark drawn becomes a chip in the message at the caret, as
///         one undo step, and each time the message changes the layer is told which mark chips it holds and whether marks
///         can be made, so that undoing the chip hides the mark and redoing it shows the mark again.
///     </para>
///     <para>
///         The class is owned by the user interface thread. It does not own the composer, the registry, or the layer; the
///         composition root does.
///     </para>
/// </remarks>
public sealed partial class ChatPanelViewModel : ObservableObject, IPushToTalkTarget, IDisposable
{
    /// <summary>
    ///     The label of the talk button while nothing listens.
    /// </summary>
    public const string TalkIdleText = "Hold to talk";

    /// <summary>
    ///     The text of the transcript when no message was sent yet.
    /// </summary>
    public const string EmptyTranscriptText = "Messages you send appear here.";

    /// <summary>
    ///     The composer of the message.
    /// </summary>
    private readonly ComposerViewModel _composer;

    /// <summary>
    ///     The layer on which the user marks the picture.
    /// </summary>
    private readonly AnnotationOverlayViewModel _annotation;

    /// <summary>
    ///     Speech, which is prepared ahead of the first press of the talk button, or <see langword="null" /> for none.
    /// </summary>
    private readonly IDictationService? _dictation;

    /// <summary>
    ///     Optional logger for diagnostics.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     The handler of the composer's property changes, kept so that it can be removed.
    /// </summary>
    private readonly PropertyChangedEventHandler _composerPropertyChanged;

    /// <summary>
    ///     The handler that tells the layer what the message holds, kept so that it can be removed.
    /// </summary>
    private readonly EventHandler _composerChanged;

    /// <summary>
    ///     The handler that adds the chip of every mark that is drawn, kept so that it can be removed.
    /// </summary>
    private readonly EventHandler<MarkAddedEventArgs> _markAdded;

    /// <summary>
    ///     The handler of the sent messages, kept so that it can be removed.
    /// </summary>
    private readonly EventHandler<MessageSentEventArgs> _messageSent;

    /// <summary>
    ///     The folder of the project the transcript belongs to, or <see langword="null" /> when none was open.
    /// </summary>
    private string? _transcriptProject;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatPanelViewModel" /> class and joins the composer and the
    ///     annotation layer.
    /// </summary>
    /// <param name="composer">The composer of the message.</param>
    /// <param name="marks">The marks made for the draft, from which the pictures of the mark chips are made.</param>
    /// <param name="annotation">The layer on which the user marks the picture.</param>
    /// <param name="markImages">Cuts the area around a mark out of its picture, for the picture of a mark chip.</param>
    /// <param name="dictation">Speech, which is prepared ahead of the first press of the talk button, or <see langword="null" /> for none.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument other than the dictation service and the logger is <see langword="null" />.</exception>
    public ChatPanelViewModel(
        ComposerViewModel composer,
        MarkRegistry marks,
        AnnotationOverlayViewModel annotation,
        IMarkImageComposer markImages,
        IDictationService? dictation = null,
        RollingFileLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(composer);
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentNullException.ThrowIfNull(markImages);

        _composer = composer;
        _annotation = annotation;
        _dictation = dictation;
        _logger = logger;
        Transcript = [];
        Thumbnails = new ChipThumbnailProvider(chip => CropOfDraftMark(marks, markImages, chip));
        _transcriptProject = composer.ProjectFolder;

        // The panel follows the composer: what depends on it is announced again, a changed project drops the transcript,
        // and a sent message joins the transcript
        _composerPropertyChanged = OnComposerPropertyChanged;
        _messageSent = (_, e) => AppendEntry(e.Message);
        _composerChanged = (_, _) => JoinAnnotationToComposer();
        _markAdded = (_, e) => _composer.AddMark(e.Mark);
        _composer.PropertyChanged += _composerPropertyChanged;
        _composer.MessageSent += _messageSent;
        _composer.Changed += _composerChanged;
        _annotation.MarkAdded += _markAdded;
        JoinAnnotationToComposer();
    }

    /// <summary>
    ///     Gets the composer of the message, which the panel's editing control shows.
    /// </summary>
    public ComposerViewModel Composer => _composer;

    /// <summary>
    ///     Gets the messages that were sent, oldest first. The panel adds to it; nothing else does.
    /// </summary>
    public ObservableCollection<TranscriptEntryViewModel> Transcript { get; }

    /// <summary>
    ///     Gets a value indicating whether any message was sent in the open project.
    /// </summary>
    public bool HasTranscript => Transcript.Count > 0;

    /// <summary>
    ///     Gets the provider of the small pictures of the chips of the message being composed.
    /// </summary>
    public IChipThumbnailProvider Thumbnails { get; }

    /// <summary>
    ///     Gets the command that sends the message, which is the composer's.
    /// </summary>
    public IAsyncRelayCommand SendCommand => _composer.SendCommand;

    /// <summary>
    ///     Gets a value indicating whether the message can be sent now.
    /// </summary>
    public bool CanSend => _composer.CanSend;

    /// <summary>
    ///     Gets a value indicating whether the talk button can be used: a message can be written.
    /// </summary>
    public bool CanTalk => _composer.CanEdit;

    /// <summary>
    ///     Gets a value indicating whether the talk button is held and the application is listening, or starting to, or
    ///     finishing.
    /// </summary>
    public bool IsTalking => _composer.IsDictating;

    /// <summary>
    ///     Gets the label of the talk button, which says what the button is doing.
    /// </summary>
    public string TalkText => _composer.DictationState switch
    {
        DictationState.Starting => "Starting... keep holding",
        DictationState.Listening => "Listening... let go to finish",
        DictationState.Finishing => "Finishing...",
        _ => TalkIdleText,
    };

    /// <summary>
    ///     Gets or sets what shows the payload inspector. While it is <see langword="null" /> the "View payload" commands
    ///     do nothing. The window sets it when the panel is placed in it.
    /// </summary>
    public IPayloadInspectorLauncher? InspectorLauncher { get; set; }

    /// <summary>
    ///     Starts listening because the talk button was pressed. The composer decides whether dictation can work and, when
    ///     it cannot, says why and what to do in its status text, which the panel shows; it never throws for that.
    /// </summary>
    /// <returns>A task that completes when listening has started or has been found impossible.</returns>
    public Task BeginPushToTalkAsync() => _composer.BeginPushToTalkAsync();

    /// <summary>
    ///     Stops listening because the talk button was released and adds what was said to the message as one block. A
    ///     release when nothing listens is ignored.
    /// </summary>
    /// <returns>A task that completes when what was said has been added to the message.</returns>
    public Task EndPushToTalkAsync() => _composer.EndPushToTalkAsync();

    /// <summary>
    ///     Loads the speech engine ahead of the first press of the talk button, so that the first press does not wait for
    ///     it. The panel calls it when it is shown. Nothing is loaded when dictation is not available, and a failure is
    ///     logged, never thrown.
    /// </summary>
    /// <returns>A task that completes when the preparation has finished.</returns>
    public async Task PrepareDictationAsync()
    {
        if (_dictation is null)
        {
            return;
        }

        try
        {
            await _dictation.PrepareAsync();
        }
        catch (OperationCanceledException)
        {
            // Preparing was given up; the first press loads the engine instead
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger?.Log(LogLevel.Warning, $"Preparing dictation failed: {ex.Message}");
        }
    }

    /// <summary>
    ///     Stops following the composer and the annotation layer and releases the pictures of the transcript and the
    ///     draft's chips. It does not dispose the composer, the registry, or the layer.
    /// </summary>
    public void Dispose()
    {
        _composer.PropertyChanged -= _composerPropertyChanged;
        _composer.MessageSent -= _messageSent;
        _composer.Changed -= _composerChanged;
        _annotation.MarkAdded -= _markAdded;
        ClearTranscript();
        if (Thumbnails is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    ///     Cuts the area around a mark of the draft out of its picture, for the picture of the mark's chip.
    /// </summary>
    /// <param name="marks">The marks of the draft.</param>
    /// <param name="markImages">Cuts the area out.</param>
    /// <param name="chip">The chip of the mark.</param>
    /// <returns>The PNG bytes of the area, or <see langword="null" /> when the registry has no such mark.</returns>
    private static byte[]? CropOfDraftMark(MarkRegistry marks, IMarkImageComposer markImages, MarkChip chip)
    {
        var mark = marks.Find(chip.MarkId);
        if (mark is null)
        {
            return null;
        }

        var images = markImages.Compose(mark.Snapshot, [mark]);
        return images.Crops.Length == 0 ? null : images.Crops[0].Png.ToArray();
    }

    /// <summary>
    ///     Tells the layer which mark chips the message holds and whether marks can be made, so that the layer shows
    ///     exactly the marks of the message.
    /// </summary>
    private void JoinAnnotationToComposer()
    {
        _annotation.IsDrawingAllowed = _composer.CanEdit;
        _annotation.SetActiveMarkIds(_composer.MarkChipIds);
    }

    /// <summary>
    ///     Announces what the panel shows from the composer and drops the transcript when the project changed.
    /// </summary>
    /// <param name="sender">The composer.</param>
    /// <param name="e">The name of the property that changed.</param>
    private void OnComposerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ComposerViewModel.CanSend):
                OnPropertyChanged(nameof(CanSend));
                break;

            case nameof(ComposerViewModel.CanEdit):
            case nameof(ComposerViewModel.IsSending):
                // Sending takes the message away from the user for a moment, which also stops the talk button
                OnPropertyChanged(nameof(CanTalk));
                OnPropertyChanged(nameof(CanSend));
                break;

            case nameof(ComposerViewModel.IsDictating):
                OnPropertyChanged(nameof(IsTalking));
                break;

            case nameof(ComposerViewModel.DictationState):
                OnPropertyChanged(nameof(TalkText));
                break;

            case nameof(ComposerViewModel.ProjectFolder):
                // Another project is another conversation: the transcript is only in memory and belongs to its project
                if (!string.Equals(_transcriptProject, _composer.ProjectFolder, StringComparison.OrdinalIgnoreCase))
                {
                    _transcriptProject = _composer.ProjectFolder;
                    ClearTranscript();
                }

                break;
        }
    }

    /// <summary>
    ///     Adds a sent message to the end of the transcript.
    /// </summary>
    /// <param name="message">The message.</param>
    private void AppendEntry(SentMessage message)
    {
        Transcript.Add(new TranscriptEntryViewModel(message, _composer.ProjectFolder, () => InspectorLauncher));
        OnPropertyChanged(nameof(HasTranscript));
    }

    /// <summary>
    ///     Removes every message from the transcript and releases their pictures.
    /// </summary>
    private void ClearTranscript()
    {
        foreach (var entry in Transcript)
        {
            entry.Dispose();
        }

        Transcript.Clear();
        OnPropertyChanged(nameof(HasTranscript));
    }
}
