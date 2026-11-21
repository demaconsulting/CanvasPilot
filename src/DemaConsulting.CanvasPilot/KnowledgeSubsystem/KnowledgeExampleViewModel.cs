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

using CommunityToolkit.Mvvm.Input;

namespace DemaConsulting.CanvasPilot.KnowledgeSubsystem;

/// <summary>
///     One example of the card that is shown, with the command that copies its text to the clipboard.
/// </summary>
/// <param name="Example">The example.</param>
/// <param name="CopyCommand">The command of the browser that copies an example; the parameter is this object.</param>
public sealed record KnowledgeExampleViewModel(KnowledgeExample Example, IAsyncRelayCommand<KnowledgeExampleViewModel> CopyCommand)
{
    /// <summary>
    ///     Gets the name of the example.
    /// </summary>
    public string Name => Example.Name;

    /// <summary>
    ///     Gets the line above the example: its name, what it is, its language, and the name of its file when it has one.
    /// </summary>
    public string Header => Example.FileName is null
        ? $"{Example.Name} ({Example.Kind}, {Example.Language})"
        : $"{Example.Name} ({Example.Kind}, {Example.Language}, {Example.FileName})";

    /// <summary>
    ///     Gets what the example shows.
    /// </summary>
    public string Description => Example.Description;

    /// <summary>
    ///     Gets the text of the example, which is what the command copies.
    /// </summary>
    public string Content => Example.Content;
}
