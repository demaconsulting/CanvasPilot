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

namespace DemaConsulting.CanvasPilot.PreviewSubsystem;

/// <summary>
///     Takes notes about the shown picture from a part that is not the preview pane, so that they are listed with the
///     notes of the renderer. The Preview subsystem owns this narrow interface, so a part that checks the picture can
///     report to the pane without depending on it.
/// </summary>
public interface IRenderNoteSink
{
    /// <summary>
    ///     Shows notes in the list of notes of the picture. They replace the notes that were shown earlier for the same
    ///     group, and they go away when the picture is drawn again.
    /// </summary>
    /// <param name="group">
    ///     The name of the group of notes, a short lower case word; the code of every note of the group starts with the
    ///     group name and a hyphen.
    /// </param>
    /// <param name="notes">The notes to show; empty to remove the notes of the group.</param>
    void ShowNotes(string group, IReadOnlyList<RenderDiagnostic> notes);
}
