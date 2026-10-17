using GenesisChess.Application;
using GenesisChess.Clock;
using GenesisChess.Engine;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace GenesisChess.Application.Tests;

public class GameSessionTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly InMemoryGameStore _store = new();

    private GameService Service => new(_store, _time);

    private static MoveAction Move(string from, string to) => new(Square.Parse(from), Square.Parse(to));

    private static PlaceAction Place(PieceType type, string square) => new(type, Square.Parse(square));

    private (GameSession Session, string White, string Black) StartedGame(TimeControl? timeControl = null)
    {
        var created = Service.CreateGame(timeControl ?? TimeControl.Untimed, ColorChoice.White);
        var black = created.Session.Join(null);
        Assert.Equal(Seat.Black, black.Seat);
        return (created.Session, created.Token, black.Token!);
    }

    [Theory]
    [InlineData(ColorChoice.White, Seat.White)]
    [InlineData(ColorChoice.Black, Seat.Black)]
    public void Creator_gets_the_chosen_seat(ColorChoice choice, Seat expected)
    {
        var created = Service.CreateGame(TimeControl.Untimed, choice);

        Assert.Equal(expected, created.Seat);
        Assert.Equal(expected, created.Session.SeatOf(created.Token));
        Assert.Same(created.Session, _store.Find(created.Session.Id));
    }

    [Fact]
    public void Second_visitor_takes_open_seat_and_third_spectates()
    {
        var created = Service.CreateGame(TimeControl.Untimed, ColorChoice.Black);
        Assert.Equal(GameStatus.WaitingForOpponent, created.Session.Status);

        var second = created.Session.Join(null);
        var third = created.Session.Join("not-a-token");

        Assert.Equal(Seat.White, second.Seat);
        Assert.Equal(Seat.Spectator, third.Seat);
        Assert.Null(third.Token);
        Assert.Equal(GameStatus.InProgress, created.Session.Status);
    }

    [Fact]
    public void Rejoining_with_a_token_keeps_the_seat()
    {
        var (session, white, black) = StartedGame();

        Assert.Equal(new JoinResult(Seat.White, white), session.Join(white));
        Assert.Equal(new JoinResult(Seat.Black, black), session.Join(black));
    }

    [Fact]
    public void Cannot_play_before_opponent_joins()
    {
        var created = Service.CreateGame(TimeControl.Untimed, ColorChoice.White);

        var result = created.Session.Submit(created.Token, Place(PieceType.Queen, "d1"));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Players_alternate_and_moves_are_recorded()
    {
        var (session, white, black) = StartedGame();

        Assert.True(session.Submit(white, Place(PieceType.Knight, "f3")).Succeeded);
        Assert.False(session.Submit(white, Place(PieceType.Knight, "c3")).Succeeded);
        Assert.True(session.Submit(black, Move("e8", "d8")).Succeeded);

        var view = session.GetView(white);
        Assert.Equal(["N@f3", "Kd8"], view.Moves.Select(m => m.San));
        Assert.Equal(Move("e8", "d8"), view.LastAction);
        Assert.True(view.IsYourTurn);
    }

    [Fact]
    public void Spectators_and_strangers_cannot_play()
    {
        var (session, _, _) = StartedGame();

        Assert.False(session.Submit(null, Place(PieceType.Knight, "f3")).Succeeded);
        Assert.False(session.Submit("forged", Place(PieceType.Knight, "f3")).Succeeded);
    }

    [Fact]
    public void Illegal_action_is_rejected_with_reason()
    {
        var (session, white, _) = StartedGame();

        var result = session.Submit(white, Place(PieceType.Knight, "f6"));

        Assert.False(result.Succeeded);
        Assert.Contains("own half", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void View_lists_legal_actions_only_on_your_turn()
    {
        var (session, white, black) = StartedGame();

        Assert.NotEmpty(session.GetView(white).LegalPlacements);
        Assert.NotEmpty(session.GetView(white).LegalMoves);
        Assert.Empty(session.GetView(black).LegalPlacements);
        Assert.Empty(session.GetView(null).LegalMoves);
        Assert.Equal(Seat.Spectator, session.GetView(null).Seat);
    }

    [Fact]
    public void Resignation_ends_the_game()
    {
        var (session, white, black) = StartedGame();

        Assert.True(session.Resign(black).Succeeded);

        var view = session.GetView(white);
        Assert.Equal(GameStatus.Finished, view.Status);
        Assert.Equal(GameOutcome.WinFor(Color.White, OutcomeReason.Resignation), view.Outcome);
        Assert.False(session.Submit(white, Place(PieceType.Queen, "d1")).Succeeded);
    }

    [Fact]
    public void Draw_offer_accepted_by_opponent_ends_in_draw()
    {
        var (session, white, black) = StartedGame();

        session.OfferDraw(white);
        Assert.Equal(Color.White, session.GetView(black).DrawOfferedBy);

        session.OfferDraw(black);

        Assert.Equal(GameOutcome.Draw(OutcomeReason.Agreement), session.GetView(null).Outcome);
    }

    [Fact]
    public void Draw_offer_is_declined_explicitly_or_by_playing_on()
    {
        var (session, white, black) = StartedGame();

        session.OfferDraw(white);
        Assert.True(session.DeclineDraw(black).Succeeded);
        Assert.Null(session.GetView(null).DrawOfferedBy);

        session.OfferDraw(white);
        session.Submit(white, Place(PieceType.Knight, "f3"));
        Assert.Equal(Color.White, session.GetView(null).DrawOfferedBy);
        session.Submit(black, Place(PieceType.Knight, "f6"));
        Assert.Null(session.GetView(null).DrawOfferedBy);
    }

    [Fact]
    public void Checkmate_finishes_the_game()
    {
        var (session, white, black) = StartedGame();

        // A protected contact check cannot be blocked by placement: Qe7# backed by the a3 bishop.
        GameAction[] script =
        [
            Place(PieceType.Bishop, "a3"), Place(PieceType.Pawn, "a6"),
            Place(PieceType.Queen, "h4"), Place(PieceType.Pawn, "b6"),
            Move("h4", "e7"),
        ];

        for (var i = 0; i < script.Length; i++)
        {
            var result = session.Submit(i % 2 == 0 ? white : black, script[i]);
            Assert.True(result.Succeeded, $"{script[i]}: {result.Error}");
        }

        var view = session.GetView(null);
        Assert.Equal(GameStatus.Finished, view.Status);
        Assert.Equal(OutcomeReason.Checkmate, view.Outcome!.Reason);
        Assert.EndsWith("#", view.Moves[^1].San, StringComparison.Ordinal);
    }

    [Fact]
    public void Changed_fires_after_each_successful_command_only()
    {
        var (session, white, black) = StartedGame();
        var count = 0;
        session.Changed += _ => count++;

        session.Submit(white, Place(PieceType.Knight, "f3"));
        session.Submit(white, Place(PieceType.Knight, "c3"));
        session.Resign(black);

        Assert.Equal(2, count);
    }

    [Fact]
    public void Failing_subscriber_does_not_break_others()
    {
        var (session, white, _) = StartedGame();
        var called = false;
        session.Changed += _ => throw new InvalidOperationException("dead circuit");
        session.Changed += _ => called = true;

        Assert.True(session.Submit(white, Place(PieceType.Knight, "f3")).Succeeded);
        Assert.True(called);
    }

    [Fact]
    public void Cleanup_removes_only_idle_sessions()
    {
        var options = Options.Create(new GameOptions { IdleTimeout = TimeSpan.FromHours(1) });
        var cleanup = new GameCleanupService(_store, _time, options);
        var stale = Service.CreateGame(TimeControl.Untimed, ColorChoice.White).Session;

        _time.Advance(TimeSpan.FromMinutes(50));
        var (fresh, white, _) = StartedGame();
        _time.Advance(TimeSpan.FromMinutes(20));
        fresh.Submit(white, Place(PieceType.Knight, "f3"));

        Assert.Equal(1, cleanup.RemoveIdle());
        Assert.Null(_store.Find(stale.Id));
        Assert.NotNull(_store.Find(fresh.Id));
    }
}
