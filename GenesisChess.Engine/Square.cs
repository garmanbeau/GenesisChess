using System.Diagnostics.CodeAnalysis;

namespace GenesisChess.Engine;

/// <summary>A board coordinate. File 0 = a, Rank 0 = rank 1.</summary>
public readonly record struct Square
{
    public Square(int file, int rank)
    {
        if (!IsOnBoard(file, rank))
        {
            throw new ArgumentOutOfRangeException(nameof(file), $"({file},{rank}) is off the board.");
        }

        File = file;
        Rank = rank;
    }

    public int File { get; }

    public int Rank { get; }

    /// <summary>0-63 index, a1 = 0, h8 = 63.</summary>
    public int Index => (Rank * 8) + File;

    public static IEnumerable<Square> All
    {
        get
        {
            for (var i = 0; i < 64; i++)
            {
                yield return FromIndex(i);
            }
        }
    }

    public static Square FromIndex(int index) => new(index % 8, index / 8);

    public static bool IsOnBoard(int file, int rank) => file is >= 0 and < 8 && rank is >= 0 and < 8;

    public static Square Parse(string text) =>
        TryParse(text, out var square) ? square : throw new FormatException($"'{text}' is not a square.");

    public static bool TryParse([NotNullWhen(true)] string? text, out Square square)
    {
        square = default;
        if (text is not { Length: 2 })
        {
            return false;
        }

        var file = char.ToLowerInvariant(text[0]) - 'a';
        var rank = text[1] - '1';
        if (!IsOnBoard(file, rank))
        {
            return false;
        }

        square = new Square(file, rank);
        return true;
    }

    /// <summary>Returns the square offset by the given deltas, or null if that is off the board.</summary>
    public Square? Offset(int fileDelta, int rankDelta)
    {
        var file = File + fileDelta;
        var rank = Rank + rankDelta;
        return IsOnBoard(file, rank) ? new Square(file, rank) : null;
    }

    public char FileLetter => (char)('a' + File);

    public override string ToString() => $"{FileLetter}{Rank + 1}";
}
