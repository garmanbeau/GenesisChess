using GenesisChess.Engine;

namespace GenesisChess.Web.Components.Game;

/// <summary>Unicode chess glyphs. The solid set is used for both colors and tinted with CSS.</summary>
public static class PieceGlyph
{
    // U+FE0E forces text (not emoji) presentation, which matters for the pawn on some platforms.
    private const string TextPresentation = "︎";

    public static string For(PieceType type) => type switch
    {
        PieceType.King => "♚",
        PieceType.Queen => "♛",
        PieceType.Rook => "♜",
        PieceType.Bishop => "♝",
        PieceType.Knight => "♞",
        PieceType.Pawn => "♟",
        _ => "?",
    } + TextPresentation;

    public static string CssClass(Color color) => color == Color.White ? "piece-white" : "piece-black";

    public static string Name(PieceType type) => type.ToString();
}
