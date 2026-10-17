namespace GenesisChess.Engine;

/// <summary>One turn: either move a piece on the board or place one from the reserve.</summary>
public abstract record GameAction;

/// <summary>Moves a piece. Castling is expressed as the king moving two files.</summary>
public sealed record MoveAction(Square From, Square To, PieceType? Promotion = null) : GameAction
{
    public override string ToString() => $"{From}{To}{(Promotion is { } p ? char.ToLowerInvariant(p.Letter()).ToString() : string.Empty)}";
}

/// <summary>Places a piece from the side to move's reserve onto an empty square.</summary>
public sealed record PlaceAction(PieceType Type, Square Square) : GameAction
{
    public override string ToString() => $"{Type.Letter()}@{Square}";
}
