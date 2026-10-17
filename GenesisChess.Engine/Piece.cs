namespace GenesisChess.Engine;

/// <summary>A piece on the board. <see cref="HasMoved"/> tracks castling eligibility after placement.</summary>
public readonly record struct Piece(PieceType Type, Color Color, bool HasMoved = false)
{
    /// <summary>FEN-style symbol: upper case for White, lower case for Black.</summary>
    public char Symbol => Color == Color.White ? Type.Letter() : char.ToLowerInvariant(Type.Letter());

    public Piece AsMoved() => this with { HasMoved = true };

    public override string ToString() => Symbol.ToString();
}
