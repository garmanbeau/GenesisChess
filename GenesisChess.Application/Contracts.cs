using GenesisChess.Clock;
using GenesisChess.Engine;

namespace GenesisChess.Application;

/// <summary>Where a viewer sits in a game.</summary>
public enum Seat
{
    White,
    Black,
    Spectator,
}

/// <summary>The color the game creator wants to play.</summary>
public enum ColorChoice
{
    White,
    Black,
    Random,
}

public enum GameStatus
{
    WaitingForOpponent,
    InProgress,
    Finished,
}

/// <summary>One played turn, in order.</summary>
public sealed record MoveRecord(int Ply, Color By, GameAction Action, string San);

/// <summary>Result of a command against a session.</summary>
public sealed record CommandResult(bool Succeeded, string? Error = null)
{
    public static CommandResult Ok { get; } = new(true);

    public static CommandResult Fail(string error) => new(false, error);
}

/// <summary>Seat and token a viewer holds after joining.</summary>
public sealed record JoinResult(Seat Seat, string? Token);

/// <summary>A newly created game together with the creator's player token.</summary>
public sealed record CreatedGame(GameSession Session, string Token, Seat Seat);

/// <summary>
/// Everything a client needs to render one viewer's perspective of a game. Engine value types
/// are part of the contract; the client never calls engine rules itself.
/// </summary>
public sealed record GameView
{
    public required string Id { get; init; }

    public required TimeControl TimeControl { get; init; }

    public required GameStatus Status { get; init; }

    public required Seat Seat { get; init; }

    public required GameState State { get; init; }

    public GameOutcome? Outcome { get; init; }

    public bool InCheck { get; init; }

    public required IReadOnlyList<MoveRecord> Moves { get; init; }

    public bool WhiteSeated { get; init; }

    public bool BlackSeated { get; init; }

    public Color? DrawOfferedBy { get; init; }

    /// <summary>Legal moves for this viewer; empty unless it is their turn.</summary>
    public IReadOnlyList<MoveAction> LegalMoves { get; init; } = [];

    /// <summary>Legal placements for this viewer; empty unless it is their turn.</summary>
    public IReadOnlyList<PlaceAction> LegalPlacements { get; init; } = [];

    public ClockSnapshot? Clock { get; init; }

    public GameAction? LastAction => Moves.Count > 0 ? Moves[^1].Action : null;

    public Color? SeatColor => Seat switch
    {
        Seat.White => Color.White,
        Seat.Black => Color.Black,
        _ => null,
    };

    public bool IsYourTurn => Status == GameStatus.InProgress && SeatColor == State.SideToMove;
}
