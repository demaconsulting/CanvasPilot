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

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     What the catalog found out about one place in which kits are kept: where it is, whether it could be read, and which
///     folders in it hold a kit that cannot be used.
/// </summary>
/// <param name="Location">The kind of place.</param>
/// <param name="Path">The full path of the folder of the place.</param>
/// <param name="IsAvailable"><see langword="false" /> when the folder could not be read now, for example a network folder that is offline.</param>
/// <param name="Problem">Why the place is not available, or a note about it, or <see langword="null" /> when there is nothing to say.</param>
/// <param name="Broken">The folders of the place that have a style file with errors.</param>
public sealed record StyleLocationStatus(
    StyleKitLocation Location,
    string Path,
    bool IsAvailable,
    string? Problem,
    IReadOnlyList<StyleBrokenKit> Broken);
