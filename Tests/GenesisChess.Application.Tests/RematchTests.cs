using GenesisChess.Application;
using GenesisChess.Clock;
using GenesisChess.Engine;
using Microsoft.Extensions.Time.Testing;

namespace GenesisChess.Application.Tests;

public class RematchTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly InMemoryGameStore _store = new();

    private GameService Service => new(_store, _time);

    /// <summary>A finished game with both players present on the page.</summary>
    private (GameSession Session, string White, string Black) FinishedGame(TimeControl? timeControl = null)
    {
        var created = Service.CreateGame(timeControl ?? TimeControl.Untimed, ColorChoice.White);
        var session = created.Session;
        var black = session.Join(null).Token!;
        session.Connect(created.Token);
        session.Connect(black);
        Assert.True(session.Resign(black).Succeeded);
        return (session, created.Token, black);
    }

    private GameSession RematchOf(GameSession session)
    {
        var id = session.GetView(null).RematchGameId;
        Assert.NotNull(id);
        return _store.Find(id)!;
    }

    [Fact]
    public void Accepted_offer_starts_a_new_game_with_colors_swapped_and_same_time_control()
    {
        var timeControl = new TimeControl(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(3));
        var (session, white, black) = FinishedGame(timeControl);

        Assert.True(Service.OfferRematch(session, white).Succeeded);
        Assert.Equal(Color.White, session.GetView(black).RematchOfferedBy);
        Assert.Null(session.GetView(black).RematchGameId);

        Assert.True(Service.OfferRematch(session, black).Succeeded);

        var rematch = RematchOf(session);
        Assert.NotEqual(session.Id, rematch.Id);
        Assert.Equal(timeControl, rematch.TimeControl);
        Assert.Equal(Seat.Black, rematch.SeatOf(white));
        Assert.Equal(Seat.White, rematch.SeatOf(black));
        Assert.Equal(GameStatus.InProgress, rematch.Status);
        Assert.Null(session.GetView(white).RematchOfferedBy);
    }

    [Fact]
    public void Both_players_clicking_rematch_starts_it_once()
    {
        var (session, white, black) = FinishedGame();

        Service.OfferRematch(session, black);
        Service.OfferRematch(session, white);
        var again = Service.OfferRematch(session, black);

        Assert.False(again.Succeeded);
        Assert.Equal(2, _store.All().Count());
        Assert.Equal(Seat.White, RematchOf(session).SeatOf(black));
    }

    [Fact]
    public void Colors_alternate_across_several_rematches()
    {
        var (first, white, black) = FinishedGame();
        var games = new List<GameSession> { first };

        for (var i = 0; i < 3; i++)
        {
            var game = games[^1];
            if (i > 0)
            {
                game.Connect(white);
                game.Connect(black);
                Assert.True(game.Resign(white).Succeeded);
            }

            Assert.True(Service.OfferRematch(game, white).Succeeded);
            Assert.True(Service.OfferRematch(game, black).Succeeded);
            games.Add(RematchOf(game));
        }

        Assert.Equal(
            [Seat.White, Seat.Black, Seat.White, Seat.Black],
            games.Select(g => g.SeatOf(white)));
    }

    [Fact]
    public void Decline_clears_the_offer_for_both_sides()
    {
        var (session, white, black) = FinishedGame();
        Service.OfferRematch(session, white);

        Assert.False(session.DeclineRematch(white).Succeeded); // Can't decline your own offer.
        Assert.True(session.DeclineRematch(black).Succeeded);

        Assert.Null(session.GetView(white).RematchOfferedBy);
        Assert.Null(session.GetView(black).RematchOfferedBy);
        Assert.Single(_store.All());
    }

    [Fact]
    public void Cancel_clears_the_offer_for_both_sides()
    {
        var (session, white, black) = FinishedGame();
        Service.OfferRematch(session, white);

        Assert.False(session.CancelRematch(black).Succeeded); // Can't cancel the opponent's offer.
        Assert.True(session.CancelRematch(white).Succeeded);

        Assert.Null(session.GetView(white).RematchOfferedBy);
        Assert.Null(session.GetView(black).RematchOfferedBy);

        // A fresh offer after cancelling still needs the opponent to accept.
        Assert.True(Service.OfferRematch(session, black).Succeeded);
        Assert.Null(session.GetView(null).RematchGameId);
    }

    [Fact]
    public void Rematch_cannot_be_offered_while_the_game_is_in_progress()
    {
        var created = Service.CreateGame(TimeControl.Untimed, ColorChoice.White);
        var black = created.Session.Join(null).Token!;
        created.Session.Connect(created.Token);
        created.Session.Connect(black);

        Assert.False(Service.OfferRematch(created.Session, created.Token).Succeeded);
    }

    [Fact]
    public void Spectators_cannot_offer_a_rematch()
    {
        var (session, _, _) = FinishedGame();

        Assert.False(Service.OfferRematch(session, null).Succeeded);
        Assert.False(Service.OfferRematch(session, "stranger").Succeeded);
    }

    [Fact]
    public void Cannot_offer_when_the_opponent_has_left()
    {
        var created = Service.CreateGame(TimeControl.Untimed, ColorChoice.White);
        var session = created.Session;
        var white = created.Token;
        var black = session.Join(null).Token!;
        session.Connect(white);
        var blackPage = session.Connect(black);
        session.Resign(black);

        blackPage.Dispose();

        Assert.False(session.GetView(white).OpponentConnected);
        var result = Service.OfferRematch(session, white);
        Assert.False(result.Succeeded);
        Assert.Equal("Your opponent has left.", result.Error);
    }

    [Fact]
    public void Leaving_withdraws_your_pending_offer()
    {
        var created = Service.CreateGame(TimeControl.Untimed, ColorChoice.White);
        var session = created.Session;
        var white = created.Token;
        var black = session.Join(null).Token!;
        var whitePage = session.Connect(white);
        session.Connect(black);
        session.Resign(black);
        Service.OfferRematch(session, white);

        whitePage.Dispose();
        whitePage.Dispose(); // Idempotent.

        var view = session.GetView(black);
        Assert.Null(view.RematchOfferedBy);
        Assert.False(view.OpponentConnected);
    }

    [Fact]
    public void Presence_counts_every_open_page_of_a_player()
    {
        var created = Service.CreateGame(TimeControl.Untimed, ColorChoice.White);
        var session = created.Session;
        var black = session.Join(null).Token!;
        var changes = 0;
        session.Changed += _ => changes++;

        var tab1 = session.Connect(black);
        var tab2 = session.Connect(black);
        tab1.Dispose();
        Assert.True(session.GetView(created.Token).OpponentConnected);

        tab2.Dispose();
        Assert.False(session.GetView(created.Token).OpponentConnected);
        Assert.Equal(2, changes); // Arrival and departure only.

        session.Connect(null).Dispose(); // Spectators are not tracked.
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Offer_cancel_decline_and_accept_raise_changed()
    {
        var (session, white, black) = FinishedGame();
        var changes = 0;
        session.Changed += _ => changes++;

        Service.OfferRematch(session, white);
        session.CancelRematch(white);
        Service.OfferRematch(session, black);
        session.DeclineRematch(white);
        Service.OfferRematch(session, white);
        Service.OfferRematch(session, black);

        Assert.Equal(6, changes);
    }
}
