using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GenesisChess.Application;

public sealed class GameOptions
{
    /// <summary>Sessions with no activity for this long are removed.</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromHours(24);

    /// <summary>How often the store is swept for idle sessions.</summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>Periodically removes abandoned sessions from the store.</summary>
public sealed class GameCleanupService(IGameStore store, TimeProvider time, IOptions<GameOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.CleanupInterval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            RemoveIdle();
        }
    }

    /// <summary>Removes idle sessions now and returns how many were removed.</summary>
    internal int RemoveIdle()
    {
        var cutoff = time.GetUtcNow() - options.Value.IdleTimeout;
        var removed = 0;
        foreach (var session in store.All().Where(s => s.LastActivity < cutoff).ToList())
        {
            if (store.Remove(session.Id))
            {
                session.Dispose();
                removed++;
            }
        }

        return removed;
    }
}
