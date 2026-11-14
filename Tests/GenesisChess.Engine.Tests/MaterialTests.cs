using GenesisChess.Engine;
using static GenesisChess.Engine.Tests.TestPositions;

namespace GenesisChess.Engine.Tests;

public class MaterialTests
{
    [Fact]
    public void Standard_piece_values()
    {
        Assert.Equal(1, PieceType.Pawn.Value());
        Assert.Equal(3, PieceType.Knight.Value());
        Assert.Equal(3, PieceType.Bishop.Value());
        Assert.Equal(5, PieceType.Rook.Value());
        Assert.Equal(9, PieceType.Queen.Value());
        Assert.Equal(0, PieceType.King.Value());
    }

    [Fact]
    public void Initial_position_is_even_with_nothing_captured()
    {
        var balance = MaterialBalance.FromHistory(GameState.Initial(), []);

        Assert.Empty(balance.CapturedByWhite);
        Assert.Empty(balance.CapturedByBlack);
        Assert.Equal(0, balance.WhiteAdvantage);
    }

    [Fact]
    public void Placements_alone_do_not_change_the_balance()
    {
        // Pieces still in reserve count, so White placing a queen first is not "+9".
        var balance = MaterialBalance.FromHistory(GameState.Initial(), [Place(PieceType.Queen, "d1")]);

        Assert.Equal(0, balance.WhiteAdvantage);
        Assert.Empty(balance.CapturedByWhite);
    }

    [Fact]
    public void Capture_is_credited_to_the_capturing_side()
    {
        var balance = MaterialBalance.FromHistory(GameState.Initial(),
        [
            Place(PieceType.Rook, "d3"),
            Place(PieceType.Knight, "d6"),
            Move("d3", "d6"),
        ]);

        Assert.Equal([PieceType.Knight], balance.CapturedByWhite);
        Assert.Empty(balance.CapturedByBlack);
        Assert.Equal(3, balance.WhiteAdvantage);
        Assert.Equal(3, balance.Advantage(Color.White));
        Assert.Equal(-3, balance.Advantage(Color.Black));
    }

    [Fact]
    public void Captured_pieces_are_listed_most_valuable_first()
    {
        // White: Ra1, Ke1. Black: pawn a2, knight b3, Ke8 (White is +1 to start).
        var start = State("4k3/8/8/8/8/1n6/p7/R3K3");

        var balance = MaterialBalance.FromHistory(start,
        [
            Move("a1", "a2"),   // Rxa2
            Move("e8", "d7"),
            Move("a2", "a3"),
            Move("d7", "d6"),
            Move("a3", "b3"),   // Rxb3
        ]);

        Assert.Equal([PieceType.Knight, PieceType.Pawn], balance.CapturedByWhite);
        Assert.Empty(balance.CapturedByBlack);
        Assert.Equal(5, balance.WhiteAdvantage);
    }

    [Fact]
    public void Captures_by_both_sides_net_out()
    {
        // White: Rc3, Ke1. Black: Qd6, pawn b4, Ke8.
        var start = State("4k3/8/3q4/8/1p6/2R5/8/4K3");

        var balance = MaterialBalance.FromHistory(start,
        [
            Move("c3", "b3"),
            Move("d6", "d4"),
            Move("b3", "b4"),   // Rxb4
            Move("d4", "b4"),   // Qxb4+
        ]);

        Assert.Equal([PieceType.Pawn], balance.CapturedByWhite);
        Assert.Equal([PieceType.Rook], balance.CapturedByBlack);
        Assert.Equal(-9, balance.WhiteAdvantage);
        Assert.Equal(9, balance.Advantage(Color.Black));
    }

    [Fact]
    public void Equal_trade_leaves_material_even()
    {
        // White: Nc3, pawn d2. Black: Nd5, pawn e6.
        var start = State("4k3/8/4p3/3n4/8/2N5/3P4/4K3");

        var balance = MaterialBalance.FromHistory(start,
        [
            Move("c3", "d5"),   // Nxd5
            Move("e6", "d5"),   // exd5
        ]);

        Assert.Equal([PieceType.Knight], balance.CapturedByWhite);
        Assert.Equal([PieceType.Knight], balance.CapturedByBlack);
        Assert.Equal(0, balance.WhiteAdvantage);
    }

    [Fact]
    public void Promotion_replaces_the_pawn_value_with_the_new_piece()
    {
        var start = State("4k3/P7/8/8/8/8/8/4K3");

        var balance = MaterialBalance.FromHistory(start, [Move("a7", "a8", PieceType.Queen)]);

        // Start: White +1 (one pawn). After promotion: White +9.
        Assert.Equal(9, balance.WhiteAdvantage);
        Assert.Empty(balance.CapturedByWhite);
    }

    [Fact]
    public void Capture_promotion_counts_capture_and_promotion()
    {
        var start = State("1r2k3/P7/8/8/8/8/8/4K3");

        var balance = MaterialBalance.FromHistory(start, [Move("a7", "b8", PieceType.Knight)]);

        Assert.Equal([PieceType.Rook], balance.CapturedByWhite);
        Assert.Equal(3, balance.WhiteAdvantage);
    }

    [Fact]
    public void Captured_promoted_piece_counts_as_its_promoted_type()
    {
        var start = State("4k3/P7/8/8/8/8/r7/4K3");

        var balance = MaterialBalance.FromHistory(start,
        [
            Move("a7", "a8", PieceType.Queen),
            Move("a2", "a8"),   // Rxa8 takes the new queen
        ]);

        Assert.Equal([PieceType.Queen], balance.CapturedByBlack);
        Assert.Empty(balance.CapturedByWhite);
        Assert.Equal(-5, balance.WhiteAdvantage);
    }

    [Fact]
    public void En_passant_captures_the_passed_pawn()
    {
        var start = State("4k3/3p4/8/4P3/8/8/8/4K3", sideToMove: Color.Black);

        var balance = MaterialBalance.FromHistory(start,
        [
            Move("d7", "d5"),
            Move("e5", "d6"),   // exd6 e.p.
        ]);

        Assert.Equal([PieceType.Pawn], balance.CapturedByWhite);
        Assert.Equal(1, balance.WhiteAdvantage);
    }

    [Fact]
    public void Replay_stops_at_an_illegal_action()
    {
        var balance = MaterialBalance.FromHistory(GameState.Initial(), [Move("a1", "a8")]);

        Assert.Equal(MaterialBalance.Empty.WhiteAdvantage, balance.WhiteAdvantage);
        Assert.Empty(balance.CapturedByWhite);
    }
}
