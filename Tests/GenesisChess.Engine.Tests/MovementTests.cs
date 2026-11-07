using GenesisChess.Engine;
using static GenesisChess.Engine.Tests.TestPositions;

namespace GenesisChess.Engine.Tests;

public class MovementTests
{
    [Fact]
    public void Knight_in_center_has_eight_moves()
    {
        var state = State("4k3/8/8/8/3N4/8/8/4K3");

        Assert.Equal(["b3", "b5", "c2", "c6", "e2", "e6", "f3", "f5"], TargetsFrom(state, "d4").Order());
    }

    [Fact]
    public void Rook_stops_at_own_piece_and_captures_enemy()
    {
        var state = State("4k3/8/8/8/R2p4/8/8/N3K3");

        Assert.Equal(["a2", "a3", "a5", "a6", "a7", "a8", "b4", "c4", "d4"], TargetsFrom(state, "a4").Order());
    }

    [Fact]
    public void Queen_combines_rook_and_bishop_lines()
    {
        var state = State("4k3/8/8/8/8/8/8/Q3K3");

        Assert.Equal(17, TargetsFrom(state, "a1").Count);
    }

    [Fact]
    public void Pawn_double_steps_only_from_start_rank()
    {
        var state = State("4k3/8/8/8/8/3P4/4P3/4K3");

        Assert.Equal(["e3", "e4"], TargetsFrom(state, "e2").Order());
        Assert.Equal(["d4"], TargetsFrom(state, "d3"));
    }

    [Fact]
    public void Pawn_is_blocked_by_any_piece_ahead()
    {
        var state = State("4k3/8/8/8/8/4p3/4P3/4K3");

        Assert.Empty(TargetsFrom(state, "e2"));
    }

    [Fact]
    public void Pawn_captures_diagonally_only()
    {
        var state = State("4k3/8/8/3p1n2/4P3/8/8/4K3");

        Assert.Equal(["d5", "e5", "f5"], TargetsFrom(state, "e4").Order());
    }

    [Fact]
    public void Pinned_piece_cannot_leave_the_pin_line()
    {
        var state = State("4r1k1/8/8/8/8/8/4B3/4K3");

        Assert.Empty(TargetsFrom(state, "e2"));
    }

    [Fact]
    public void King_cannot_step_into_attack()
    {
        var state = State("3rk3/8/8/8/8/8/8/4K3");

        Assert.DoesNotContain("d1", TargetsFrom(state, "e1"));
        Assert.DoesNotContain("d2", TargetsFrom(state, "e1"));
        Assert.Contains("f2", TargetsFrom(state, "e1"));
    }

    [Fact]
    public void Kings_may_not_stand_adjacent()
    {
        var state = State("8/8/8/3k4/8/3K4/8/8");

        Assert.DoesNotContain("d4", TargetsFrom(state, "d3"));
        Assert.DoesNotContain("e4", TargetsFrom(state, "d3"));
    }

    [Fact]
    public void In_check_only_resolving_moves_are_legal()
    {
        var checking = State("4r1k1/8/8/8/8/8/8/R3K3");

        var moves = MoveGenerator.LegalMoves(checking).Select(m => m.ToString()).ToHashSet();

        Assert.Contains("e1d1", moves);
        Assert.Contains("e1f2", moves);
        Assert.DoesNotContain("a1a2", moves);
        Assert.DoesNotContain("e1e2", moves);
        Assert.Contains("e1d2", moves);
    }

    [Fact]
    public void Applying_a_move_updates_board_and_turn()
    {
        var state = State("4k3/8/8/8/8/8/8/R3K3");

        var next = Play(state, Move("a1", "a5"));

        Assert.Equal("4k3/8/8/R7/8/8/8/4K3", next.Board.ToPlacementString());
        Assert.Equal(Color.Black, next.SideToMove);
        Assert.True(next.Board["a5"]!.Value.HasMoved);
        Assert.Equal(1, next.HalfmoveClock);
        Assert.Equal(2, next.PositionHistory.Count);
    }

    [Fact]
    public void Moving_the_opponents_piece_is_rejected()
    {
        var state = State("4k3/8/8/8/8/8/8/r3K3");

        var result = Rules.Apply(state, Move("a1", "a2"));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Move_leaving_king_in_check_is_rejected()
    {
        var state = State("4r1k1/8/8/8/8/8/4B3/4K3");

        var result = Rules.Apply(state, Move("e2", "d3"));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void En_passant_captures_the_passed_pawn()
    {
        var state = State("4k3/3p4/8/4P3/8/8/8/4K3", Color.Black);

        state = Play(state, Move("d7", "d5"));
        Assert.Equal(Square.Parse("d6"), state.EnPassantTarget);

        state = Play(state, Move("e5", "d6"));
        Assert.Equal("4k3/8/3P4/8/8/8/8/4K3", state.Board.ToPlacementString());
        Assert.Equal(0, state.HalfmoveClock);
    }

    [Fact]
    public void En_passant_expires_after_one_turn()
    {
        var state = State("4k3/3p4/8/4P3/8/8/8/R3K3", Color.Black);

        state = Play(state, Move("d7", "d5"), Move("a1", "a2"), Move("e8", "f8"));

        Assert.DoesNotContain("d6", TargetsFrom(state, "e5"));
    }

    [Fact]
    public void Double_step_without_adjacent_enemy_pawn_sets_no_target()
    {
        var state = Play(State("4k3/3p4/8/8/8/8/8/4K3", Color.Black), Move("d7", "d5"));

        Assert.Null(state.EnPassantTarget);
    }

    [Fact]
    public void Promotion_requires_a_choice()
    {
        var state = State("4k3/P7/8/8/8/8/8/4K3");

        var result = Rules.Apply(state, Move("a7", "a8"));

        Assert.False(result.IsSuccess);
        Assert.Contains("promote", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Promotion_replaces_the_pawn()
    {
        var state = Play(State("4k3/P7/8/8/8/8/8/4K3"), Move("a7", "a8", PieceType.Knight));

        Assert.Equal(new Piece(PieceType.Knight, Color.White, HasMoved: true), state.Board["a8"]);
    }
}
