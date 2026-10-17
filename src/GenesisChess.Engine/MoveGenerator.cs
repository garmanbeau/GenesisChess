namespace GenesisChess.Engine;

/// <summary>Generates legal actions for the side to move.</summary>
public static class MoveGenerator
{
    private static readonly PieceType[] PromotionTypes =
        [PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight];

    public static IEnumerable<GameAction> LegalActions(GameState state) =>
        LegalMoves(state).Cast<GameAction>();

    public static IEnumerable<MoveAction> LegalMoves(GameState state) =>
        PseudoLegalMoves(state).Where(m => !Attacks.IsInCheck(Rules.BoardAfter(state, m), state.SideToMove));

    public static IEnumerable<MoveAction> LegalMovesFrom(GameState state, Square from) =>
        LegalMoves(state).Where(m => m.From == from);

    internal static IEnumerable<MoveAction> PseudoLegalMoves(GameState state)
    {
        var color = state.SideToMove;
        foreach (var (from, piece) in state.Board.Pieces(color).ToList())
        {
            var moves = piece.Type switch
            {
                PieceType.Pawn => PawnMoves(state, from, color),
                PieceType.Knight => StepMoves(state.Board, from, color, Attacks.KnightOffsets),
                PieceType.Bishop => SlideMoves(state.Board, from, color, Attacks.BishopDirections),
                PieceType.Rook => SlideMoves(state.Board, from, color, Attacks.RookDirections),
                PieceType.Queen => SlideMoves(state.Board, from, color, [.. Attacks.RookDirections, .. Attacks.BishopDirections]),
                PieceType.King => StepMoves(state.Board, from, color, Attacks.KingOffsets),
                _ => [],
            };

            foreach (var move in moves)
            {
                yield return move;
            }
        }
    }

    private static IEnumerable<MoveAction> PawnMoves(GameState state, Square from, Color color)
    {
        var board = state.Board;
        var forward = color.Forward();

        if (from.Offset(0, forward) is { } one && board.IsEmpty(one))
        {
            foreach (var move in WithPromotions(from, one, color))
            {
                yield return move;
            }

            if (from.Rank == color.PawnRank() && one.Offset(0, forward) is { } two && board.IsEmpty(two))
            {
                yield return new MoveAction(from, two);
            }
        }

        foreach (var fileDelta in new[] { -1, 1 })
        {
            if (from.Offset(fileDelta, forward) is not { } target)
            {
                continue;
            }

            var isCapture = board[target] is { } victim && victim.Color != color;
            if (isCapture || target == state.EnPassantTarget)
            {
                foreach (var move in WithPromotions(from, target, color))
                {
                    yield return move;
                }
            }
        }
    }

    private static IEnumerable<MoveAction> WithPromotions(Square from, Square to, Color color)
    {
        if (to.Rank != color.PromotionRank())
        {
            yield return new MoveAction(from, to);
            yield break;
        }

        foreach (var type in PromotionTypes)
        {
            yield return new MoveAction(from, to, type);
        }
    }

    private static IEnumerable<MoveAction> StepMoves(Board board, Square from, Color color, (int File, int Rank)[] offsets)
    {
        foreach (var (f, r) in offsets)
        {
            if (from.Offset(f, r) is { } to && board[to]?.Color != color)
            {
                yield return new MoveAction(from, to);
            }
        }
    }

    private static IEnumerable<MoveAction> SlideMoves(Board board, Square from, Color color, (int File, int Rank)[] directions)
    {
        foreach (var (f, r) in directions)
        {
            var current = from.Offset(f, r);
            while (current is { } to)
            {
                if (board[to] is { } blocker)
                {
                    if (blocker.Color != color)
                    {
                        yield return new MoveAction(from, to);
                    }

                    break;
                }

                yield return new MoveAction(from, to);
                current = to.Offset(f, r);
            }
        }
    }
}
