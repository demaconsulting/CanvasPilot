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
///     The places a style kit can come from, listed in the order of precedence, highest first.
/// </summary>
/// <remarks>
///     The order of the members is the order in which a kit of the same identifier wins: the more specific and more local
///     a copy is, the more deliberate it is, so a kit in the project beats a kit of the user, which beats a kit of a
///     shared folder, which beats a kit that comes with the program.
/// </remarks>
public enum StyleKitLocation
{
    /// <summary>
    ///     The <c>styles</c> folder of the open project. Highest precedence.
    /// </summary>
    Project,

    /// <summary>
    ///     The <c>styles</c> folder in the data folder of the user.
    /// </summary>
    User,

    /// <summary>
    ///     A folder that the user lists in the settings, for example a folder of a team. Read only in the application.
    /// </summary>
    Shared,

    /// <summary>
    ///     The kits that come with the program, beside the executable. Read only. Lowest precedence.
    /// </summary>
    BuiltIn
}
