using GenesisChess.Engine;
using static GenesisChess.Engine.Tests.TestPositions;

namespace GenesisChess.Engine.Tests;

public class CastlingTests
{
    [Fact]
    public void Can_castle_kingside_with_unmoved_king_and_rook()
    {
        var state = Play(State("4k3/8/8/8/8/8/8/4K2R"), Move("e1", "g1"));

        Assert.Equal("4k3/8/8/8/8/8/8/5RK1", state.Board.ToPlacementString());
    }

    [Fact]
    public void Can_castle_queenside()
    {
        var state = Play(State("r3k3/8/8/8/8/8/8/4K3", Color.Black), Move("e8", "c8"));

        Assert.Equal("2kr4/8/8/8/8/8/8/4K3", state.Board.ToPlacementString());
    }

    [Fact]
    public void Cannot_castle_after_the_rook_has_moved()
    {
        var state = Play(
            State("4k3/8/8/8/8/8/8/4K2R"),
            Move("h1", "h2"), Move("e8", "d8"), Move("h2", "h1"), Move("d8", "e8"));

        Assert.DoesNotContain("g1", TargetsFrom(state, "e1"));
    }

    [Fact]
    public void Cannot_castle_after_the_king_has_moved()
    {
        var state = Play(
            State("4k3/8/8/8/8/8/8/4K2R"),
            Move("e1", "f1"), Move("e8", "d8"), Move("f1", "e1"), Move("d8", "e8"));

        Assert.DoesNotContain("g1", TargetsFrom(state, "e1"));
    }

    [Fact]
    public void Cannot_castle_out_of_check()
    {
        var state = State("4r1k1/8/8/8/8/8/8/4K2R");

        Assert.DoesNotContain("g1", TargetsFrom(state, "e1"));
    }

    [Fact]
    public void Cannot_castle_through_an_attacked_square()
    {
        var state = State("5rk1/8/8/8/8/8/8/4K2R");

        Assert.DoesNotContain("g1", TargetsFrom(state, "e1"));
    }

    [Fact]
    public void Cannot_castle_into_check()
    {
        var state = State("6rk/8/8/8/8/8/8/4K2R");

        Assert.DoesNotContain("g1", TargetsFrom(state, "e1"));
    }

    [Fact]
    public void Cannot_castle_through_pieces()
    {
        var state = State("4k3/8/8/8/8/8/8/R2QK3");

        Assert.DoesNotContain("c1", TargetsFrom(state, "e1"));
    }

    [Fact]
    public void Queenside_allows_attacked_b_file_square()
    {
        var state = State("1r2k3/8/8/8/8/8/8/R3K3");

        Assert.Contains("c1", TargetsFrom(state, "e1"));
    }
}
