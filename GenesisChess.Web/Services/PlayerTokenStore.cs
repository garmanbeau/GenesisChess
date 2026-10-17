using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace GenesisChess.Web.Services;

/// <summary>
/// Remembers this browser's player token per game in encrypted local storage, so a refresh or a
/// reconnect keeps the player's seat. Only usable once the circuit is interactive (not while prerendering).
/// </summary>
public sealed class PlayerTokenStore(ProtectedLocalStorage storage)
{
    private static string Key(string gameId) => $"genesis-chess:token:{gameId}";

    public async Task<string?> GetAsync(string gameId)
    {
        try
        {
            var result = await storage.GetAsync<string>(Key(gameId));
            return result.Success ? result.Value : null;
        }
        catch (CryptographicException)
        {
            // Data-protection keys changed (e.g. a different machine); the old token is unreadable.
            await storage.DeleteAsync(Key(gameId));
            return null;
        }
    }

    public ValueTask SetAsync(string gameId, string token) => storage.SetAsync(Key(gameId), token);
}
