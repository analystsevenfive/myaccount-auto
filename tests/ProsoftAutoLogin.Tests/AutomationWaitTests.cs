using System.Diagnostics;
using ProsoftAutoLogin.Automation;

namespace ProsoftAutoLogin.Tests;

public sealed class AutomationWaitTests
{
    [Fact]
    public async Task UntilAsync_ReturnsAsSoonAsConditionSucceeds()
    {
        var probes = 0;
        var stopwatch = Stopwatch.StartNew();

        var result = await AutomationWait.UntilAsync(
            () => ++probes >= 3,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(20),
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(3, probes);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UntilAsync_ReturnsFalseAtTimeout()
    {
        var result = await AutomationWait.UntilAsync(
            () => false,
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(10),
            CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task UntilAsync_HonorsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            AutomationWait.UntilAsync(
                () => false,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromMilliseconds(10),
                cancellation.Token));
    }

}
