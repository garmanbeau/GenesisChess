using GenesisChess.Engine;

namespace GenesisChess.Engine.Tests;

internal static class TestPositions
{
    /// <summary>Builds a state from a FEN placement with empty reserves unless given.</summary>
    public static GameState State(
        string placement,
        Color sideToMove = Color.White,
        Reserve? white = null,
        Reserve? black = null,
        string? enPassant = null) =>
        GameState.Create(
            Board.FromPlacementString(placement),
            white ?? Reserve.Empty,
            black ?? Reserve.Empty,
            sideToMove,
            enPassant is null ? null : Square.Parse(enPassant));

    public static MoveAction Move(string from, string to, PieceType? promotion = null) =>
        new(Square.Parse(from), Square.Parse(to), promotion);

    public static PlaceAction Place(PieceType type, string square) => new(type, Square.Parse(square));

    public static GameState Play(GameState state, params GameAction[] actions)
    {
        foreach (var action in actions)
        {
            var result = Rules.Apply(state, action);
            Assert.True(result.IsSuccess, $"{action} failed: {result.Error}");
            state = result.State;
        }

        return state;
    }

    public static HashSet<string> TargetsFrom(GameState state, string from) =>
        MoveGenerator.LegalMovesFrom(state, Square.Parse(from)).Select(m => m.To.ToString()).ToHashSet();
}
