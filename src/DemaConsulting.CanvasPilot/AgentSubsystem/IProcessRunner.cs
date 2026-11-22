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
///     What came of running a program: whether it started, how it ended, and what it printed.
/// </summary>
/// <remarks>
///     A program that cannot be started, or that is stopped because it ran too long, is a normal result and not an
///     exception, so a caller that probes programs that may be broken can treat every outcome as data.
/// </remarks>
/// <param name="Started">
///     <see langword="true" /> when the program was started; <see langword="false" /> when the operating system refused
///     to start it (the file is missing, is not a program, or may not be run).
/// </param>
/// <param name="ExitCode">
///     The exit code of the program; <c>-1</c> when it did not start or was stopped because it ran too long.
/// </param>
/// <param name="Output">
///     What the program printed on its standard output followed by what it printed on its standard error; for a program
///     that did not start, a short reason; never <see langword="null" />.
/// </param>
/// <param name="TimedOut">
///     <see langword="true" /> when the program ran longer than the time allowed and was stopped.
/// </param>
public sealed record ProcessResult(bool Started, int ExitCode, string Output, bool TimedOut)
{
    /// <summary>
    ///     Makes the result of a program that did not start.
    /// </summary>
    /// <param name="reason">A short reason, kept as the output.</param>
    /// <returns>A result whose <see cref="Started" /> is <see langword="false" />.</returns>
    public static ProcessResult NotStarted(string reason)
    {
        return new ProcessResult(false, -1, reason, false);
    }
}

/// <summary>
///     Runs a program to completion with a time limit and returns what it printed. The interface is the seam that lets
///     the code that probes installed programs (the Copilot CLI locator, the credential check of the readiness checks) be
///     tested without running any program.
/// </summary>
/// <remarks>
///     The arguments are a list and not one string, so no argument is ever split or quoted by the caller. An
///     implementation must not block the thread that calls it.
/// </remarks>
public interface IProcessRunner
{
    /// <summary>
    ///     Runs a program and waits for it to end, for no longer than the time given.
    /// </summary>
    /// <param name="executablePath">The full path of the program.</param>
    /// <param name="arguments">The arguments, one list entry each; may be empty.</param>
    /// <param name="timeout">
    ///     The longest time to wait; a program still running after it is stopped together with the programs it started.
    ///     Must be longer than zero.
    /// </param>
    /// <param name="cancellationToken">A token that stops the program and cancels the wait.</param>
    /// <returns>
    ///     The result; a program that cannot be started or that runs too long is a result, not an exception.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled; the program is stopped.</exception>
    Task<ProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
