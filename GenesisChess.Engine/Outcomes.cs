namespace GenesisChess.Engine;

/// <summary>Detects positions that end the game on the board.</summary>
public static class Outcomes
{
    /// <summary>The board-decided outcome of the position, or null while the game goes on.</summary>
    public static GameOutcome? Evaluate(GameState state)
    {
        var mover = state.SideToMove;
        if (!MoveGenerator.LegalActions(state).Any())
        {
            // Stalemate needs no move AND no placement; any legal placement keeps the game alive.
            return Attacks.IsInCheck(state.Board, mover)
                ? GameOutcome.WinFor(mover.Opponent(), OutcomeReason.Checkmate)
                : GameOutcome.Draw(OutcomeReason.Stalemate);
        }

        if (!CanEverMate(state, Color.White) && !CanEverMate(state, Color.Black))
        {
            return GameOutcome.Draw(OutcomeReason.InsufficientMaterial);
        }

        if (state.HalfmoveClock >= 100)
        {
            return GameOutcome.Draw(OutcomeReason.FiftyMoveRule);
        }

        var key = state.PositionKey;
        if (state.PositionHistory.Count(k => k == key) >= 3)
        {
            return GameOutcome.Draw(OutcomeReason.ThreefoldRepetition);
        }

        return null;
    }

    /// <summary>
    /// Whether <paramref name="color"/> still has material that could deliver mate.
    /// Any reserve piece counts; on the board a lone king or king plus one minor piece cannot.
    /// Used both for insufficient-material draws and for deciding a loss on time.
    /// </summary>
    public static bool CanEverMate(GameState state, Color color)
    {
        if (!state.ReserveOf(color).IsEmpty)
        {
            return true;
        }

        var pieces = state.Board.Pieces(color).Where(p => p.Piece.Type != PieceType.King).ToList();
        return pieces.Count switch
        {
            0 => false,
            1 => pieces[0].Piece.Type is not (PieceType.Knight or PieceType.Bishop),
            _ => true,
        };
    }
}
