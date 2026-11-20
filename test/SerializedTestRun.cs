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

using Xunit;

[assembly: AssemblyFixture(typeof(DemaConsulting.CanvasPilot.TestSupport.SerializedTestRun))]

namespace DemaConsulting.CanvasPilot.TestSupport;

/// <summary>
///     Lets only one of the test projects of the repository run its tests at a time, however many test hosts
///     <c>dotnet test</c> starts together.
/// </summary>
/// <remarks>
///     <para>
///         <c>dotnet test</c> on the solution starts the test host of every test project at the same moment. The unit
///         tests, the headless user interface tests, and the OTS tests then compete for the processor, and tests that
///         depend on timing (the speech recognition pipeline, file watchers, the headless platform) fail now and then,
///         or a host reports a fatal error at the end of its run, although every test passes when its project runs
///         alone. The fixture makes each host wait, before its first test, until it holds an exclusive lock on one file
///         in the temporary folder, and release it after its last test, so the projects run one after the other.
///     </para>
///     <para>
///         The lock is held by the operating system on an open file, so a host that is killed releases it at once and
///         never leaves the others waiting for it. A host that cannot get the lock within <see cref="MaxWait" />, or
///         cannot use the file at all, runs its tests anyway: the fixture is a help for repeatable runs, never a reason
///         for a run to hang or fail. The source file is compiled into each test project, so the three projects
///         agree on the file.
///     </para>
/// </remarks>
public sealed class SerializedTestRun : IAsyncLifetime
{
    /// <summary>
    ///     The longest a test host waits for the others before it runs its tests anyway.
    /// </summary>
    public static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(10);

    /// <summary>
    ///     How long a host waits between two attempts to get the lock.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    ///     The open lock file while this host holds the lock, or <see langword="null" />.
    /// </summary>
    private FileStream? _lock;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        // Try until the lock is ours, the wait is over, or the file cannot be used at all
        var path = Path.Combine(Path.GetTempPath(), "canvaspilot-test-run.lock");
        var deadline = DateTime.UtcNow + MaxWait;
        while (_lock is null && DateTime.UtcNow < deadline)
        {
            try
            {
                _lock = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                // Another test host holds the lock: wait for it to finish its run
                await Task.Delay(PollInterval);
            }
            catch (UnauthorizedAccessException)
            {
                // The file cannot be used, so the tests run without the lock
                return;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        // Let the next test host run
        _lock?.Dispose();
        _lock = null;
        return ValueTask.CompletedTask;
    }
}
