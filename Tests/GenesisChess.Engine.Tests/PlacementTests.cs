using GenesisChess.Engine;
using static GenesisChess.Engine.Tests.TestPositions;

namespace GenesisChess.Engine.Tests;

public class PlacementTests
{
    [Fact]
    public void Placing_moves_piece_from_reserve_to_board()
    {
        var state = Play(GameState.Initial(), Place(PieceType.Queen, "d1"));

        Assert.Equal(new Piece(PieceType.Queen, Color.White), state.Board["d1"]);
        Assert.Equal(0, state.WhiteReserve.Count(PieceType.Queen));
        Assert.Equal(15, state.BlackReserve.Total);
        Assert.Equal(Color.Black, state.SideToMove);
    }

    [Theory]
    [InlineData(Color.White, "d4", true)]
    [InlineData(Color.White, "d5", false)]
    [InlineData(Color.Black, "d5", true)]
    [InlineData(Color.Black, "d4", false)]
    public void Placement_is_limited_to_own_half(Color side, string square, bool legal)
    {
        var state = GameState.Initial() with { SideToMove = side };

        Assert.Equal(legal, Rules.Apply(state, Place(PieceType.Knight, square)).IsSuccess);
    }

    [Theory]
    [InlineData(Color.White, "a1", false)]
    [InlineData(Color.White, "a2", true)]
    [InlineData(Color.Black, "a8", false)]
    [InlineData(Color.Black, "a7", true)]
    public void Pawns_cannot_be_placed_on_own_back_rank(Color side, string square, bool legal)
    {
        var state = GameState.Initial() with { SideToMove = side };

        Assert.Equal(legal, Rules.Apply(state, Place(PieceType.Pawn, square)).IsSuccess);
    }

    [Fact]
    public void Cannot_place_on_an_occupied_square()
    {
        var result = Rules.Apply(GameState.Initial(), Place(PieceType.Rook, "e1"));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Cannot_place_a_piece_not_in_reserve()
    {
        var state = State("4k3/8/8/8/8/8/8/4K3", white: Reserve.Of((PieceType.Pawn, 1)));

        Assert.False(Rules.Apply(state, Place(PieceType.Queen, "d1")).IsSuccess);
    }

    [Fact]
    public void Kings_cannot_be_placed()
    {
        Assert.False(Rules.Apply(GameState.Initial(), Place(PieceType.King, "a1")).IsSuccess);
    }

    [Fact]
    public void Initial_position_offers_every_reserve_type_on_every_empty_own_square()
    {
        var placements = MoveGenerator.LegalPlacements(GameState.Initial()).ToList();

        // 31 empty squares on ranks 1-4; pawns lose the 7 empty back-rank squares.
        Assert.Equal((4 * 31) + 24, placements.Count);
    }

    [Fact]
    public void Placement_can_block_a_check()
    {
        var state = State("4r1k1/8/8/8/8/8/8/4K3", white: Reserve.Of((PieceType.Bishop, 1)));

        var placements = MoveGenerator.LegalPlacements(state).Select(p => p.Square.ToString()).ToHashSet();

        Assert.Equal(["e2", "e3", "e4"], placements.Order());
    }

    [Fact]
    public void Placement_that_ignores_check_is_rejected()
    {
        var state = State("4r1k1/8/8/8/8/8/8/4K3", white: Reserve.Of((PieceType.Bishop, 1)));

        Assert.False(Rules.Apply(state, Place(PieceType.Bishop, "a3")).IsSuccess);
    }

    [Fact]
    public void Placed_piece_may_give_check()
    {
        var state = State("4k3/8/8/8/8/8/8/K7", white: Reserve.Of((PieceType.Rook, 1)));

        var next = Play(state, Place(PieceType.Rook, "e1"));

        Assert.True(Attacks.IsInCheck(next.Board, Color.Black));
    }

    [Fact]
    public void Placement_resets_the_fifty_move_counter()
    {
        var state = State("4k3/8/8/8/8/8/8/K7", white: Reserve.Of((PieceType.Rook, 1))) with { HalfmoveClock = 40 };

        Assert.Equal(0, Play(state, Place(PieceType.Rook, "b1")).HalfmoveClock);
    }

    [Fact]
    public void Placed_pawn_on_second_rank_can_double_step()
    {
        var state = Play(GameState.Initial(), Place(PieceType.Pawn, "d2"), Place(PieceType.Pawn, "a7"));

        Assert.Contains("d4", TargetsFrom(state, "d2"));
    }

    [Fact]
    public void Rook_placed_on_its_corner_can_castle()
    {
        var state = State("4k3/8/8/8/8/8/8/4K3", white: Reserve.Of((PieceType.Rook, 1)), black: Reserve.Of((PieceType.Knight, 1)));

        state = Play(state, Place(PieceType.Rook, "h1"), Place(PieceType.Knight, "b8"), Move("e1", "g1"));

        Assert.Equal("1n2k3/8/8/8/8/8/8/5RK1", state.Board.ToPlacementString());
    }

    [Fact]
    public void Rook_placed_off_its_corner_then_moved_there_cannot_castle()
    {
        var state = State("4k3/8/8/8/8/8/8/4K3", white: Reserve.Of((PieceType.Rook, 1)), black: Reserve.Of((PieceType.Knight, 1)));

        state = Play(state, Place(PieceType.Rook, "h3"), Place(PieceType.Knight, "b8"), Move("h3", "h1"), Move("b8", "c6"));

        Assert.DoesNotContain("g1", TargetsFrom(state, "e1"));
    }
}
