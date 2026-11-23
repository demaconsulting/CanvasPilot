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
///     Reads environment variables of the running process, as a seam so that the code that depends on them (the Copilot CLI
///     locator reads <c>PATH</c> and <c>LOCALAPPDATA</c>) can be tested without changing the environment of the test run.
/// </summary>
public interface IEnvironmentReader
{
    /// <summary>
    ///     Gets the value of an environment variable.
    /// </summary>
    /// <param name="name">The name of the variable.</param>
    /// <returns>The value, or <see langword="null" /> when the variable is not set.</returns>
    string? GetVariable(string name);
}

/// <summary>
///     The <see cref="IEnvironmentReader" /> that reads the environment of the running process.
/// </summary>
/// <remarks>
///     The class holds no state and is safe for concurrent use. It never writes a variable.
/// </remarks>
public sealed class SystemEnvironmentReader : IEnvironmentReader
{
    /// <inheritdoc />
    public string? GetVariable(string name)
    {
        return Environment.GetEnvironmentVariable(name);
    }
}
