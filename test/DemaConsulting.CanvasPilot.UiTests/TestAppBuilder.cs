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

using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;

[assembly: AvaloniaTestApplication(typeof(DemaConsulting.CanvasPilot.UiTests.TestAppBuilder))]

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Builds the headless Avalonia application used by <see cref="AvaloniaFactAttribute" />-decorated tests in
///     this assembly, so real views and view models can be exercised without a visible window or platform
///     windowing system. Each headless-Avalonia test assembly needs its own
///     <see cref="AvaloniaTestApplicationAttribute" /> target, since the attribute is assembly-scoped.
/// </summary>
public static class TestAppBuilder
{
    /// <summary>
    ///     Configures the headless Avalonia application instance shared by this assembly's Avalonia-backed tests.
    /// </summary>
    /// <remarks>
    ///     The embedded Inter font is registered, exactly as the desktop program registers it, so headless text is
    ///     measured and drawn with the same font on every machine: layout assertions and screenshots do not depend on
    ///     the system fonts of the machine that runs them.
    /// </remarks>
    /// <returns>An <see cref="AppBuilder" /> configured for headless rendering.</returns>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .WithInterFont()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
