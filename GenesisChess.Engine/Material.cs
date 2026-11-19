namespace GenesisChess.Engine;

/// <summary>Standard piece values and capture detection.</summary>
public static class Material
{
    /// <summary>Pawn 1, knight 3, bishop 3, rook 5, queen 9. Kings are never captured and count 0.</summary>
    public static int Value(this PieceType type) => type switch
    {
        PieceType.Pawn => 1,
        PieceType.Knight => 3,
        PieceType.Bishop => 3,
        PieceType.Rook => 5,
        PieceType.Queen => 9,
        _ => 0,
    };

    /// <summary>
    /// Total value a color owns: pieces on the board plus pieces still in reserve. Reserve pieces
    /// count so that placing a piece never changes the balance; only captures and promotions do.
    /// </summary>
    public static int Total(GameState state, Color color) =>
        state.Board.Pieces(color).Sum(p => p.Piece.Type.Value())
        + Enum.GetValues<PieceType>().Sum(t => state.ReserveOf(color).Count(t) * t.Value());

    /// <summary>The piece <paramref name="action"/> captures from <paramref name="before"/>, if any (including en passant).</summary>
    public static Piece? CapturedBy(GameState before, GameAction action)
    {
        if (action is not MoveAction move || before.Board[move.From] is not { } mover)
        {
            return null;
        }

        if (before.Board[move.To] is { } target)
        {
            return target;
        }

        var enPassant = mover.Type == PieceType.Pawn && move.From.File != move.To.File && move.To == before.EnPassantTarget;
        return enPassant ? before.Board[new Square(move.To.File, move.From.Rank)] : null;
    }
}

/// <summary>Pieces each side has captured, and who is ahead on material.</summary>
public sealed record MaterialBalance(
    IReadOnlyList<PieceType> CapturedByWhite,
    IReadOnlyList<PieceType> CapturedByBlack,
    int WhiteAdvantage)
{
    public static MaterialBalance Empty { get; } = new([], [], 0);

    /// <summary>Pieces captured by <paramref name="color"/>, most valuable first.</summary>
    public IReadOnlyList<PieceType> CapturedBy(Color color) => color == Color.White ? CapturedByWhite : CapturedByBlack;

    /// <summary>How far <paramref name="color"/> is ahead (negative when behind).</summary>
    public int Advantage(Color color) => color == Color.White ? WhiteAdvantage : -WhiteAdvantage;

    /// <summary>
    /// Replays <paramref name="actions"/> from <paramref name="start"/>, collecting captured pieces.
    /// A captured promoted piece counts as its promoted type. The advantage compares each side's
    /// total material (board plus reserve) in the final position, so promotions count as the
    /// promoted piece's value replacing the pawn's. Replay stops at the first illegal action.
    /// </summary>
    public static MaterialBalance FromHistory(GameState start, IEnumerable<GameAction> actions)
    {
        var byWhite = new List<PieceType>();
        var byBlack = new List<PieceType>();
        var state = start;

        foreach (var action in actions)
        {
            var captured = Material.CapturedBy(state, action);
            var result = Rules.Apply(state, action);
            if (!result.IsSuccess)
            {
                break;
            }

            if (captured is { } piece)
            {
                (piece.Color == Color.Black ? byWhite : byBlack).Add(piece.Type);
            }

            state = result.State;
        }

        return new MaterialBalance(
            Sorted(byWhite),
            Sorted(byBlack),
            Material.Total(state, Color.White) - Material.Total(state, Color.Black));
    }

    private static PieceType[] Sorted(List<PieceType> pieces) =>
        [.. pieces.OrderByDescending(t => t.Value()).ThenByDescending(t => t)];
}
