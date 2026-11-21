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
///     A card that the user can go to from the card that is shown: a card below it, a related card, or a hit of the
///     search. The window shows it as a button that runs <see cref="NavigateCommand" /> with the identifier.
/// </summary>
/// <param name="Id">The identifier of the card, which is the parameter of the command.</param>
/// <param name="Title">The title of the card.</param>
/// <param name="Summary">The one-line summary of the card.</param>
/// <param name="NavigateCommand">The command of the browser that shows a card by its identifier.</param>
public sealed record KnowledgeLinkViewModel(string Id, string Title, string Summary, IRelayCommand<string> NavigateCommand);
