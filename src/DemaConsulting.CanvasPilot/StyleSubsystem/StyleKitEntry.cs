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
///     A style kit that was found in one of the places, with where it is and what that means for what can be done to it.
/// </summary>
/// <remarks>
///     A kit of a lower place with the same id as a kit of a higher place is still an entry, marked as overridden, so that
///     the user can see it and the name of what hides it; only the entry that is not overridden is the kit that is used
///     for the id. The record is immutable and safe to share between threads.
/// </remarks>
/// <param name="Kit">The kit, as read from its folder.</param>
/// <param name="Location">The place the kit was found in.</param>
/// <param name="FolderPath">The full path of the kit folder.</param>
/// <param name="SharedRoot">The shared folder that holds the kit when the location is <see cref="StyleKitLocation.Shared" />; otherwise <see langword="null" />.</param>
/// <param name="IsReadOnly"><see langword="true" /> for a kit that comes with the program or that a team shares, which the application does not change.</param>
/// <param name="IsOverridden"><see langword="true" /> when a kit of a more local place has the same id and so hides this one.</param>
/// <param name="OverriddenBy">The name of the place that holds the kit that hides this one, or <see langword="null" /> when this kit is not overridden.</param>
/// <param name="Warnings">The warnings that reading the kit gave; the kit is usable.</param>
public sealed record StyleKitEntry(
    StyleKit Kit,
    StyleKitLocation Location,
    string FolderPath,
    string? SharedRoot,
    bool IsReadOnly,
    bool IsOverridden,
    string? OverriddenBy,
    IReadOnlyList<StyleIssue> Warnings);
