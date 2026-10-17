using GenesisChess.Application;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class GenesisChessServiceCollectionExtensions
{
    /// <summary>Registers the game service, an in-memory store and idle-game cleanup.</summary>
    public static IServiceCollection AddGenesisChess(this IServiceCollection services, Action<GameOptions>? configure = null)
    {
        services.AddOptions<GameOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IGameStore, InMemoryGameStore>();
        services.TryAddSingleton<GameService>();
        services.AddHostedService<GameCleanupService>();
        return services;
    }
}
