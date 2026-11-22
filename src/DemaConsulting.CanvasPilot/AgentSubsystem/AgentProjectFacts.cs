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

using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     What the instructions of the copilot say about the open project.
/// </summary>
/// <param name="Name">The name of the project.</param>
/// <param name="Folder">The full path of the project folder.</param>
/// <param name="Files">
///     The paths of the files of the project, relative to the project folder and written with forward slashes, in the
///     order in which they should be listed; the instructions list only the first few.
/// </param>
/// <param name="ActiveStyle">The style kit that the project uses, or <see langword="null" /> when it has none or it cannot be found.</param>
/// <param name="ActiveStyleId">
///     The id of the style kit that the project file names, or <see langword="null" /> for none. It is told to the copilot
///     when the kit cannot be found.
/// </param>
public sealed record AgentProjectFacts(
    string Name,
    string Folder,
    IReadOnlyList<string> Files,
    StyleKitEntry? ActiveStyle,
    string? ActiveStyleId);
