namespace GenesisChess.Engine;

/// <summary>Validates and applies actions.</summary>
public static class Rules
{
    public static ActionResult Apply(GameState state, GameAction action) => action switch
    {
        MoveAction move => ApplyMove(state, move),
        _ => ActionResult.Failure($"Unsupported action '{action}'."),
    };

    private static ActionResult ApplyMove(GameState state, MoveAction move)
    {
        if (state.Board[move.From] is not { } piece || piece.Color != state.SideToMove)
        {
            return ActionResult.Failure($"No {state.SideToMove} piece on {move.From}.");
        }

        if (!MoveGenerator.LegalMovesFrom(state, move.From).Contains(move))
        {
            var needsPromotion = piece.Type == PieceType.Pawn && move.To.Rank == piece.Color.PromotionRank();
            return ActionResult.Failure(needsPromotion && move.Promotion is null
                ? "Choose a piece to promote to."
                : $"{move} is not a legal move.");
        }

        var isCapture = !state.Board.IsEmpty(move.To) || IsEnPassant(state, move, piece);
        var isPawnMove = piece.Type == PieceType.Pawn;

        var next = state with
        {
            Board = BoardAfter(state, move),
            EnPassantTarget = EnPassantTargetAfter(state, move, piece),
            HalfmoveClock = isCapture || isPawnMove ? 0 : state.HalfmoveClock + 1,
        };

        return ActionResult.Success(EndTurn(next));
    }

    /// <summary>Passes the turn and records the new position.</summary>
    internal static GameState EndTurn(GameState next)
    {
        var mover = next.SideToMove;
        next = next with
        {
            SideToMove = mover.Opponent(),
            FullmoveNumber = mover == Color.Black ? next.FullmoveNumber + 1 : next.FullmoveNumber,
        };
        return next with { PositionHistory = next.PositionHistory.Add(next.PositionKey) };
    }

    /// <summary>The board after a (pseudo-legal) move, including en passant and promotion side effects.</summary>
    internal static Board BoardAfter(GameState state, MoveAction move)
    {
        var piece = state.Board[move.From]!.Value;
        var placed = move.Promotion is { } promotion
            ? new Piece(promotion, piece.Color, HasMoved: true)
            : piece.AsMoved();

        if (IsEnPassant(state, move, piece))
        {
            var captured = new Square(move.To.File, move.From.Rank);
            return state.Board.With((move.From, null), (captured, null), (move.To, placed));
        }

        return state.Board.With((move.From, null), (move.To, placed));
    }

    private static bool IsEnPassant(GameState state, MoveAction move, Piece piece) =>
        piece.Type == PieceType.Pawn && move.To == state.EnPassantTarget && move.From.File != move.To.File;

    /// <summary>
    /// After a double step, the skipped square becomes the en passant target, but only when an
    /// enemy pawn is adjacent and could use it (keeps repetition keys honest).
    /// </summary>
    private static Square? EnPassantTargetAfter(GameState state, MoveAction move, Piece piece)
    {
        if (piece.Type != PieceType.Pawn || Math.Abs(move.To.Rank - move.From.Rank) != 2)
        {
            return null;
        }

        var enemy = piece.Color.Opponent();
        var adjacentEnemyPawn = new[] { move.To.Offset(-1, 0), move.To.Offset(1, 0) }
            .Any(s => s is { } sq && state.Board[sq] is { Type: PieceType.Pawn } p && p.Color == enemy);

        return adjacentEnemyPawn ? new Square(move.From.File, (move.From.Rank + move.To.Rank) / 2) : null;
    }
}
