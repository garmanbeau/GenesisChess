using System.Globalization;

namespace GenesisChess.Clock;

/// <summary>Base time per player plus a Fischer increment added after every completed turn.</summary>
public sealed record TimeControl
{
    public TimeControl(TimeSpan baseTime, TimeSpan increment)
    {
        if (baseTime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(baseTime), "Base time must be positive.");
        }

        if (increment < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(increment), "Increment cannot be negative.");
        }

        Base = baseTime;
        Increment = increment;
    }

    private TimeControl()
    {
        IsUntimed = true;
    }

    public static TimeControl Untimed { get; } = new();

    public TimeSpan Base { get; }

    public TimeSpan Increment { get; }

    public bool IsUntimed { get; }

    /// <summary>Common presets, fastest first.</summary>
    public static IReadOnlyList<TimeControl> Presets { get; } =
    [
        FromMinutes(1, 0),
        FromMinutes(2, 1),
        FromMinutes(3, 0),
        FromMinutes(3, 2),
        FromMinutes(5, 0),
        FromMinutes(5, 3),
        FromMinutes(10, 0),
        FromMinutes(10, 5),
        FromMinutes(15, 10),
        FromMinutes(30, 0),
        FromMinutes(30, 20),
    ];

    public static TimeControl FromMinutes(double baseMinutes, int incrementSeconds) =>
        new(TimeSpan.FromMinutes(baseMinutes), TimeSpan.FromSeconds(incrementSeconds));

    /// <summary>Speed category by estimated game length (base + 40 increments), as on most chess sites.</summary>
    public string Category
    {
        get
        {
            if (IsUntimed)
            {
                return "Untimed";
            }

            var estimate = Base + (Increment * 40);
            return estimate.TotalMinutes switch
            {
                < 3 => "Bullet",
                < 8 => "Blitz",
                < 25 => "Rapid",
                _ => "Classical",
            };
        }
    }

    /// <summary>Parses "5+3" (minutes + seconds) or "untimed".</summary>
    public static bool TryParse(string? text, out TimeControl timeControl)
    {
        timeControl = Untimed;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.Equals("untimed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parts = text.Split('+');
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            && minutes > 0 && seconds >= 0)
        {
            timeControl = FromMinutes(minutes, seconds);
            return true;
        }

        return false;
    }

    public override string ToString() => IsUntimed
        ? "untimed"
        : string.Create(CultureInfo.InvariantCulture, $"{Base.TotalMinutes:0.##}+{Increment.TotalSeconds:0}");
}
