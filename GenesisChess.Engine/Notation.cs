using System.Text;

namespace GenesisChess.Engine;

/// <summary>Standard algebraic notation for moves, and "N@f3" style for placements.</summary>
public static class Notation
{
    /// <summary>Formats a legal action played from <paramref name="before"/>.</summary>
    public static string ToSan(GameState before, GameAction action)
    {
        var result = Rules.Apply(before, action);
        if (!result.IsSuccess)
        {
            throw new ArgumentException(result.Error, nameof(action));
        }

        var sb = new StringBuilder(action switch
        {
            MoveAction move => MoveText(before, move),
            PlaceAction place => $"{place.Type.Letter()}@{place.Square}",
            _ => action.ToString(),
        });

        var after = result.State;
        if (Attacks.IsInCheck(after.Board, after.SideToMove))
        {
            sb.Append(MoveGenerator.LegalActions(after).Any() ? '+' : '#');
        }

        return sb.ToString();
    }

    private static string MoveText(GameState before, MoveAction move)
    {
        var piece = before.Board[move.From]!.Value;
        if (Rules.IsCastling(move, piece))
        {
            return move.To.File > move.From.File ? "O-O" : "O-O-O";
        }

        var isCapture = !before.Board.IsEmpty(move.To) || (piece.Type == PieceType.Pawn && move.From.File != move.To.File);
        var sb = new StringBuilder();

        if (piece.Type == PieceType.Pawn)
        {
            if (isCapture)
            {
                sb.Append(move.From.FileLetter);
            }
        }
        else
        {
            sb.Append(piece.Type.Letter());
            sb.Append(Disambiguation(before, move, piece));
        }

        if (isCapture)
        {
            sb.Append('x');
        }

        sb.Append(move.To);

        if (move.Promotion is { } promotion)
        {
            sb.Append('=').Append(promotion.Letter());
        }

        return sb.ToString();
    }

    private static string Disambiguation(GameState before, MoveAction move, Piece piece)
    {
        var rivals = MoveGenerator.LegalMoves(before)
            .Where(m => m.To == move.To && m.From != move.From && before.Board[m.From]?.Type == piece.Type)
            .Select(m => m.From)
            .ToList();

        if (rivals.Count == 0)
        {
            return string.Empty;
        }

        if (rivals.All(r => r.File != move.From.File))
        {
            return move.From.FileLetter.ToString();
        }

        if (rivals.All(r => r.Rank != move.From.Rank))
        {
            return (move.From.Rank + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return move.From.ToString();
    }
}
