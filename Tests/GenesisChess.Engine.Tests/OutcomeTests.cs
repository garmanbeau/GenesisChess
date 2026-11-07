using GenesisChess.Engine;
using static GenesisChess.Engine.Tests.TestPositions;

namespace GenesisChess.Engine.Tests;

public class OutcomeTests
{
    [Fact]
    public void Initial_position_is_ongoing() => Assert.Null(Outcomes.Evaluate(GameState.Initial()));

    [Fact]
    public void Back_rank_mate_is_checkmate()
    {
        var state = State("R5k1/5ppp/8/8/8/8/8/6K1", Color.Black);

        Assert.Equal(GameOutcome.WinFor(Color.White, OutcomeReason.Checkmate), Outcomes.Evaluate(state));
    }

    [Fact]
    public void Check_that_can_be_blocked_by_placement_is_not_mate()
    {
        var state = State("R5k1/5ppp/8/8/8/8/8/6K1", Color.Black, black: Reserve.Of((PieceType.Knight, 1)));

        Assert.Null(Outcomes.Evaluate(state));
    }

    [Fact]
    public void Contact_check_cannot_be_blocked_so_reserve_does_not_help()
    {
        var state = State("6k1/5pQp/7P/8/8/8/8/6K1", Color.Black, black: Reserve.Initial);

        Assert.Equal(OutcomeReason.Checkmate, Outcomes.Evaluate(state)!.Reason);
    }

    [Fact]
    public void Placement_can_deliver_checkmate()
    {
        // Black king h8 boxed in by its own rook and pawn; a queen dropped on h4 mates down the h-file.
        var state = State("6rk/6p1/8/8/8/8/8/K7", white: Reserve.Of((PieceType.Queen, 1)));

        var mated = Play(state, Place(PieceType.Queen, "h4"));

        Assert.Equal(GameOutcome.WinFor(Color.White, OutcomeReason.Checkmate), Outcomes.Evaluate(mated));
    }

    [Fact]
    public void No_moves_and_no_placements_without_check_is_stalemate()
    {
        var state = State("k7/2Q5/1K6/8/8/8/8/8", Color.Black);

        Assert.Equal(GameOutcome.Draw(OutcomeReason.Stalemate), Outcomes.Evaluate(state));
    }

    [Fact]
    public void Having_a_legal_placement_prevents_stalemate()
    {
        var state = State("k7/2Q5/1K6/8/8/8/8/8", Color.Black, black: Reserve.Of((PieceType.Pawn, 1)));

        Assert.Null(Outcomes.Evaluate(state));
    }

    [Fact]
    public void Bare_kings_with_empty_reserves_is_insufficient_material()
    {
        var state = State("4k3/8/8/8/8/8/8/4K3");

        Assert.Equal(GameOutcome.Draw(OutcomeReason.InsufficientMaterial), Outcomes.Evaluate(state));
    }

    [Fact]
    public void King_and_minor_vs_king_is_insufficient_material()
    {
        Assert.Equal(OutcomeReason.InsufficientMaterial, Outcomes.Evaluate(State("4k3/8/8/8/8/8/8/2B1K3"))!.Reason);
    }

    [Fact]
    public void Bare_kings_with_a_reserve_piece_play_on()
    {
        var state = State("4k3/8/8/8/8/8/8/4K3", white: Reserve.Of((PieceType.Pawn, 1)));

        Assert.Null(Outcomes.Evaluate(state));
    }

    [Fact]
    public void Hundred_halfmoves_without_progress_is_a_draw()
    {
        var state = State("4k3/8/8/8/8/8/8/R3K3") with { HalfmoveClock = 100 };

        Assert.Equal(OutcomeReason.FiftyMoveRule, Outcomes.Evaluate(state)!.Reason);
    }

    [Fact]
    public void Threefold_repetition_is_a_draw()
    {
        // Kings and rook off their home squares, so castling rights never change the key.
        var state = State("3k4/8/8/8/8/R7/8/3K4");
        GameAction[] cycle = [Move("a3", "a4"), Move("d8", "c8"), Move("a4", "a3"), Move("c8", "d8")];

        state = Play(state, [.. cycle]);
        Assert.Null(Outcomes.Evaluate(state));

        state = Play(state, [.. cycle]);
        Assert.Equal(OutcomeReason.ThreefoldRepetition, Outcomes.Evaluate(state)!.Reason);
    }

    [Fact]
    public void Same_board_with_different_reserves_is_not_a_repetition()
    {
        var withPawn = State("3k4/8/8/8/8/R7/8/3K4", white: Reserve.Of((PieceType.Pawn, 1)));
        GameAction[] cycle = [Move("a3", "a4"), Move("d8", "c8"), Move("a4", "a3"), Move("c8", "d8")];

        var state = Play(withPawn, [.. cycle, .. cycle]);
        state = state with { WhiteReserve = Reserve.Empty };

        Assert.DoesNotContain(state.PositionKey, state.PositionHistory);
        Assert.Null(Outcomes.Evaluate(state));
    }

    [Theory]
    [InlineData("4k3/8/8/8/8/8/8/4K3", false)]
    [InlineData("4k3/8/8/8/8/8/8/3NK3", false)]
    [InlineData("4k3/8/8/8/8/8/8/2NNK3", true)]
    [InlineData("4k3/8/8/8/8/8/4P3/4K3", true)]
    public void Can_ever_mate_depends_on_board_material(string placement, bool expected)
    {
        Assert.Equal(expected, Outcomes.CanEverMate(State(placement), Color.White));
    }

    [Fact]
    public void Can_ever_mate_with_reserve_pieces()
    {
        var state = State("4k3/8/8/8/8/8/8/4K3", white: Reserve.Of((PieceType.Knight, 1)));

        Assert.True(Outcomes.CanEverMate(state, Color.White));
    }
}
