using GenesisChess.Application;
using GenesisChess.Clock;
using GenesisChess.Engine;
using Microsoft.Extensions.Time.Testing;

namespace GenesisChess.Application.Tests;

public class GameTimelineTests
{
    private static MoveAction Move(string from, string to, PieceType? promotion = null) =>
        new(Square.Parse(from), Square.Parse(to), promotion);

    private static PlaceAction Place(PieceType type, string square) => new(type, Square.Parse(square));

    private static GameState Position(string placement, Color sideToMove = Color.White) =>
        GameState.Create(Board.FromPlacementString(placement), Reserve.Empty, Reserve.Empty, sideToMove);

    /// <summary>Plays the actions for real to produce the move records a session would hold.</summary>
    private static List<MoveRecord> Record(GameState initial, params GameAction[] actions)
    {
        var records = new List<MoveRecord>();
        var state = initial;
        foreach (var action in actions)
        {
            var result = Rules.Apply(state, action);
            Assert.True(result.IsSuccess, $"{action} failed: {result.Error}");
            records.Add(new MoveRecord(records.Count + 1, state.SideToMove, action, Notation.ToSan(state, action)));
            state = result.State;
        }

        return records;
    }

    private static GameTimeline Timeline(GameState initial, params GameAction[] actions)
    {
        var timeline = new GameTimeline(initial);
        timeline.Sync(Record(initial, actions));
        return timeline;
    }

    [Fact]
    public void Ply_zero_is_the_initial_state()
    {
        var initial = GameState.Initial();
        var timeline = Timeline(initial, Place(PieceType.Pawn, "e4"));

        Assert.Same(initial, timeline.StateAt(0));
        Assert.Null(timeline.ActionAt(0));
        Assert.Equal(1, timeline.LatestPly);
    }

    [Fact]
    public void Placements_change_the_reserves_ply_by_ply()
    {
        var timeline = Timeline(
            GameState.Initial(),
            Place(PieceType.Pawn, "e4"),
            Place(PieceType.Knight, "c6"),
            Place(PieceType.Queen, "d1"));

        Assert.Equal(Reserve.Initial, timeline.StateAt(0).WhiteReserve);
        Assert.Equal(Reserve.Initial, timeline.StateAt(0).BlackReserve);

        var afterPawn = timeline.StateAt(1);
        Assert.Equal(7, afterPawn.WhiteReserve.Count(PieceType.Pawn));
        Assert.Equal(Reserve.Initial, afterPawn.BlackReserve);
        Assert.Equal(new Piece(PieceType.Pawn, Color.White), afterPawn.Board["e4"]);
        Assert.Equal(Color.Black, afterPawn.SideToMove);

        var afterKnight = timeline.StateAt(2);
        Assert.Equal(1, afterKnight.BlackReserve.Count(PieceType.Knight));
        Assert.Equal(7, afterKnight.WhiteReserve.Count(PieceType.Pawn));

        var afterQueen = timeline.StateAt(3);
        Assert.Equal(0, afterQueen.WhiteReserve.Count(PieceType.Queen));
        Assert.Equal(PieceType.Queen, afterQueen.Board["d1"]!.Value.Type);
        Assert.Equal(Place(PieceType.Queen, "d1"), timeline.ActionAt(3));

        // Earlier states are untouched by later ones.
        Assert.Null(timeline.StateAt(1).Board["d1"]);
        Assert.Equal(1, timeline.StateAt(1).WhiteReserve.Count(PieceType.Queen));
    }

    [Fact]
    public void Captures_remove_the_piece_and_earlier_plies_still_have_it()
    {
        var timeline = Timeline(
            GameState.Initial(),
            Place(PieceType.Pawn, "e4"),
            Place(PieceType.Pawn, "d5"),
            Move("e4", "d5"));

        Assert.Equal(new Piece(PieceType.Pawn, Color.Black), timeline.StateAt(2).Board["d5"]);
        Assert.Equal(Color.White, timeline.StateAt(3).Board["d5"]!.Value.Color);
        Assert.Null(timeline.StateAt(3).Board["e4"]);
        Assert.Equal(0, timeline.StateAt(3).HalfmoveClock);

        // Captured pieces don't return to the reserve.
        Assert.Equal(7, timeline.StateAt(3).BlackReserve.Count(PieceType.Pawn));
    }

    [Theory]
    [InlineData(PieceType.Queen)]
    [InlineData(PieceType.Knight)]
    public void Promotion_is_rebuilt_with_the_chosen_piece(PieceType promotion)
    {
        var timeline = Timeline(Position("4k3/P7/8/8/8/8/8/4K3"), Move("a7", "a8", promotion));

        Assert.Equal(PieceType.Pawn, timeline.StateAt(0).Board["a7"]!.Value.Type);
        Assert.Null(timeline.StateAt(1).Board["a7"]);
        Assert.Equal(new Piece(promotion, Color.White, HasMoved: true), timeline.StateAt(1).Board["a8"]);
    }

    [Fact]
    public void En_passant_target_and_capture_are_rebuilt()
    {
        var timeline = Timeline(
            Position("4k3/3p4/8/4P3/8/8/8/4K3", Color.Black),
            Move("d7", "d5"),
            Move("e5", "d6"));

        Assert.Equal(Square.Parse("d6"), timeline.StateAt(1).EnPassantTarget);
        Assert.Equal(PieceType.Pawn, timeline.StateAt(1).Board["d5"]!.Value.Type);

        var afterCapture = timeline.StateAt(2);
        Assert.Null(afterCapture.Board["d5"]);
        Assert.Null(afterCapture.Board["e5"]);
        Assert.Equal(new Piece(PieceType.Pawn, Color.White, HasMoved: true), afterCapture.Board["d6"]);
        Assert.Null(afterCapture.EnPassantTarget);
    }

    [Fact]
    public void Castling_moves_both_pieces_and_earlier_plies_keep_the_rights()
    {
        var timeline = Timeline(
            Position("r3k3/8/8/8/8/8/8/4K2R"),
            Move("e1", "g1"),
            Move("e8", "c8"));

        Assert.Equal("Kq", timeline.StateAt(0).CastlingAvailability);
        Assert.Equal("r3k3/8/8/8/8/8/8/5RK1", timeline.StateAt(1).Board.ToPlacementString());
        Assert.Equal("q", timeline.StateAt(1).CastlingAvailability);
        Assert.Equal("2kr4/8/8/8/8/8/8/5RK1", timeline.StateAt(2).Board.ToPlacementString());
        Assert.Equal("-", timeline.StateAt(2).CastlingAvailability);
    }

    [Fact]
    public void A_placed_rook_can_castle_and_the_replay_matches()
    {
        var timeline = Timeline(
            GameState.Initial(),
            Place(PieceType.Rook, "h1"),
            Place(PieceType.Pawn, "d5"),
            Move("e1", "g1"));

        Assert.Equal(1, timeline.StateAt(1).WhiteReserve.Count(PieceType.Rook));
        Assert.Equal("K", timeline.StateAt(1).CastlingAvailability);
        Assert.Equal(new Piece(PieceType.King, Color.White, HasMoved: true), timeline.StateAt(3).Board["g1"]);
        Assert.Equal(PieceType.Rook, timeline.StateAt(3).Board["f1"]!.Value.Type);
        Assert.Null(timeline.StateAt(3).Board["h1"]);
    }

    [Fact]
    public void Sync_extends_with_new_moves_and_keeps_cached_states()
    {
        var initial = GameState.Initial();
        var records = Record(initial, Place(PieceType.Pawn, "e4"), Place(PieceType.Pawn, "e5"), Place(PieceType.Knight, "f3"));
        var timeline = new GameTimeline(initial);

        timeline.Sync(records[..2]);
        var cached = timeline.StateAt(2);
        timeline.Sync(records);

        Assert.Equal(3, timeline.LatestPly);
        Assert.Same(cached, timeline.StateAt(2));
        Assert.Equal(PieceType.Knight, timeline.StateAt(3).Board["f3"]!.Value.Type);
    }

    [Fact]
    public void Sync_with_a_different_history_rebuilds_from_where_it_diverges()
    {
        var initial = GameState.Initial();
        var timeline = new GameTimeline(initial);
        timeline.Sync(Record(initial, Place(PieceType.Pawn, "e4"), Place(PieceType.Pawn, "e5")));
        Assert.NotNull(timeline.StateAt(2).Board["e5"]);

        timeline.Sync(Record(initial, Place(PieceType.Pawn, "e4"), Place(PieceType.Pawn, "c5")));

        Assert.Null(timeline.StateAt(2).Board["e5"]);
        Assert.NotNull(timeline.StateAt(2).Board["c5"]);
    }

    [Fact]
    public void Out_of_range_plies_throw()
    {
        var timeline = Timeline(GameState.Initial(), Place(PieceType.Pawn, "e4"));

        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.StateAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.StateAt(2));
    }

    [Fact]
    public void An_unreplayable_move_throws_with_the_ply()
    {
        var timeline = new GameTimeline(GameState.Initial());
        timeline.Sync([new MoveRecord(1, Color.White, Move("e1", "e5"), "Ke5")]);

        var ex = Assert.Throws<InvalidOperationException>(() => timeline.StateAt(1));
        Assert.Contains("Ply 1", ex.Message);
    }

    [Fact]
    public void Replaying_a_live_session_reproduces_every_position()
    {
        var time = new FakeTimeProvider();
        var created = new GameService(new InMemoryGameStore(), time).CreateGame(TimeControl.Untimed, ColorChoice.White);
        var session = created.Session;
        var black = session.Join(null).Token;

        GameAction[] actions =
        [
            Place(PieceType.Pawn, "e4"), Place(PieceType.Pawn, "d5"),
            Move("e4", "d5"), Place(PieceType.Queen, "d8"),
            Place(PieceType.Rook, "h1"), Move("d8", "d5"),
            Move("e1", "g1"),
        ];
        for (var i = 0; i < actions.Length; i++)
        {
            var result = session.Submit(i % 2 == 0 ? created.Token : black, actions[i]);
            Assert.True(result.Succeeded, $"{actions[i]} failed: {result.Error}");
        }

        var view = session.GetView(null);
        var timeline = new GameTimeline(view.InitialState);
        timeline.Sync(view.Moves);

        Assert.Equal(actions.Length, timeline.LatestPly);
        Assert.Equal(view.State.PositionKey, timeline.StateAt(timeline.LatestPly).PositionKey);
        Assert.Equal(
            view.State.PositionHistory,
            Enumerable.Range(0, timeline.LatestPly + 1).Select(ply => timeline.StateAt(ply).PositionKey));
    }
}
