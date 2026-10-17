using System.Text;

namespace GenesisChess.Engine;

/// <summary>
/// 8x8 piece layout. Treated as immutable once published inside a <see cref="GameState"/>;
/// mutation goes through <see cref="With"/> or a private clone in the engine.
/// </summary>
public sealed class Board
{
    private readonly Piece?[] _squares;

    private Board(Piece?[] squares) => _squares = squares;

    public static Board Empty => new(new Piece?[64]);

    public Piece? this[Square square] => _squares[square.Index];

    public Piece? this[string square] => this[Square.Parse(square)];

    public bool IsEmpty(Square square) => _squares[square.Index] is null;

    /// <summary>Returns a copy with the given squares set (null clears a square).</summary>
    public Board With(params ReadOnlySpan<(Square Square, Piece? Piece)> changes)
    {
        var copy = (Piece?[])_squares.Clone();
        foreach (var (square, piece) in changes)
        {
            copy[square.Index] = piece;
        }

        return new Board(copy);
    }

    public IEnumerable<(Square Square, Piece Piece)> Pieces()
    {
        for (var i = 0; i < 64; i++)
        {
            if (_squares[i] is { } piece)
            {
                yield return (Square.FromIndex(i), piece);
            }
        }
    }

    public IEnumerable<(Square Square, Piece Piece)> Pieces(Color color) => Pieces().Where(p => p.Piece.Color == color);

    public Square? FindKing(Color color)
    {
        for (var i = 0; i < 64; i++)
        {
            if (_squares[i] is { Type: PieceType.King } piece && piece.Color == color)
            {
                return Square.FromIndex(i);
            }
        }

        return null;
    }

    /// <summary>FEN piece-placement field, e.g. "4k3/8/8/8/8/8/8/4K3".</summary>
    public string ToPlacementString()
    {
        var sb = new StringBuilder();
        for (var rank = 7; rank >= 0; rank--)
        {
            var empty = 0;
            for (var file = 0; file < 8; file++)
            {
                if (_squares[(rank * 8) + file] is { } piece)
                {
                    if (empty > 0)
                    {
                        sb.Append(empty);
                        empty = 0;
                    }

                    sb.Append(piece.Symbol);
                }
                else
                {
                    empty++;
                }
            }

            if (empty > 0)
            {
                sb.Append(empty);
            }

            if (rank > 0)
            {
                sb.Append('/');
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Parses a FEN piece-placement field. Pieces are marked as moved unless they stand on a
    /// castling corner or king start square; use <see cref="With"/> for finer control in tests.
    /// </summary>
    public static Board FromPlacementString(string placement)
    {
        var squares = new Piece?[64];
        var ranks = placement.Split('/');
        if (ranks.Length != 8)
        {
            throw new FormatException("Placement must have 8 ranks.");
        }

        for (var r = 0; r < 8; r++)
        {
            var rank = 7 - r;
            var file = 0;
            foreach (var c in ranks[r])
            {
                if (char.IsDigit(c))
                {
                    file += c - '0';
                    continue;
                }

                if (file > 7)
                {
                    throw new FormatException($"Rank {rank + 1} is too long.");
                }

                var color = char.IsUpper(c) ? Color.White : Color.Black;
                var square = new Square(file, rank);
                var type = PieceTypeExtensions.FromLetter(c);
                squares[square.Index] = new Piece(type, color, HasMoved: !IsHomeSquare(type, color, square));
                file++;
            }

            if (file != 8)
            {
                throw new FormatException($"Rank {rank + 1} does not have 8 files.");
            }
        }

        return new Board(squares);
    }

    private static bool IsHomeSquare(PieceType type, Color color, Square square) => type switch
    {
        PieceType.King => square == GameState.KingStart(color),
        PieceType.Rook => square.Rank == color.BackRank() && square.File is 0 or 7,
        PieceType.Pawn => square.Rank == color.PawnRank(),
        _ => false,
    };

    public override string ToString() => ToPlacementString();
}
