using System.Collections.Concurrent;

namespace GenesisChess.Application;

/// <summary>Where live sessions are kept. Swap the implementation to persist or share across servers.</summary>
public interface IGameStore
{
    void Add(GameSession session);

    GameSession? Find(string id);

    bool Remove(string id);

    IEnumerable<GameSession> All();
}

/// <summary>Process-local store; sessions are lost on restart.</summary>
public sealed class InMemoryGameStore : IGameStore
{
    private readonly ConcurrentDictionary<string, GameSession> _sessions = new(StringComparer.Ordinal);

    public void Add(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!_sessions.TryAdd(session.Id, session))
        {
            throw new InvalidOperationException($"A game with id '{session.Id}' already exists.");
        }
    }

    public GameSession? Find(string id) => _sessions.GetValueOrDefault(id);

    public bool Remove(string id) => _sessions.TryRemove(id, out _);

    public IEnumerable<GameSession> All() => _sessions.Values;
}
