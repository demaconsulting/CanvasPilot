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
///     A named size at which a picture of a kit is exported, for example a slide or an email banner.
/// </summary>
/// <remarks>
///     At least one of <paramref name="Width" /> and <paramref name="Height" /> is set. When only one is set the picture
///     is scaled to it keeping its ratio; when both are set the picture is fitted inside that box keeping its ratio and
///     the margin is filled with the background (or left transparent). The values are not checked here;
///     <see cref="StyleKitSerializer.Validate" /> does that.
/// </remarks>
/// <param name="Name">The name that the user sees in the export dialog, 1 to 40 characters, unique within a kit ignoring case.</param>
/// <param name="Width">The width in pixels (1 to 8192), or <see langword="null" /> to scale from the height.</param>
/// <param name="Height">The height in pixels (1 to 8192), or <see langword="null" /> to scale from the width.</param>
/// <param name="Dpi">The resolution that is written into the PNG file (24 to 1200), or <see langword="null" /> for none.</param>
/// <param name="Transparent"><see langword="true" /> to keep the margin transparent; <see langword="false" /> to fill it with the background of the kit.</param>
public sealed record StyleSizePreset(string Name, int? Width, int? Height, double? Dpi, bool Transparent);
