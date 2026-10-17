namespace GenesisChess.Engine;

public enum GameResult
{
    WhiteWins,
    BlackWins,
    Draw,
}

public enum OutcomeReason
{
    Checkmate,
    Stalemate,
    FiftyMoveRule,
    ThreefoldRepetition,
    InsufficientMaterial,
    Resignation,
    Timeout,
    TimeoutVsInsufficientMaterial,
    Agreement,
}

/// <summary>How a finished game ended.</summary>
public sealed record GameOutcome(GameResult Result, OutcomeReason Reason)
{
    public static GameOutcome WinFor(Color winner, OutcomeReason reason) =>
        new(winner == Color.White ? GameResult.WhiteWins : GameResult.BlackWins, reason);

    public static GameOutcome Draw(OutcomeReason reason) => new(GameResult.Draw, reason);

    public Color? Winner => Result switch
    {
        GameResult.WhiteWins => Color.White,
        GameResult.BlackWins => Color.Black,
        _ => null,
    };

    public override string ToString() => (Result, Reason) switch
    {
        (GameResult.Draw, OutcomeReason.TimeoutVsInsufficientMaterial) => "Draw: time ran out, but the opponent cannot mate",
        (GameResult.Draw, var reason) => $"Draw by {Describe(reason)}",
        (_, var reason) => $"{Winner} wins by {Describe(reason)}",
    };

    private static string Describe(OutcomeReason reason) => reason switch
    {
        OutcomeReason.Checkmate => "checkmate",
        OutcomeReason.Stalemate => "stalemate",
        OutcomeReason.FiftyMoveRule => "the fifty-move rule",
        OutcomeReason.ThreefoldRepetition => "threefold repetition",
        OutcomeReason.InsufficientMaterial => "insufficient material",
        OutcomeReason.Resignation => "resignation",
        OutcomeReason.Timeout => "timeout",
        OutcomeReason.Agreement => "agreement",
        _ => reason.ToString(),
    };
}
