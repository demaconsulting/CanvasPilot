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

using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     The <see cref="IProcessRunner" /> that starts a real program: no shell, no window, an argument list, redirected
///     output, a time limit, and the whole tree of processes stopped when the limit is reached.
/// </summary>
/// <remarks>
///     The program is started with <c>UseShellExecute</c> off, so the path is run as given and nothing is interpreted by
///     a shell; the arguments are added one by one, so the operating system does the quoting. Starting and waiting are
///     done without blocking the calling thread, which makes the runner safe to call from the user interface thread.
///     The runner holds no state and is safe for concurrent use. It never logs: what a program prints is returned to the
///     caller, who decides what may be recorded.
/// </remarks>
public sealed class ProcessRunner : IProcessRunner
{
    /// <summary>
    ///     How long to wait for the output streams to end after the program was stopped, because a program that was not
    ///     stopped completely (a process that escaped the tree) may keep them open.
    /// </summary>
    private static readonly TimeSpan StreamDrainGrace = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public async Task<ProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        // Refuse arguments that are a programming error; every problem with the program itself is data
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        // Describe the process: no shell, no window, everything redirected so the program cannot wait for a console
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Start on a thread-pool thread, because starting a process can block for a noticeable time
        var (process, reason) = await Task.Run(() => TryStart(startInfo), cancellationToken).ConfigureAwait(false);
        if (process is null)
        {
            return ProcessResult.NotStarted(reason);
        }

        using (process)
        {
            return await WaitAsync(process, timeout, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Starts the process and closes its input, so that a program that reads its input sees the end at once.
    /// </summary>
    /// <param name="startInfo">How to start the process.</param>
    /// <returns>
    ///     The started process, or <see langword="null" /> with the reason when the operating system refused to start it.
    /// </returns>
    private static (Process? Process, string Reason) TryStart(ProcessStartInfo startInfo)
    {
        try
        {
            var process = Process.Start(startInfo);
            process?.StandardInput.Close();
            return (process, process is null ? "The program could not be started." : string.Empty);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or PlatformNotSupportedException)
        {
            // A missing file, a file that is not a program, or a refused permission is an expected outcome of probing
            return (null, ex.Message);
        }
    }

    /// <summary>
    ///     Waits for a started process for the time allowed, collecting what it prints, and stops it when the time ends.
    /// </summary>
    /// <param name="process">The started process.</param>
    /// <param name="timeout">The longest time to wait.</param>
    /// <param name="cancellationToken">The token of the caller; its cancellation stops the process and is rethrown.</param>
    /// <returns>The result, with <see cref="ProcessResult.TimedOut" /> set when the time ended first.</returns>
    private static async Task<ProcessResult> WaitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        // Read both streams at the same time as the wait, so a program that fills one pipe can never block on it
        var outputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var errorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

        // Wait for the exit, limited by the timeout and by the token of the caller
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stop the process and everything it started, then tell a cancellation of the caller from the time limit
            Kill(process);
            cancellationToken.ThrowIfCancellationRequested();
            timedOut = true;
        }

        // Collect what the program printed; a stream that does not end in time (an escaped process) is given up
        var streams = Task.WhenAll(outputTask, errorTask);
        var finished = await Task.WhenAny(streams, Task.Delay(StreamDrainGrace, CancellationToken.None)).ConfigureAwait(false);
        var output = finished == streams ? JoinOutput(outputTask.Result, errorTask.Result) : string.Empty;

        return new ProcessResult(true, timedOut ? -1 : process.ExitCode, output, timedOut);
    }

    /// <summary>
    ///     Stops a process and all processes that it started.
    /// </summary>
    /// <param name="process">The process to stop.</param>
    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // The process ended on its own between the timeout and the kill, which is the outcome that was wanted
        }
    }

    /// <summary>
    ///     Joins what a program printed on its standard output and its standard error into one text.
    /// </summary>
    /// <param name="standardOutput">The text of the standard output.</param>
    /// <param name="standardError">The text of the standard error.</param>
    /// <returns>The standard output followed by the standard error on a new line, without trailing white space.</returns>
    private static string JoinOutput(string standardOutput, string standardError)
    {
        var joined = string.IsNullOrEmpty(standardError)
            ? standardOutput
            : standardOutput.TrimEnd() + Environment.NewLine + standardError;
        return joined.TrimEnd();
    }
}
