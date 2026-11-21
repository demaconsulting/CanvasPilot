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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OtsSoftwareTests;

/// <summary>
///     Self-validation tests for the CommunityToolkit.Mvvm off-the-shelf software item: the MVVM library whose
///     source generators write the property change notification and command code of the application's view models.
///     They use generated members the same way the view models do.
/// </summary>
public class CommunityToolkitMvvmTests
{
    /// <summary>
    ///     Test that a generated observable property raises a change notification for itself and for a dependent
    ///     property, and raises nothing when the value is unchanged.
    /// </summary>
    [Fact]
    public void CommunityToolkitMvvm_ObservableProperty_RaisesOwnAndDependentNotifications()
    {
        // Arrange: a sample object and a recorder of the names of the properties that changed
        var sample = new SampleViewModel();
        var raised = new List<string>();
        sample.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);

        // Act: change the value, then set the same value again
        sample.Value = "changed";
        var afterChange = raised.ToArray();
        sample.Value = "changed";

        // Assert: the property and its dependent property were reported once, and the repeat reported nothing
        Assert.Equal([nameof(SampleViewModel.Value), nameof(SampleViewModel.HasValue)], afterChange);
        Assert.Equal(afterChange, raised);
        Assert.True(sample.HasValue);
    }

    /// <summary>
    ///     Test that a generated command follows its can-execute rule, raises its can-execute change event when the
    ///     property it depends on changes, and runs its method only when it can run.
    /// </summary>
    [Fact]
    public void CommunityToolkitMvvm_RelayCommand_FollowsCanExecuteRule()
    {
        // Arrange: a sample object whose command can run only when it has a value
        var sample = new SampleViewModel();
        var changes = 0;
        sample.ClearCommand.CanExecuteChanged += (_, _) => changes++;
        var canRunWithoutValue = sample.ClearCommand.CanExecute(null);

        // Act: give the object a value and run the command
        sample.Value = "something";
        var canRunWithValue = sample.ClearCommand.CanExecute(null);
        sample.ClearCommand.Execute(null);
        var valueAfterRun = sample.Value;

        // Assert: the rule was followed, the change events were raised, and the method ran once
        Assert.False(canRunWithoutValue);
        Assert.True(canRunWithValue);
        Assert.True(changes >= 2);
        Assert.Equal(string.Empty, valueAfterRun);
        Assert.Equal(1, sample.ClearRuns);
    }

    /// <summary>
    ///     Test that a generated asynchronous command reports that it is running, and refuses to start again
    ///     while it is running.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CommunityToolkitMvvm_AsyncRelayCommand_ReportsRunningAndRefusesConcurrentRuns()
    {
        // Arrange: a sample object whose asynchronous command waits for a signal
        var sample = new SampleViewModel();
        var idleBefore = !sample.WaitCommand.IsRunning && sample.WaitCommand.CanExecute(null);

        // Act: start the command, ask whether it can start again, release it, and wait for the end
        var running = sample.WaitCommand.ExecuteAsync(null);
        var runningDuring = sample.WaitCommand.IsRunning;
        var canStartAgain = sample.WaitCommand.CanExecute(null);
        sample.Release.SetResult();
        await running;

        // Assert: the command reported its run, refused to start twice, and is ready again
        Assert.True(idleBefore);
        Assert.True(runningDuring);
        Assert.False(canStartAgain);
        Assert.False(sample.WaitCommand.IsRunning);
        Assert.True(sample.WaitCommand.CanExecute(null));
        Assert.Equal(1, sample.WaitRuns);
    }
}

/// <summary>
///     Sample view model that uses the generated members the way the application's view models do.
/// </summary>
internal sealed partial class SampleViewModel : ObservableObject
{
    /// <summary>
    ///     Backing field for the generated <c>Value</c> property.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValue))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    private string _value = string.Empty;

    /// <summary>
    ///     Gets the signal that lets <c>WaitCommand</c> finish.
    /// </summary>
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    ///     Gets the number of times <c>ClearCommand</c> ran.
    /// </summary>
    public int ClearRuns { get; private set; }

    /// <summary>
    ///     Gets the number of times <c>WaitCommand</c> finished.
    /// </summary>
    public int WaitRuns { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether <see cref="Value" /> is not empty.
    /// </summary>
    public bool HasValue => Value.Length > 0;

    /// <summary>
    ///     Clears the value; can run only when there is a value.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasValue))]
    private void Clear()
    {
        ClearRuns++;
        Value = string.Empty;
    }

    /// <summary>
    ///     Waits for <see cref="Release" />.
    /// </summary>
    /// <returns>A task that completes when the signal is set.</returns>
    [RelayCommand]
    private async Task WaitAsync()
    {
        await Release.Task;
        WaitRuns++;
    }
}
