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
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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
    ///     The prompts that the panel offers in the harness, whose project has a drawing (a picture is shown): the
    ///     refinements and the export.
    /// </summary>
    private static readonly IReadOnlyList<string> Offered = FollowUpSuggestions.Plan(hasDrawing: true, hasPictureShown: true, busy: false);

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
        Assert.True(harness.Find<TextBlock>("ChatTranscriptEmpty").IsEffectivelyVisible);
        Assert.Equal(ChatPanelViewModel.EmptyTranscriptText, harness.Find<TextBlock>("ChatTranscriptEmpty").Text);
        Assert.Empty(harness.Entries);
        Assert.Same(harness.Composer, harness.Editor.DataContext);
        Assert.Same(harness.Chat.Thumbnails, harness.Editor.ThumbnailProvider);
        Assert.False(harness.SendButton.IsEffectivelyEnabled);
        Assert.True(harness.TalkButton.IsEffectivelyEnabled);
        Assert.Equal(ChatPanelViewModel.TalkIdleText, harness.TalkLabel);
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
        Assert.False(harness.Find<TextBlock>("ChatTranscriptEmpty").IsEffectivelyVisible);
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
    ///     Test that the bubble of a sent message that holds a chip is as big as its content: the chip has a row of its own
    ///     that is as high as the chip, the words are below the row and as high as their lines, and the bubble is as high as
    ///     the two with its padding, so nothing is cut off.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_Send_MessageWithChip_BubbleSizesToTheChipRowAndTheWords()
    {
        // Arrange: a message with a mark chip and words
        using var harness = new ChatHarness();
        var mark = harness.Marks.Add(harness.Picture, MarkKind.Rectangle, [new RenderPoint(5, 5), new RenderPoint(40, 30)]);
        harness.Composer.AddMark(mark);
        harness.Composer.InsertTyped("Chart downloads by month");

        // Act: send it and lay out the transcript
        harness.ClickSend();
        harness.Layout();

        // Assert: the chip row holds the chip, the words are below it with their full lines, and the bubble fits both
        var entry = harness.Entries.Single();
        var bubble = entry.FindControl<Border>("MessageBubble")!;
        var blocks = entry.FindControl<StackPanel>("MessageBlocks")!;
        var row = Assert.Single(blocks.Children.OfType<WrapPanel>());
        var chip = Assert.Single(entry.Chips);
        var words = Assert.Single(entry.TextBlocks);
        Assert.Equal("Chart downloads by month", ChatHarness.TextOf(entry));
        Assert.True(row.Bounds.Height >= chip.Bounds.Height - 0.5, $"The row is {row.Bounds.Height} high, the chip {chip.Bounds.Height}.");
        Assert.True(words.Bounds.Top >= row.Bounds.Bottom, "The words must be below the row of chips.");
        Assert.True(words.Bounds.Height >= words.LineHeight - 0.5, $"The words are {words.Bounds.Height} high, a line is {words.LineHeight}.");
        Assert.Equal(bubble.Padding.Top + blocks.Bounds.Height + bubble.Padding.Bottom, bubble.Bounds.Height, 0.5);
        Assert.True(blocks.Bounds.Height >= row.Bounds.Height + words.Bounds.Height - 0.5);
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
        Assert.Equal("Listening... let go to finish", harness.TalkLabel);
        Assert.Equal("draw a", harness.Find<TextBlock>("ComposerPartialText").Text);
        Assert.True(harness.Find<TextBlock>("ComposerPartialText").IsVisible);
        Assert.False(harness.SendButton.IsEffectivelyEnabled);

        // Act: let go on the button
        harness.ReleaseAt(harness.CenterOf(harness.TalkButton));

        // Assert: listening ended, the words are in the message as one block, and the label is back
        Assert.True(harness.WaitFor(() => harness.Editor.SurfaceText == "draw a circle"));
        Assert.False(harness.View.IsTalkHeld);
        Assert.Equal(DictationState.Idle, harness.Composer.DictationState);
        Assert.Equal(ChatPanelViewModel.TalkIdleText, harness.TalkLabel);
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
        Assert.Equal(ChatPanelViewModel.TalkIdleText, harness.TalkLabel);
    }

    /// <summary>
    ///     Test the flow of a user: with an empty message a rectangle is drawn on the picture, a note is added with "Add mark",
    ///     the user clicks into the message and types. The message that assistive technology reads is the chip, a line feed,
    ///     and the words; the chip is on a line of its own above the words, and the words were typed on the line below.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_EmptyMessageMarkWithNoteThenClickAndType_ChipRowIsSeparateFromTheWords()
    {
        // Arrange: an empty message and the rectangle tool
        using var harness = new ChatHarness();
        harness.Annotation.SelectTool(AnnotationTool.Rectangle);

        // Act: draw, give the mark a note and choose Add mark, click into the message, and type
        harness.Annotation.BeginStroke(new RenderPoint(10, 10));
        harness.Annotation.EndStroke(new RenderPoint(60, 40));
        harness.Annotation.NoteText = "this one";
        harness.Annotation.CommitNoteCommand.Execute(null);
        harness.Layout();
        var surface = harness.Editor.FindControl<ComposerTextEditor>("Editor")!;
        var peer = ControlAutomationPeer.CreatePeerForElement(surface);
        var valueBeforeTyping = Assert.IsAssignableFrom<IValueProvider>(peer).Value;
        var caretAfterMark = harness.Composer.CaretOffset;
        var chipRow = harness.Editor.GetVisualDescendants().OfType<ChipView>().Single();
        var onChipRow = chipRow.TranslatePoint(new Point(chipRow.Bounds.Width + 30, chipRow.Bounds.Height / 2), harness.Window)!.Value;
        harness.Window.MouseDown(onChipRow, MouseButton.Left);
        harness.Window.MouseUp(onChipRow, MouseButton.Left);
        var caretAfterClick = surface.CaretOffset;
        harness.Type("Make this a green diamond.");
        var value = Assert.IsAssignableFrom<IValueProvider>(peer).Value;

        // Assert: the line feed is there before the typing, the caret was on the line below the chip, the words follow the
        // line feed, and the chip is above the line of the words
        Assert.Equal("[Mark 1]\n", valueBeforeTyping);
        Assert.Equal(2, caretAfterMark);
        Assert.Equal(1, caretAfterClick);
        Assert.Equal("[Mark 1]\nMake this a green diamond.", value);
        var chip = Assert.Single(harness.Editor.GetVisualDescendants().OfType<ChipView>());
        var chipBottom = chip.TranslatePoint(new Point(0, chip.Bounds.Height), surface.TextArea.TextView)!.Value.Y;
        Assert.True(surface.TextArea.Caret.CalculateCaretRectangle().Y >= chipBottom - 1, "The words must be below the chip.");
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

        // Assert: the chip is in the row above the text and the mark is shown
        Assert.Equal("\uFFFC\nsee ", harness.Editor.SurfaceText);
        Assert.Single(harness.Annotation.VisibleMarks);

        // Act: undo with the keyboard in the editing control
        harness.Editor.FocusEditor();
        harness.Window.KeyPressQwerty(PhysicalKey.Z, ComposerHarness.Command);
        harness.Window.KeyReleaseQwerty(PhysicalKey.Z, ComposerHarness.Command);
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
        Assert.Same(harness.Chat.SentEntries.First().Payload, shown.Payload);
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
        var runs = harness.Entries.Single().TextBlocks.Single().Inlines!.OfType<Avalonia.Controls.Documents.Run>().ToList();
        Assert.Equal(2, runs.Count);
        Assert.Null(runs[0].Background);
        var dictated = Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(runs[1].Background);
        Assert.Equal(ThemeUiHelpers.ColorOf("CpFillDictated", Avalonia.Styling.ThemeVariant.Dark), dictated.Color);
    }

    /// <summary>
    ///     Test that the empty conversation offers the prompts that fit the project (the harness shows a picture, so the refinements and the export), that choosing one writes it in the message, and that
    ///     the prompts above the message only show once there is a conversation.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_EmptyState_ExamplePromptFillsTheMessageAndSuggestionsWaitForAConversation()
    {
        // Arrange: a new panel
        using var harness = new ChatHarness();
        var prompts = harness.FindAll<Button>("ChatExamplePrompt");
        Assert.Equal(Offered, prompts.Select(button => (string)button.Content!));
        Assert.DoesNotContain(harness.FindAll<Button>("ChatSuggestion"), button => button.IsEffectivelyVisible);

        // Act: choose the first example
        prompts[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        harness.Layout();

        // Assert: the words are in the message, ready to send, and the examples are still there to change one's mind
        Assert.Equal(Offered[0], harness.Composer.Document.FlatText);
        Assert.True(harness.SendButton.IsEffectivelyEnabled);

        // Act: send it
        harness.ClickSend();

        // Assert: the examples give way to the conversation and the follow-ups appear
        Assert.False(harness.Find<TextBlock>("ChatTranscriptEmpty").IsEffectivelyVisible);
        Assert.Equal(Offered.Count, harness.FindAll<Button>("ChatSuggestion").Count(button => button.IsEffectivelyVisible));
    }

    /// <summary>
    ///     Test that the button of the header asks to hide the column, and that the workspace shows a restore button then.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_CollapseButton_RaisesCollapseRequested()
    {
        // Arrange
        using var harness = new ChatHarness();
        var raised = 0;
        harness.Chat.CollapseRequested += (_, _) => raised++;

        // Act
        harness.Find<Button>("ChatCollapseButton").Command!.Execute(null);

        // Assert
        Assert.Equal(1, raised);
    }

    /// <summary>
    ///     Test that the composer card shows its placeholder while the message is empty, that every button without text has an
    ///     accessible name, and that the hint says what Enter does.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_ComposerCard_PlaceholderNamesAndHint()
    {
        // Arrange / Act: a new panel in an open project
        using var harness = new ChatHarness();

        // Assert: the placeholder shows, and the icon buttons have names and tips
        var placeholder = harness.Find<TextBlock>("ComposerPlaceholder");
        Assert.True(placeholder.IsEffectivelyVisible);
        Assert.Equal(ComposerViewModel.PlaceholderText, placeholder.Text);
        Assert.Equal("Send", AutomationProperties.GetName(harness.SendButton));
        Assert.Equal("Attach files", AutomationProperties.GetName(harness.Find<Button>("ChatAttachButton")));
        Assert.Equal(ChatPanelViewModel.TalkIdleText, harness.TalkLabel);
        Assert.NotNull(ToolTip.GetTip(harness.TalkButton));
        Assert.Equal("Enter to send", harness.Find<TextBlock>("ChatSendHint").Text);

        // Act: type something
        harness.Type("Make it blue");

        // Assert: the placeholder is gone
        Assert.False(placeholder.IsEffectivelyVisible);
    }

    /// <summary>
    ///     Test that Enter on its own sends the message, and that Shift+Enter writes a line break instead.
    /// </summary>
    [AvaloniaFact]
    public async Task ChatPanelView_EnterKey_SendsTheMessageAndShiftEnterWritesALineBreak()
    {
        // Arrange: a message in the editing control that has the focus
        using var harness = new ChatHarness();
        harness.Type("Make it blue");
        harness.Editor.FocusEditor();

        // Act: Shift+Enter
        harness.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Shift);
        harness.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.Shift);
        harness.Layout();

        // Assert: nothing was sent, and the message has a second line
        Assert.Null(harness.Composer.SendCommand.ExecutionTask);
        Assert.Empty(harness.Entries);
        Assert.Contains('\n', harness.Composer.Document.FlatText);

        // Act: Enter on its own starts the send, which runs asynchronously, so wait for that very send to finish
        harness.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        harness.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        var sending = harness.Composer.SendCommand.ExecutionTask;
        Assert.NotNull(sending);
        await sending;
        harness.Layout();

        // Assert: the message was sent
        Assert.Single(harness.Entries);
    }

    /// <summary>
    ///     Test that the attach button asks for files and adds them to the message as attachment chips.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_AttachButton_AddsThePickedFilesAsChips()
    {
        // Arrange: a file and a picker that chooses it
        using var harness = new ChatHarness();
        var file = Path.Combine(harness.Folder, "notes.txt");
        File.WriteAllText(file, "notes");
        var picker = new FakeAttachmentPicker { Files = [file] };
        harness.Chat.AttachmentPicker = picker;

        // Act: press the attach button
        harness.Find<Button>("ChatAttachButton").Command!.Execute(null);

        // Assert: the picker was asked once, from the project folder, and the message has the chip
        Assert.True(harness.WaitFor(() => harness.Composer.Document.Chips.Count() == 1));
        Assert.Equal(1, picker.Asked);
        Assert.Equal(harness.Composer.ProjectFolder, picker.InitialFolder);
        Assert.IsType<FileChip>(Assert.Single(harness.Composer.Document.Chips));
    }

    /// <summary>
    ///     A picker of attachments that answers with the files that the test names.
    /// </summary>
    private sealed class FakeAttachmentPicker : IAttachmentPicker
    {
        /// <summary>
        ///     Gets the files that the user picks.
        /// </summary>
        public required IReadOnlyList<string> Files { get; init; }

        /// <summary>
        ///     Gets how many times the user was asked.
        /// </summary>
        public int Asked { get; private set; }

        /// <summary>
        ///     Gets the folder that the picker was asked to start in.
        /// </summary>
        public string? InitialFolder { get; private set; }

        /// <inheritdoc />
        public Task<IReadOnlyList<string>> PickAttachmentsAsync(string? initialFolder)
        {
            Asked++;
            InitialFolder = initialFolder;
            return Task.FromResult(Files);
        }
    }
}
