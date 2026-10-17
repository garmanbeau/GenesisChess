using GenesisChess.Engine;

namespace GenesisChess.Engine.Tests;

public class CoreTypesTests
{
    [Theory]
    [InlineData("a1", 0, 0)]
    [InlineData("e4", 4, 3)]
    [InlineData("h8", 7, 7)]
    public void Square_parses_and_formats(string text, int file, int rank)
    {
        var square = Square.Parse(text);

        Assert.Equal(file, square.File);
        Assert.Equal(rank, square.Rank);
        Assert.Equal(text, square.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("i1")]
    [InlineData("a9")]
    [InlineData("e44")]
    public void Square_rejects_invalid_text(string text) => Assert.False(Square.TryParse(text, out _));

    [Fact]
    public void Square_offset_returns_null_off_board()
    {
        Assert.Null(Square.Parse("h8").Offset(1, 0));
        Assert.Equal(Square.Parse("f6"), Square.Parse("e4").Offset(1, 2));
    }

    [Fact]
    public void Initial_state_has_only_kings_and_full_reserves()
    {
        var state = GameState.Initial();

        Assert.Equal("4k3/8/8/8/8/8/8/4K3", state.Board.ToPlacementString());
        Assert.Equal(15, state.WhiteReserve.Total);
        Assert.Equal(8, state.BlackReserve.Count(PieceType.Pawn));
        Assert.Equal(1, state.BlackReserve.Count(PieceType.Queen));
        Assert.Equal(Color.White, state.SideToMove);
        Assert.Single(state.PositionHistory);
    }

    [Fact]
    public void Reserve_remove_decrements_and_throws_when_empty()
    {
        var reserve = Reserve.Initial.Remove(PieceType.Queen);

        Assert.Equal(0, reserve.Count(PieceType.Queen));
        Assert.Equal(14, reserve.Total);
        Assert.Throws<InvalidOperationException>(() => reserve.Remove(PieceType.Queen));
        Assert.Equal(1, Reserve.Initial.Count(PieceType.Queen));
    }

    [Fact]
    public void Reserves_with_equal_counts_are_equal()
    {
        Assert.Equal(Reserve.Of((PieceType.Pawn, 2)), Reserve.Of((PieceType.Pawn, 3)).Remove(PieceType.Pawn));
    }

    [Fact]
    public void Board_round_trips_placement_string()
    {
        const string placement = "r3k2r/8/8/3q4/8/8/PP6/R3K2R";

        Assert.Equal(placement, Board.FromPlacementString(placement).ToPlacementString());
    }

    [Fact]
    public void Castling_availability_requires_unmoved_king_and_rook()
    {
        var board = Board.FromPlacementString("r3k2r/8/8/8/8/8/8/R3K2R");
        var state = GameState.Create(board, Reserve.Empty, Reserve.Empty);

        Assert.Equal("KQkq", state.CastlingAvailability);

        var h1 = Square.Parse("h1");
        var movedRook = state with { Board = board.With((h1, board[h1]!.Value.AsMoved())) };
        Assert.Equal("Qkq", movedRook.CastlingAvailability);
    }

    [Fact]
    public void Position_key_includes_reserves()
    {
        var a = GameState.Initial();
        var b = a.WithReserve(Color.White, a.WhiteReserve.Remove(PieceType.Pawn));

        Assert.NotEqual(a.PositionKey, b.PositionKey);
    }
}
