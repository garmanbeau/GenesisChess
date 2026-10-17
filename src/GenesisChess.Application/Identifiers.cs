using System.Security.Cryptography;
using System.Text;

namespace GenesisChess.Application;

/// <summary>Random game ids (short, URL friendly) and player tokens (unguessable secrets).</summary>
internal static class Identifiers
{
    // No 0/o/1/l to keep ids readable when shared by voice or chat.
    private const string IdAlphabet = "abcdefghijkmnpqrstuvwxyz23456789";

    public static string NewGameId() => RandomNumberGenerator.GetString(IdAlphabet, 10);

    public static string NewPlayerToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static bool TokensMatch(string? expected, string? actual) =>
        expected is not null && actual is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
}
