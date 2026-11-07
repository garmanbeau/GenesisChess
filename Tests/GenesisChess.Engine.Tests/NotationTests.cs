using GenesisChess.Engine;
using static GenesisChess.Engine.Tests.TestPositions;

namespace GenesisChess.Engine.Tests;

public class NotationTests
{
    [Fact]
    public void Placement_uses_at_sign() =>
        Assert.Equal("N@f3", Notation.ToSan(GameState.Initial(), Place(PieceType.Knight, "f3")));

    [Fact]
    public void Placement_giving_check_is_marked() =>
        Assert.Equal("R@e1+", Notation.ToSan(State("4k3/8/8/8/8/8/8/K7", white: Reserve.Of((PieceType.Rook, 1))), Place(PieceType.Rook, "e1")));

    [Fact]
    public void Placement_mate_is_marked() =>
        Assert.Equal("Q@h4#", Notation.ToSan(State("6rk/6p1/8/8/8/8/8/K7", white: Reserve.Of((PieceType.Queen, 1))), Place(PieceType.Queen, "h4")));

    [Fact]
    public void Pawn_push_and_capture()
    {
        var state = State("4k3/8/8/3p4/4P3/8/8/4K3");

        Assert.Equal("e5", Notation.ToSan(state, Move("e4", "e5")));
        Assert.Equal("exd5", Notation.ToSan(state, Move("e4", "d5")));
    }

    [Fact]
    public void Piece_capture_and_promotion()
    {
        Assert.Equal("Rxa8+", Notation.ToSan(State("r3k3/8/8/8/8/8/8/R3K3"), Move("a1", "a8")));
        Assert.Equal("b8=Q+", Notation.ToSan(State("4k3/1P6/8/8/8/8/8/4K3"), Move("b7", "b8", PieceType.Queen)));
    }

    [Fact]
    public void Castling() =>
        Assert.Equal("O-O", Notation.ToSan(State("4k3/8/8/8/8/8/8/4K2R"), Move("e1", "g1")));

    [Fact]
    public void Ambiguous_knights_are_disambiguated_by_file() =>
        Assert.Equal("Nbd2", Notation.ToSan(State("4k3/8/8/8/8/8/8/1N2KN2"), Move("b1", "d2")));

    [Fact]
    public void Ambiguous_rooks_on_same_file_use_rank() =>
        Assert.Equal("R1a4", Notation.ToSan(State("R7/8/8/7k/8/8/8/R3K3"), Move("a1", "a4")));
}
