using System.Diagnostics;

namespace ProsoftAutoLogin.Automation;

internal static class AutomationWait
{
    public static async Task<T?> UntilNotNullAsync<T>(
        Func<T?> probe,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(probe);
        ValidateDurations(timeout, pollInterval);

        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = probe();
            if (result is not null)
            {
                return result;
            }

            var remaining = timeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                return null;
            }

            await Task.Delay(Min(pollInterval, remaining), cancellationToken);
        }
    }

    public static async Task<bool> UntilAsync(
        Func<bool> condition,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(condition);

        var result = await UntilNotNullAsync(
            () => condition() ? TrueMarker.Instance : null,
            timeout,
            pollInterval,
            cancellationToken);

        return result is not null;
    }

    private static void ValidateDurations(TimeSpan timeout, TimeSpan pollInterval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;

    private sealed class TrueMarker
    {
        public static readonly TrueMarker Instance = new();
    }
}
