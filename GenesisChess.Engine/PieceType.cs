namespace GenesisChess.Engine;

public enum PieceType
{
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King,
}

public static class PieceTypeExtensions
{
    /// <summary>Upper-case letter used in notation (P, N, B, R, Q, K).</summary>
    public static char Letter(this PieceType type) => type switch
    {
        PieceType.Pawn => 'P',
        PieceType.Knight => 'N',
        PieceType.Bishop => 'B',
        PieceType.Rook => 'R',
        PieceType.Queen => 'Q',
        PieceType.King => 'K',
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static PieceType FromLetter(char letter) => char.ToUpperInvariant(letter) switch
    {
        'P' => PieceType.Pawn,
        'N' => PieceType.Knight,
        'B' => PieceType.Bishop,
        'R' => PieceType.Rook,
        'Q' => PieceType.Queen,
        'K' => PieceType.King,
        _ => throw new ArgumentOutOfRangeException(nameof(letter), letter, "Unknown piece letter."),
    };
}
