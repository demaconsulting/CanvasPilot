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

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     Something that listens to the user while a talk button or a talk key is held and turns what was said into part of
///     the message. The window reaches it through this interface when the talk key is pressed and released, so the
///     window does not need to know what dictates.
/// </summary>
public interface IPushToTalkTarget
{
    /// <summary>
    ///     Starts listening because the talk button or key was pressed. A press while listening is already going on, or
    ///     is being started, is ignored.
    /// </summary>
    /// <returns>A task that completes when listening has started, or when it was found that it cannot.</returns>
    Task BeginPushToTalkAsync();

    /// <summary>
    ///     Stops listening because the talk button or key was released, and adds what was said to the message. A release
    ///     while nothing is listening is ignored; a release while listening is still being started makes it give up.
    /// </summary>
    /// <returns>A task that completes when what was said has been added to the message.</returns>
    Task EndPushToTalkAsync();
}
