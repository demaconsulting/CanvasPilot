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

namespace DemaConsulting.CanvasPilot.SettingsSubsystem;

/// <summary>
///     The outcome of checking whether the AI provider that the settings describe can be used.
/// </summary>
/// <param name="IsUsable">
///     <see langword="true" /> when the provider can be used; a warning does not make it unusable.
/// </param>
/// <param name="Message">One sentence for the user that says what was found and, when it is not usable, what to do.</param>
public sealed record ProviderCheckResult(bool IsUsable, string Message);

/// <summary>
///     Checks whether the AI provider that the settings describe can be used. The Settings subsystem owns this
///     interface so that the settings dialog can offer a connection check without depending on the subsystem that
///     implements it.
/// </summary>
public interface IProviderCheck
{
    /// <summary>
    ///     Checks the provider that the settings describe.
    /// </summary>
    /// <param name="settings">The AI provider choices to check, which may not have been saved yet.</param>
    /// <param name="cancellationToken">A token that cancels the check.</param>
    /// <returns>The outcome; a failed check is a result, not an exception.</returns>
    Task<ProviderCheckResult> CheckAsync(AiProviderSettings settings, CancellationToken cancellationToken = default);
}
