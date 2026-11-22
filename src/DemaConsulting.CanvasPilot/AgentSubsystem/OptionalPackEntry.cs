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

using DemaConsulting.AgentKit.Core;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     One entry of the catalog of optional tool packs: a pack that the user can switch on, described so that a settings
///     page can list it without creating it.
/// </summary>
/// <remarks>
///     The catalog is empty in this version. The entry exists so that adding a pack later (for example one of the text
///     file packs of the AI library) is one entry in the catalog and one stored identifier, and nothing else is
///     restructured.
/// </remarks>
/// <param name="DisplayName">The name that a settings page shows for the pack.</param>
/// <param name="Factory">Creates the pack for one project.</param>
/// <param name="RequiredCapabilities">What the model or provider must be able to do for the pack to be offered.</param>
public sealed record OptionalPackEntry(
    string DisplayName,
    Func<AgentToolContext, IToolPack> Factory,
    HostCapabilities RequiredCapabilities);
