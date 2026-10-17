using System.Collections.Immutable;

namespace GenesisChess.Engine;

/// <summary>A complete, immutable snapshot of a Genesis Chess game.</summary>
public sealed record GameState
{
    public required Board Board { get; init; }

    public required Reserve WhiteReserve { get; init; }

    public required Reserve BlackReserve { get; init; }

    public Color SideToMove { get; init; } = Color.White;

    /// <summary>Square a pawn may capture onto en passant this turn, if any.</summary>
    public Square? EnPassantTarget { get; init; }

    /// <summary>Half-moves since the last capture, pawn move or placement (50-move rule).</summary>
    public int HalfmoveClock { get; init; }

    public int FullmoveNumber { get; init; } = 1;

    /// <summary>Position keys of every position reached so far, including this one.</summary>
    public ImmutableList<string> PositionHistory { get; init; } = [];

    public static Square KingStart(Color color) => new(4, color.BackRank());

    /// <summary>Kings on e1/e8, every other piece in reserve, White to move.</summary>
    public static GameState Initial()
    {
        var board = Board.Empty.With(
            (KingStart(Color.White), new Piece(PieceType.King, Color.White)),
            (KingStart(Color.Black), new Piece(PieceType.King, Color.Black)));

        return Create(board, Reserve.Initial, Reserve.Initial);
    }

    /// <summary>Builds a state from arbitrary parts, seeding the repetition history.</summary>
    public static GameState Create(
        Board board,
        Reserve whiteReserve,
        Reserve blackReserve,
        Color sideToMove = Color.White,
        Square? enPassantTarget = null)
    {
        var state = new GameState
        {
            Board = board,
            WhiteReserve = whiteReserve,
            BlackReserve = blackReserve,
            SideToMove = sideToMove,
            EnPassantTarget = enPassantTarget,
        };

        return state with { PositionHistory = [state.PositionKey] };
    }

    public Reserve ReserveOf(Color color) => color == Color.White ? WhiteReserve : BlackReserve;

    public GameState WithReserve(Color color, Reserve reserve) =>
        color == Color.White ? this with { WhiteReserve = reserve } : this with { BlackReserve = reserve };

    /// <summary>
    /// Castling availability derived from the board: king and corner rook both unmoved.
    /// Returned in FEN order ("KQkq"), or "-" when none.
    /// </summary>
    public string CastlingAvailability
    {
        get
        {
            var rights = string.Concat(
                CanStillCastle(Color.White, kingside: true) ? "K" : string.Empty,
                CanStillCastle(Color.White, kingside: false) ? "Q" : string.Empty,
                CanStillCastle(Color.Black, kingside: true) ? "k" : string.Empty,
                CanStillCastle(Color.Black, kingside: false) ? "q" : string.Empty);
            return rights.Length == 0 ? "-" : rights;
        }
    }

    /// <summary>Whether the king and the rook on the given corner have never moved (ignores attacks and blockers).</summary>
    public bool CanStillCastle(Color color, bool kingside)
    {
        var king = Board[KingStart(color)];
        var rook = Board[new Square(kingside ? 7 : 0, color.BackRank())];
        return king is { Type: PieceType.King, HasMoved: false } && king.Value.Color == color
            && rook is { Type: PieceType.Rook, HasMoved: false } && rook.Value.Color == color;
    }

    /// <summary>
    /// Identity of the position for repetition: placement, side to move, castling,
    /// en passant and both reserves.
    /// </summary>
    public string PositionKey =>
        $"{Board.ToPlacementString()} {(SideToMove == Color.White ? 'w' : 'b')} {CastlingAvailability} " +
        $"{EnPassantTarget?.ToString() ?? "-"} {WhiteReserve.ToKey()} {BlackReserve.ToKey()}";
}
