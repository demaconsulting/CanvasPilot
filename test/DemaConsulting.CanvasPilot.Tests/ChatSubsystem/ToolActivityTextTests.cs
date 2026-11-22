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

using DemaConsulting.CanvasPilot.ChatSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests of the sentences that the tool rows of the transcript say, which the chat turn runner writes.
/// </summary>
public sealed class ToolActivityTextTests
{
    /// <summary>
    ///     Test that the value of an argument is found in the short description of the arguments, also when it holds a
    ///     comma, and that a missing or empty value gives the fallback of the tool.
    /// </summary>
    [Theory]
    [InlineData("render_image", "path=a, b.svg, size=3", "Rendering `a, b.svg`")]
    [InlineData("render_image", "size=3", "Rendering a picture")]
    [InlineData("render_image", "path=", "Rendering a picture")]
    [InlineData("project_read", "mode=x, path=notes/todo.txt", "Reading `notes/todo.txt`")]
    [InlineData("project_write", "", "Writing a file")]
    public void ToolActivityText_Started_FindsThePathInTheArguments(string tool, string arguments, string expected)
    {
        // Act / Assert: the sentence names the path, or the fallback
        Assert.Equal(expected, ToolActivityText.Started(tool, arguments));
    }

    /// <summary>
    ///     Test that the sentences of an unknown tool, of a failure without a reason, of a written file, and of a call
    ///     that was cut short end properly.
    /// </summary>
    [Fact]
    public void ToolActivityText_FinishedWrittenAndAbandoned_SayWhatHappened()
    {
        // Act / Assert: an unknown tool that failed, a known tool that failed without a reason, and the file sentences
        Assert.Equal("mystery failed: no way", ToolActivityText.Finished("mystery", string.Empty, false, "no way"));
        Assert.Equal("mystery failed.", ToolActivityText.Finished("mystery", string.Empty, false, string.Empty));
        Assert.Equal("Could not render `a.svg`.", ToolActivityText.Finished("render_image", "path=a.svg", false, string.Empty));
        Assert.Equal("Created `a.svg`", ToolActivityText.Written("a.svg", replaced: false));
        Assert.Equal("Replaced `a.svg`", ToolActivityText.Written("a.svg", replaced: true));
        Assert.Equal("Rendering a picture (stopped)", ToolActivityText.Abandoned("Rendering a picture", stopped: true));
        Assert.Equal("Rendering a picture (no result)", ToolActivityText.Abandoned("Rendering a picture", stopped: false));
    }
}
