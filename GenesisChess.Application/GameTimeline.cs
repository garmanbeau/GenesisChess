using GenesisChess.Engine;

namespace GenesisChess.Application;

/// <summary>
/// The position after every ply of a game, rebuilt by replaying the recorded actions from the initial state.
/// States are computed lazily and cached, and <see cref="Sync"/> only replays moves it has not seen yet,
/// so one instance can follow a live game cheaply. Not thread-safe: use one per viewer.
/// </summary>
public sealed class GameTimeline
{
    private readonly List<GameState> _states;
    private readonly List<MoveRecord> _moves = [];

    public GameTimeline(GameState initial) => _states = [initial];

    /// <summary>The state before any action.</summary>
    public GameState Initial => _states[0];

    /// <summary>Number of plies known; <see cref="StateAt"/> accepts 0 through this value.</summary>
    public int LatestPly => _moves.Count;

    public IReadOnlyList<MoveRecord> Moves => _moves;

    /// <summary>
    /// Brings the timeline up to date with <paramref name="moves"/>. If the known moves are not a prefix of
    /// the new list (another game, or history rewritten), the cache is discarded and rebuilt.
    /// </summary>
    public void Sync(IReadOnlyList<MoveRecord> moves)
    {
        var shared = Math.Min(_moves.Count, moves.Count);
        var prefix = 0;
        while (prefix < shared && Equals(_moves[prefix].Action, moves[prefix].Action))
        {
            prefix++;
        }

        if (prefix < _moves.Count)
        {
            _moves.RemoveRange(prefix, _moves.Count - prefix);
            if (_states.Count > prefix + 1)
            {
                _states.RemoveRange(prefix + 1, _states.Count - prefix - 1);
            }
        }

        for (var i = prefix; i < moves.Count; i++)
        {
            _moves.Add(moves[i]);
        }
    }

    /// <summary>The position after <paramref name="ply"/> actions (0 is the initial state).</summary>
    public GameState StateAt(int ply)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ply);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ply, LatestPly);

        while (_states.Count <= ply)
        {
            var index = _states.Count - 1;
            var result = Rules.Apply(_states[index], _moves[index].Action);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Ply {index + 1} ({_moves[index].Action}) cannot be replayed: {result.Error}");
            }

            _states.Add(result.State);
        }

        return _states[ply];
    }

    /// <summary>The action that produced the position at <paramref name="ply"/>, or null for the initial state.</summary>
    public GameAction? ActionAt(int ply) => ply <= 0 || ply > LatestPly ? null : _moves[ply - 1].Action;
}
