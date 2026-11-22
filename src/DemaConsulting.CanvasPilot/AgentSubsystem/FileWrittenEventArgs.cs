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

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Tells that the copilot created or replaced a file of the project, so that the shell can select the file in the
///     project explorer and show the user what changed.
/// </summary>
/// <remarks>
///     The event carries only the project-relative path and whether the file is new, never the content of the file. It is
///     raised on the thread that ran the tool, which is not the thread of the user interface, so a handler must hand the
///     work over to that thread itself.
/// </remarks>
public sealed class FileWrittenEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FileWrittenEventArgs" /> class.
    /// </summary>
    /// <param name="relativePath">The path of the file below the project folder, with forward slashes.</param>
    /// <param name="created">
    ///     <see langword="true" /> when the file did not exist before the write; <see langword="false" /> when it was
    ///     replaced.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when the path is empty or white space.</exception>
    public FileWrittenEventArgs(string relativePath, bool created)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        RelativePath = relativePath;
        Created = created;
    }

    /// <summary>
    ///     Gets the path of the written file below the project folder, with forward slashes.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    ///     Gets a value indicating whether the file is new (<see langword="true" />) or was replaced
    ///     (<see langword="false" />).
    /// </summary>
    public bool Created { get; }
}
