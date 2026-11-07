using System.Collections.Immutable;

namespace GenesisChess.Engine;

/// <summary>Pieces a player still holds off the board and may place. Immutable.</summary>
public sealed record Reserve
{
    private static readonly PieceType[] PlaceableTypes =
        [PieceType.Pawn, PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen];

    private readonly ImmutableDictionary<PieceType, int> _counts;

    private Reserve(ImmutableDictionary<PieceType, int> counts) => _counts = counts;

    /// <summary>The standard set minus the king: 8 pawns, 2 knights, 2 bishops, 2 rooks, 1 queen.</summary>
    public static Reserve Initial { get; } = new(ImmutableDictionary.CreateRange(new Dictionary<PieceType, int>
    {
        [PieceType.Pawn] = 8,
        [PieceType.Knight] = 2,
        [PieceType.Bishop] = 2,
        [PieceType.Rook] = 2,
        [PieceType.Queen] = 1,
    }));

    public static Reserve Empty { get; } = new(ImmutableDictionary<PieceType, int>.Empty);

    public static Reserve Of(params ReadOnlySpan<(PieceType Type, int Count)> counts)
    {
        var builder = ImmutableDictionary.CreateBuilder<PieceType, int>();
        foreach (var (type, count) in counts)
        {
            if (type == PieceType.King)
            {
                throw new ArgumentException("Kings cannot be held in reserve.", nameof(counts));
            }

            if (count > 0)
            {
                builder[type] = count;
            }
        }

        return new Reserve(builder.ToImmutable());
    }

    public int Count(PieceType type) => _counts.GetValueOrDefault(type);

    public int Total => _counts.Values.Sum();

    public bool IsEmpty => Total == 0;

    /// <summary>Piece types with at least one copy available, in a stable order.</summary>
    public IEnumerable<PieceType> Available => PlaceableTypes.Where(t => Count(t) > 0);

    public Reserve Remove(PieceType type)
    {
        var count = Count(type);
        if (count == 0)
        {
            throw new InvalidOperationException($"No {type} left in reserve.");
        }

        return new Reserve(count == 1 ? _counts.Remove(type) : _counts.SetItem(type, count - 1));
    }

    /// <summary>Compact key such as "P8N2B2R2Q1", used for repetition detection.</summary>
    public string ToKey() => string.Concat(PlaceableTypes.Select(t => $"{t.Letter()}{Count(t)}"));

    public bool Equals(Reserve? other) => other is not null && ToKey() == other.ToKey();

    public override int GetHashCode() => ToKey().GetHashCode(StringComparison.Ordinal);
}
