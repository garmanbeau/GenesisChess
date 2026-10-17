namespace GenesisChess.Engine;

public enum Color
{
    White,
    Black,
}

public static class ColorExtensions
{
    public static Color Opponent(this Color color) => color == Color.White ? Color.Black : Color.White;

    /// <summary>Direction pawns of this color advance in rank index terms.</summary>
    public static int Forward(this Color color) => color == Color.White ? 1 : -1;

    /// <summary>Rank index (0-7) of this color's back rank.</summary>
    public static int BackRank(this Color color) => color == Color.White ? 0 : 7;

    /// <summary>Rank index (0-7) pawns of this color start on and may double-step from.</summary>
    public static int PawnRank(this Color color) => color == Color.White ? 1 : 6;

    /// <summary>Rank index (0-7) pawns of this color promote on.</summary>
    public static int PromotionRank(this Color color) => color == Color.White ? 7 : 0;

    /// <summary>Whether the rank index lies on this color's half of the board (its placement zone).</summary>
    public static bool IsOwnHalf(this Color color, int rank) => color == Color.White ? rank <= 3 : rank >= 4;
}
