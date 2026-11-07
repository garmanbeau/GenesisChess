namespace GenesisChess.Engine;

/// <summary>Generates legal actions for the side to move.</summary>
public static class MoveGenerator
{
    private static readonly PieceType[] PromotionTypes =
        [PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight];

    public static IEnumerable<GameAction> LegalActions(GameState state) =>
        LegalMoves(state).Cast<GameAction>().Concat(LegalPlacements(state));

    /// <summary>
    /// Placements from the reserve: any empty square on the mover's own half, pawns never on
    /// the mover's back rank, and never leaving the mover's king in check.
    /// </summary>
    public static IEnumerable<PlaceAction> LegalPlacements(GameState state) =>
        state.ReserveOf(state.SideToMove).Available.SelectMany(type => LegalPlacements(state, type));

    public static IEnumerable<PlaceAction> LegalPlacements(GameState state, PieceType type)
    {
        var color = state.SideToMove;
        if (state.ReserveOf(color).Count(type) == 0)
        {
            yield break;
        }

        foreach (var square in Square.All)
        {
            if (Rules.PlacementError(state, type, square) is null)
            {
                yield return new PlaceAction(type, square);
            }
        }
    }

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
                PieceType.King => StepMoves(state.Board, from, color, Attacks.KingOffsets).Concat(CastlingMoves(state, color)),
                _ => [],
            };

            foreach (var move in moves)
            {
                yield return move;
            }
        }
    }

    /// <summary>
    /// Castling with a rook that was placed on its corner and has never moved, under the usual
    /// conditions: empty path, king not in check and not passing through an attacked square.
    /// Landing in check is filtered by the general legality check.
    /// </summary>
    private static IEnumerable<MoveAction> CastlingMoves(GameState state, Color color)
    {
        var king = GameState.KingStart(color);
        var rank = color.BackRank();
        var enemy = color.Opponent();

        foreach (var kingside in new[] { true, false })
        {
            if (!state.CanStillCastle(color, kingside))
            {
                continue;
            }

            int[] between = kingside ? [5, 6] : [1, 2, 3];
            if (!between.All(f => state.Board.IsEmpty(new Square(f, rank))))
            {
                continue;
            }

            var passing = new Square(kingside ? 5 : 3, rank);
            if (Attacks.IsAttacked(state.Board, king, enemy) || Attacks.IsAttacked(state.Board, passing, enemy))
            {
                continue;
            }

            yield return new MoveAction(king, new Square(kingside ? 6 : 2, rank));
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
