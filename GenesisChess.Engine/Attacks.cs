namespace GenesisChess.Engine;

/// <summary>Attack and check detection.</summary>
public static class Attacks
{
    internal static readonly (int File, int Rank)[] KnightOffsets =
        [(1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2)];

    internal static readonly (int File, int Rank)[] KingOffsets =
        [(1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1)];

    internal static readonly (int File, int Rank)[] RookDirections = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    internal static readonly (int File, int Rank)[] BishopDirections = [(1, 1), (1, -1), (-1, 1), (-1, -1)];

    public static bool IsInCheck(Board board, Color color) =>
        board.FindKing(color) is { } king && IsAttacked(board, king, color.Opponent());

    /// <summary>Whether any piece of <paramref name="by"/> attacks <paramref name="target"/>.</summary>
    public static bool IsAttacked(Board board, Square target, Color by)
    {
        // Pawns attack diagonally forward, so look one rank "backwards" from the target.
        var pawnRank = -by.Forward();
        if (IsPiece(board, target.Offset(-1, pawnRank), by, PieceType.Pawn)
            || IsPiece(board, target.Offset(1, pawnRank), by, PieceType.Pawn))
        {
            return true;
        }

        foreach (var (f, r) in KnightOffsets)
        {
            if (IsPiece(board, target.Offset(f, r), by, PieceType.Knight))
            {
                return true;
            }
        }

        foreach (var (f, r) in KingOffsets)
        {
            if (IsPiece(board, target.Offset(f, r), by, PieceType.King))
            {
                return true;
            }
        }

        return SlidingAttack(board, target, by, RookDirections, PieceType.Rook)
            || SlidingAttack(board, target, by, BishopDirections, PieceType.Bishop);
    }

    private static bool SlidingAttack(
        Board board, Square target, Color by, (int File, int Rank)[] directions, PieceType slider)
    {
        foreach (var (f, r) in directions)
        {
            var current = target.Offset(f, r);
            while (current is { } square)
            {
                if (board[square] is { } piece)
                {
                    if (piece.Color == by && (piece.Type == slider || piece.Type == PieceType.Queen))
                    {
                        return true;
                    }

                    break;
                }

                current = square.Offset(f, r);
            }
        }

        return false;
    }

    private static bool IsPiece(Board board, Square? square, Color color, PieceType type) =>
        square is { } s && board[s] is { } piece && piece.Color == color && piece.Type == type;
}
