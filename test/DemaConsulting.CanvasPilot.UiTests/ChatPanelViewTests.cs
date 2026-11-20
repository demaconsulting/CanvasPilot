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
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for <see cref="ChatPanelView" /> and <see cref="TranscriptEntryView" />: the real panel, the
///     real composer and editing control, the real annotation layer, and the real transcript views in a window, driven by
///     the pointer and the keyboard as a user does. Dictation, the builder of sent messages, and the pictures of marks
///     are replaced.
/// </summary>
public sealed class ChatPanelViewTests
{
    /// <summary>
    ///     Test that a new panel shows a hint instead of a transcript, the editing control, a send button that cannot be
    ///     used yet, and a talk button that can.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_New_ShowsEmptyTranscriptHintComposerAndButtons()
    {
        // Arrange / Act: a panel in an open project
        using var harness = new ChatHarness();

        // Assert: the hint says where messages appear, there are no entries, the editing control is bound to the
        // composer, the send button is disabled, and the talk button shows its first label
        Assert.True(harness.Find<TextBlock>("ChatTranscriptEmpty").IsVisible);
        Assert.Equal(ChatPanelViewModel.EmptyTranscriptText, harness.Find<TextBlock>("ChatTranscriptEmpty").Text);
        Assert.Empty(harness.Entries);
        Assert.Same(harness.Composer, harness.Editor.DataContext);
        Assert.Same(harness.Chat.Thumbnails, harness.Editor.ThumbnailProvider);
        Assert.False(harness.SendButton.IsEffectivelyEnabled);
        Assert.True(harness.TalkButton.IsEffectivelyEnabled);
        Assert.Equal(ChatPanelViewModel.TalkIdleText, harness.TalkButton.Content);
    }

    /// <summary>
    ///     Test that the panel asks for dictation to be prepared when it is shown.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_Shown_PreparesDictation()
    {
        // Arrange / Act: show a panel
        using var harness = new ChatHarness();

        // Assert: the speech engine was prepared ahead of the first press
        Assert.Equal(1, harness.Dictation.PrepareCount);
    }

    /// <summary>
    ///     Test that without a project the panel says that a project must be open and neither sends nor listens.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_NoProject_SaysSoAndDisablesSendAndTalk()
    {
        // Arrange / Act: a panel with no project open
        using var harness = new ChatHarness(openProject: false);

        // Assert: the editing control gives the hint, and both buttons are disabled
        Assert.True(harness.Find<TextBlock>("ComposerNoProjectHint").IsVisible);
        Assert.Equal(ComposerViewModel.NoProjectText, harness.Find<TextBlock>("ComposerNoProjectHint").Text);
        Assert.False(harness.SendButton.IsEffectivelyEnabled);
        Assert.False(harness.TalkButton.IsEffectivelyEnabled);
    }

    /// <summary>
    ///     Test that the send button follows the message and that sending puts the message into the transcript and empties
    ///     the editing control.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_Send_TypedText_AppearsInTranscriptAndEmptiesTheEditor()
    {
        // Arrange: a typed message
        using var harness = new ChatHarness();
        harness.Type("Make it blue");
        Assert.True(harness.SendButton.IsEffectivelyEnabled);

        // Act: press the send button
        harness.ClickSend();

        // Assert: the transcript shows the message, the hint is gone, the editing control is empty, and Send is disabled
        var entry = Assert.Single(harness.Entries);
        Assert.Equal("Make it blue", ChatHarness.TextOf(entry));
        Assert.False(harness.Find<TextBlock>("ChatTranscriptEmpty").IsVisible);
        Assert.Equal(string.Empty, harness.Editor.SurfaceText);
        Assert.False(harness.SendButton.IsEffectivelyEnabled);
        Assert.False(harness.Find<TextBlock>("TranscriptSummary").IsVisible);
    }

    /// <summary>
    ///     Test that a sent message with a mark and an attached file shows both chips in their places in the line, with
    ///     their names and without a delete button, and says what it carried.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_Send_MessageWithMarkAndFile_ShowsChipsInTranscriptWithoutDeleteButtons()
    {
        // Arrange: a message with a mark at the start, text, and an attached file
        using var harness = new ChatHarness();
        var mark = harness.Marks.Add(harness.Picture, MarkKind.Rectangle, [new RenderPoint(5, 5), new RenderPoint(40, 30)]);
        harness.Composer.AddMark(mark);
        harness.Composer.InsertTyped(" and ");
        var file = Path.Combine(harness.Folder, "notes.txt");
        File.WriteAllText(file, "notes");
        harness.Composer.HandleDrop(new DroppedContent { Paths = [file] });

        // Act: send it
        harness.ClickSend();

        // Assert: both chips are in the transcript with their names and no delete button, and the summary names them
        var entry = Assert.Single(harness.Entries);
        var chips = entry.Chips;
        Assert.Equal(2, chips.Count);
        Assert.Equal("Mark 1", chips[0].FindControl<TextBlock>("LabelText")!.Text);
        Assert.Equal("notes.txt", chips[1].FindControl<TextBlock>("LabelText")!.Text);
        Assert.All(chips, chip => Assert.False(chip.IsDeleteButtonVisible));
        Assert.Equal("1 mark, 1 attachment", harness.Find<TextBlock>("TranscriptSummary").Text);
        Assert.True(File.Exists(Path.Combine(harness.Project, "attachments", "notes.txt")));
    }

    /// <summary>
    ///     Test that the chip of a mark in the message being written shows the picture of the area around the mark.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_MarkChipInDraft_ShowsThePictureAroundTheMark()
    {
        // Arrange / Act: a mark is added to the message and the chips are drawn
        using var harness = new ChatHarness();
        var mark = harness.Marks.Add(harness.Picture, MarkKind.Rectangle, [new RenderPoint(5, 5), new RenderPoint(40, 30)]);
        harness.Composer.AddMark(mark);
        harness.Layout();

        // Assert: the chip gets its picture from the provider of the panel, which cuts it from the mark's picture
        Assert.True(harness.WaitFor(() => harness.Find<Image>("ComposerChipThumbnail").IsVisible));
        Assert.NotNull(harness.Find<Image>("ComposerChipThumbnail").Source);
    }

    /// <summary>
    ///     Test that holding the pointer on the talk button listens, shows the words as they come, and that letting go adds
    ///     them to the message as one dictated block and restores the label.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_HoldToTalk_PressSpeakRelease_ListensShowsWordsAndAddsOneBlock()
    {
        // Arrange: speech that ends in a sentence
        using var harness = new ChatHarness();
        harness.Dictation.Session.FinalText = "draw a circle";

        // Act: press the talk button and speak
        harness.PressOn(harness.TalkButton);
        harness.Dictation.Session.Say("draw a");
        harness.Layout();

        // Assert: it is listening, the label says so, and the words so far are shown
        Assert.True(harness.View.IsTalkHeld);
        Assert.Equal(DictationState.Listening, harness.Composer.DictationState);
        Assert.Equal("Listening... let go to finish", harness.TalkButton.Content);
        Assert.Equal("draw a", harness.Find<TextBlock>("ComposerPartialText").Text);
        Assert.True(harness.Find<TextBlock>("ComposerPartialText").IsVisible);
        Assert.False(harness.SendButton.IsEffectivelyEnabled);

        // Act: let go on the button
        harness.ReleaseAt(harness.CenterOf(harness.TalkButton));

        // Assert: listening ended, the words are in the message as one block, and the label is back
        Assert.True(harness.WaitFor(() => harness.Editor.SurfaceText == "draw a circle"));
        Assert.False(harness.View.IsTalkHeld);
        Assert.Equal(DictationState.Idle, harness.Composer.DictationState);
        Assert.Equal(ChatPanelViewModel.TalkIdleText, harness.TalkButton.Content);
        Assert.Equal(1, harness.Dictation.Session.EndCount);
        Assert.True(harness.SendButton.IsEffectivelyEnabled);
    }

    /// <summary>
    ///     Test that letting go of the pointer anywhere, not only on the button, ends listening.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_HoldToTalk_ReleaseOutsideTheButton_StillEndsListening()
    {
        // Arrange: the pointer is pressed on the talk button
        using var harness = new ChatHarness();
        harness.Dictation.Session.FinalText = "hello";
        harness.PressOn(harness.TalkButton);
        Assert.Equal(DictationState.Listening, harness.Composer.DictationState);

        // Act: move away from the button, and let go over the transcript
        harness.Window.MouseMove(new Point(20, 20), RawInputModifiers.LeftMouseButton);
        harness.ReleaseAt(new Point(20, 20));

        // Assert: listening ended and the words were added
        Assert.True(harness.WaitFor(() => harness.Editor.SurfaceText == "hello"));
        Assert.Equal(DictationState.Idle, harness.Composer.DictationState);
        Assert.False(harness.View.IsTalkHeld);
    }

    /// <summary>
    ///     Test that losing the pointer capture, as when a dialog opens or the window is deactivated, counts as letting go.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_HoldToTalk_PointerCaptureLost_EndsListening()
    {
        // Arrange: the pointer is pressed on the talk button
        using var harness = new ChatHarness();
        harness.Dictation.Session.FinalText = "words";
        harness.PressOn(harness.TalkButton);
        Assert.True(harness.View.IsTalkHeld);

        // Act: the button loses the pointer capture
        harness.TalkButton.RaiseEvent(new PointerCaptureLostEventArgs(harness.TalkButton, new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true)));
        harness.Layout();

        // Assert: it counts as letting go
        Assert.True(harness.WaitFor(() => harness.Editor.SurfaceText == "words"));
        Assert.False(harness.View.IsTalkHeld);
    }

    /// <summary>
    ///     Test that the space key held on the focused talk button listens in the same way, so the button can be used
    ///     without a pointer.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_HoldToTalk_SpaceKeyHeldOnFocusedButton_ListensUntilReleased()
    {
        // Arrange: the focused talk button
        using var harness = new ChatHarness();
        harness.Dictation.Session.FinalText = "spoken";
        harness.TalkButton.Focus();
        harness.Layout();

        // Act: hold the space key, then let go
        harness.Window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        harness.Layout();
        var listeningWhileHeld = harness.Composer.DictationState;
        harness.Window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);

        // Assert: it listened while the key was down and added the words when it was released
        Assert.Equal(DictationState.Listening, listeningWhileHeld);
        Assert.True(harness.WaitFor(() => harness.Editor.SurfaceText == "spoken"));
    }

    /// <summary>
    ///     Test that a panel that is taken out of the window while the button is held lets go, so that the microphone is not
    ///     left open.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_HoldToTalk_PanelRemovedWhileHeld_EndsListening()
    {
        // Arrange: listening while the button is held
        using var harness = new ChatHarness();
        harness.PressOn(harness.TalkButton);
        Assert.Equal(DictationState.Listening, harness.Composer.DictationState);

        // Act: the panel leaves the window
        harness.Window.Content = null;
        harness.Layout();

        // Assert: listening ended
        Assert.True(harness.WaitFor(() => harness.Composer.DictationState == DictationState.Idle));
        Assert.Equal(1, harness.Dictation.Session.EndCount);
    }

    /// <summary>
    ///     Test that when dictation cannot work, a press says why and what to do in the panel and changes nothing.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_HoldToTalk_NoModelChosen_ShowsReasonInPanelAndChangesNothing()
    {
        // Arrange: dictation that is not available because no model is chosen
        using var harness = new ChatHarness();
        const string reason = "No speech model is chosen. Choose one on the Speech page of the settings.";
        harness.Dictation.Availability = new DictationAvailability(DictationAvailabilityReason.NoModelChosen, reason);
        harness.Type("typed");

        // Act: press and release the talk button
        harness.PressOn(harness.TalkButton);
        harness.ReleaseAt(harness.CenterOf(harness.TalkButton));

        // Assert: the reason is shown in the panel's status line, nothing listened, and the message is unchanged
        Assert.Equal(reason, harness.Find<TextBlock>("ComposerStatus").Text);
        Assert.True(harness.Find<TextBlock>("ComposerStatus").IsVisible);
        Assert.Equal(0, harness.Dictation.BeginCount);
        Assert.Equal("typed", harness.Editor.SurfaceText);
        Assert.Equal(ChatPanelViewModel.TalkIdleText, harness.TalkButton.Content);
    }

    /// <summary>
    ///     Test that a mark drawn on the picture becomes a chip in the message through the panel and that the layer shows
    ///     the mark only while its chip is in the message.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_MarkDrawn_ChipAppearsInTheEditorAndUndoRemovesIt()
    {
        // Arrange: some typed text and the rectangle tool
        using var harness = new ChatHarness();
        harness.Type("see ");
        harness.Annotation.SelectTool(AnnotationTool.Rectangle);

        // Act: draw a rectangle on the picture
        harness.Annotation.BeginStroke(new RenderPoint(10, 10));
        harness.Annotation.EndStroke(new RenderPoint(60, 40));
        harness.Annotation.SkipNote();
        harness.Layout();

        // Assert: the chip is in the message after the text and the mark is shown
        Assert.Equal("see \uFFFC", harness.Editor.SurfaceText);
        Assert.Single(harness.Annotation.VisibleMarks);

        // Act: undo with the keyboard in the editing control
        harness.Editor.FocusEditor();
        harness.Window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
        harness.Window.KeyReleaseQwerty(PhysicalKey.Z, RawInputModifiers.Control);
        harness.Layout();

        // Assert: the chip is gone and the mark is no longer shown
        Assert.Equal("see ", harness.Editor.SurfaceText);
        Assert.Empty(harness.Annotation.VisibleMarks);
    }

    /// <summary>
    ///     Test that the "View payload" button of a sent message asks for the inspector of exactly that message's payload.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_ViewPayloadButton_ShowsInspectorOfThatMessage()
    {
        // Arrange: a sent message and a launcher that records what it is asked to show
        using var harness = new ChatHarness();
        var launcher = new RecordingInspectorLauncher();
        harness.Chat.InspectorLauncher = launcher;
        harness.Type("look at this");
        harness.ClickSend();
        var button = harness.Find<Button>("TranscriptViewPayloadButton");

        // Act: press "View payload"
        SettingsUiHelpers.Click(harness.Window, button);
        harness.Layout();

        // Assert: the launcher got an inspector for the payload of that message
        var shown = Assert.Single(launcher.Shown);
        Assert.Same(harness.Chat.Transcript[0].Payload, shown.Payload);
        Assert.Contains("look at this", shown.Description);
    }

    /// <summary>
    ///     Test that the transcript lists several messages in the order they were sent.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_Send_TwoMessages_AreListedInOrder()
    {
        // Arrange / Act: send two messages
        using var harness = new ChatHarness();
        harness.Type("first");
        harness.ClickSend();
        harness.Type("second");
        harness.ClickSend();

        // Assert: the transcript shows them in the order they were sent
        Assert.Equal(["first", "second"], harness.Entries.Select(ChatHarness.TextOf));
    }

    /// <summary>
    ///     Test that dictated words in a sent message are shown with the same background as in the message being written,
    ///     and typed words are not.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_Send_DictatedWords_AreShownWithTheDictatedBackground()
    {
        // Arrange: a message with typed and dictated words
        using var harness = new ChatHarness();
        harness.Type("typed ");
        harness.Dictation.Session.FinalText = "dictated";
        harness.PressOn(harness.TalkButton);
        harness.ReleaseAt(harness.CenterOf(harness.TalkButton));
        Assert.True(harness.WaitFor(() => harness.Editor.SurfaceText == "typed dictated"));

        // Act: send it
        harness.ClickSend();

        // Assert: the transcript text has a typed run without a background and a dictated run with one
        var runs = harness.Entries.Single().FindControl<SelectableTextBlock>("MessageText")!.Inlines!.OfType<Avalonia.Controls.Documents.Run>().ToList();
        Assert.Equal(2, runs.Count);
        Assert.Null(runs[0].Background);
        Assert.NotNull(runs[1].Background);
    }
}
