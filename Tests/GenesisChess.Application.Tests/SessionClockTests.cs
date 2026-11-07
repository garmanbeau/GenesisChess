using GenesisChess.Application;
using GenesisChess.Clock;
using GenesisChess.Engine;
using Microsoft.Extensions.Time.Testing;

namespace GenesisChess.Application.Tests;

public class SessionClockTests
{
    private readonly FakeTimeProvider _time = new();

    private static PlaceAction Place(PieceType type, string square) => new(type, Square.Parse(square));

    private static MoveAction Move(string from, string to) => new(Square.Parse(from), Square.Parse(to));

    private (GameSession Session, string White, string Black) StartedGame(string timeControl)
    {
        TimeControl.TryParse(timeControl, out var tc);
        var created = new GameService(new InMemoryGameStore(), _time).CreateGame(tc, ColorChoice.White);
        var black = created.Session.Join(null).Token!;
        return (created.Session, created.Token, black);
    }

    [Fact]
    public void Clock_waits_for_whites_first_action()
    {
        var (session, white, _) = StartedGame("1+0");

        _time.Advance(TimeSpan.FromMinutes(5));
        Assert.True(session.Submit(white, Place(PieceType.Knight, "f3")).Succeeded);

        var clock = session.GetView(null).Clock!;
        Assert.Equal(TimeSpan.FromMinutes(1), clock.WhiteRemaining);
        Assert.Equal(ClockSide.Black, clock.Running);
    }

    [Fact]
    public void Each_action_deducts_elapsed_and_adds_increment()
    {
        var (session, white, black) = StartedGame("3+2");
        session.Submit(white, Place(PieceType.Knight, "f3"));

        _time.Advance(TimeSpan.FromSeconds(10));
        session.Submit(black, Place(PieceType.Knight, "f6"));
        _time.Advance(TimeSpan.FromSeconds(4));
        session.Submit(white, Place(PieceType.Knight, "c3"));

        var clock = session.GetView(null).Clock!;
        Assert.Equal(TimeSpan.FromSeconds(172), clock.BlackRemaining);
        Assert.Equal(TimeSpan.FromSeconds(178), clock.WhiteRemaining);
    }

    [Fact]
    public void Flag_timer_ends_the_game_without_any_action()
    {
        var (session, white, _) = StartedGame("1+0");
        var changes = 0;
        session.Submit(white, Place(PieceType.Knight, "f3"));
        session.Changed += _ => changes++;

        _time.Advance(TimeSpan.FromSeconds(61));

        var view = session.GetView(null);
        Assert.Equal(GameStatus.Finished, view.Status);
        Assert.Equal(GameOutcome.WinFor(Color.White, OutcomeReason.Timeout), view.Outcome);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Flag_timer_follows_the_side_to_move()
    {
        var (session, white, black) = StartedGame("1+0");
        session.Submit(white, Place(PieceType.Knight, "f3"));
        _time.Advance(TimeSpan.FromSeconds(30));
        session.Submit(black, Place(PieceType.Knight, "f6"));

        _time.Advance(TimeSpan.FromSeconds(45));
        Assert.Null(session.GetView(null).Outcome);

        _time.Advance(TimeSpan.FromSeconds(16));
        Assert.Equal(GameOutcome.WinFor(Color.Black, OutcomeReason.Timeout), session.GetView(null).Outcome);
    }

    [Fact]
    public void Acting_after_the_flag_fell_loses_on_time()
    {
        var (session, white, black) = StartedGame("1+0");
        session.Submit(white, Place(PieceType.Knight, "f3"));
        session.Dispose(); // Stop the background timer so the late action detects the flag itself.

        _time.Advance(TimeSpan.FromSeconds(70));
        var result = session.Submit(black, Place(PieceType.Knight, "f6"));

        Assert.False(result.Succeeded);
        Assert.Equal(OutcomeReason.Timeout, session.GetView(null).Outcome!.Reason);
    }

    [Fact]
    public void Timeout_is_a_draw_when_the_opponent_can_never_mate()
    {
        // White has king and knight with an empty reserve; Black still has a rook.
        var start = GameState.Create(Board.FromPlacementString("4k3/8/8/8/8/r7/8/3NK3"), Reserve.Empty, Reserve.Empty);
        using var session = new GameSession("t", TimeControl.FromMinutes(1, 0), _time, start);
        var white = session.Claim(Seat.White);
        session.Claim(Seat.Black);

        session.Submit(white, Move("d1", "c3"));
        _time.Advance(TimeSpan.FromSeconds(61));

        Assert.Equal(GameOutcome.Draw(OutcomeReason.TimeoutVsInsufficientMaterial), session.GetView(null).Outcome);
    }

    [Fact]
    public void Untimed_games_never_flag()
    {
        var (session, white, _) = StartedGame("untimed");
        session.Submit(white, Place(PieceType.Knight, "f3"));

        _time.Advance(TimeSpan.FromDays(2));

        Assert.Null(session.GetView(null).Outcome);
        Assert.True(session.GetView(null).Clock!.IsUntimed);
    }

    [Fact]
    public void Clock_stops_when_the_game_ends()
    {
        var (session, white, black) = StartedGame("1+0");
        session.Submit(white, Place(PieceType.Knight, "f3"));
        _time.Advance(TimeSpan.FromSeconds(10));

        session.Resign(black);
        _time.Advance(TimeSpan.FromMinutes(5));

        var view = session.GetView(null);
        Assert.Equal(OutcomeReason.Resignation, view.Outcome!.Reason);
        Assert.Equal(TimeSpan.FromSeconds(50), view.Clock!.BlackRemaining);
        Assert.Null(view.Clock.Running);
    }

    [Fact]
    public void Illegal_action_does_not_press_the_clock()
    {
        var (session, white, black) = StartedGame("1+5");
        session.Submit(white, Place(PieceType.Knight, "f3"));

        Assert.False(session.Submit(black, Move("e8", "e6")).Succeeded);

        Assert.Equal(ClockSide.Black, session.GetView(null).Clock!.Running);
        Assert.Equal(TimeSpan.FromMinutes(1), session.GetView(null).Clock!.BlackRemaining);
    }
}
