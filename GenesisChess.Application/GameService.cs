using System.Security.Cryptography;
using GenesisChess.Clock;

namespace GenesisChess.Application;

/// <summary>Entry point for creating and finding games.</summary>
public sealed class GameService(IGameStore store, TimeProvider time)
{
    public CreatedGame CreateGame(TimeControl timeControl, ColorChoice colorChoice)
    {
        ArgumentNullException.ThrowIfNull(timeControl);

        var session = new GameSession(Identifiers.NewGameId(), timeControl, time);
        var seat = colorChoice switch
        {
            ColorChoice.White => Seat.White,
            ColorChoice.Black => Seat.Black,
            _ => RandomNumberGenerator.GetInt32(2) == 0 ? Seat.White : Seat.Black,
        };

        var token = session.Claim(seat);
        store.Add(session);
        return new CreatedGame(session, token, seat);
    }

    public GameSession? Find(string id) => store.Find(id);
}
