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

using DemaConsulting.CanvasPilot.KnowledgeSubsystem;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     Where a reply of the AI copilot is in its life.
/// </summary>
public enum AssistantReplyState
{
    /// <summary>The text is still being written; it grows as pieces arrive.</summary>
    Streaming,

    /// <summary>The text is whole and final.</summary>
    Completed,

    /// <summary>The user stopped the turn; the text is what had been written until then.</summary>
    Stopped,

    /// <summary>The turn failed; the text is what had been written until then, and a notice says why.</summary>
    Failed,
}

/// <summary>
///     A reply of the AI copilot in the transcript: the text that the copilot wrote, which grows while it streams, and
///     what became of it.
/// </summary>
/// <remarks>
///     <para>
///         While the reply <see cref="AssistantReplyState.Streaming" /> the text is plain and only grows
///         (<see cref="AppendText" />). When the part of the answer is whole the reply is <see cref="Complete" />d, and
///         <see cref="IsComplete" /> tells the view that the text is final and can be shown as Markdown. A reply that is
///         <see cref="Stop" />ped or <see cref="Fail" />ed keeps its text and is not Markdown, because the text may end in
///         the middle of a construct. A reply that has ended never changes again.
///     </para>
///     <para>
///         An answer that calls tools is a sequence of replies and tool rows: a reply ends when the copilot calls a tool,
///         and the text that follows the tool is a new reply.
///     </para>
/// </remarks>
public sealed class AssistantReplyViewModel : TranscriptRowViewModel
{
    /// <summary>
    ///     The text so far.
    /// </summary>
    private string _text;

    /// <summary>
    ///     What became of the reply.
    /// </summary>
    private AssistantReplyState _state;

    /// <summary>
    ///     The Markdown blocks of the text, read when they are first asked for.
    /// </summary>
    private IReadOnlyList<MarkdownBlock>? _blocks;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AssistantReplyViewModel" /> class for a reply that starts to stream,
    ///     with no text yet.
    /// </summary>
    public AssistantReplyViewModel()
        : this(string.Empty, AssistantReplyState.Streaming)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AssistantReplyViewModel" /> class for a reply that is already in a
    ///     state, such as one that was restored.
    /// </summary>
    /// <param name="text">The text of the reply.</param>
    /// <param name="state">What became of the reply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is <see langword="null" />.</exception>
    public AssistantReplyViewModel(string text, AssistantReplyState state)
    {
        ArgumentNullException.ThrowIfNull(text);

        _text = text;
        _state = state;
    }

    /// <inheritdoc />
    public override TranscriptRowKind Kind => TranscriptRowKind.Assistant;

    /// <summary>
    ///     Gets the text of the reply so far.
    /// </summary>
    public string Text => _text;

    /// <summary>
    ///     Gets a value indicating whether the reply has any text.
    /// </summary>
    public bool HasText => _text.Length > 0;

    /// <summary>
    ///     Gets what became of the reply.
    /// </summary>
    public AssistantReplyState State => _state;

    /// <summary>
    ///     Gets a value indicating whether the text is still being written.
    /// </summary>
    public bool IsStreaming => _state == AssistantReplyState.Streaming;

    /// <summary>
    ///     Gets a value indicating whether the text is whole and final, so that the view can show it as Markdown.
    /// </summary>
    public bool IsComplete => _state == AssistantReplyState.Completed;

    /// <summary>
    ///     Gets the Markdown blocks of the text once the reply is complete, and <see langword="null" /> while it is
    ///     streaming, stopped, or failed, when the text is shown as plain text. The text is read once and the blocks are
    ///     kept.
    /// </summary>
    public IReadOnlyList<MarkdownBlock>? Blocks => IsComplete ? _blocks ??= KnowledgeMarkdown.Parse(_text) : null;

    /// <summary>
    ///     Gets the word that says what became of a reply that did not end normally: <c>Stopped</c> or <c>Failed</c>, and
    ///     an empty string otherwise.
    /// </summary>
    public string StatusLabel => _state switch
    {
        AssistantReplyState.Stopped => "Stopped",
        AssistantReplyState.Failed => "Failed",
        _ => string.Empty,
    };

    /// <summary>
    ///     Adds a piece of text to the end of the reply. Nothing happens when the piece is empty or the reply has ended.
    /// </summary>
    /// <param name="text">The piece of text.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is <see langword="null" />.</exception>
    public void AppendText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (_state != AssistantReplyState.Streaming || text.Length == 0)
        {
            return;
        }

        var hadText = HasText;
        _text += text;
        OnPropertyChanged(nameof(Text));
        if (!hadText)
        {
            OnPropertyChanged(nameof(HasText));
        }
    }

    /// <summary>
    ///     Ends the reply normally: the text is whole and final. Nothing happens when the reply has ended.
    /// </summary>
    public void Complete() => End(AssistantReplyState.Completed);

    /// <summary>
    ///     Ends the reply because the user stopped the turn; the text is kept. Nothing happens when the reply has ended.
    /// </summary>
    public void Stop() => End(AssistantReplyState.Stopped);

    /// <summary>
    ///     Ends the reply because the turn failed; the text is kept. Nothing happens when the reply has ended.
    /// </summary>
    public void Fail() => End(AssistantReplyState.Failed);

    /// <summary>
    ///     Ends the reply in a state, once.
    /// </summary>
    /// <param name="state">The state that ends the reply.</param>
    private void End(AssistantReplyState state)
    {
        if (_state != AssistantReplyState.Streaming)
        {
            return;
        }

        _state = state;
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsStreaming));
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(Blocks));
        OnPropertyChanged(nameof(StatusLabel));
    }
}
