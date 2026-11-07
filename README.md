# Genesis Chess

A chess variant for two players in the browser. Each side starts with **only a king**. Every other piece waits in reserve, and on each turn you either **move** a piece on the board or **place** one from your reserve.

## Rules

- **Start:** White king on e1, Black king on e8. Each reserve holds 8 pawns, 2 rooks, 2 knights, 2 bishops and 1 queen. White moves first.
- **Turn:** move one piece, or place one reserve piece.
- **Placement:** on any empty square of your own half (White ranks 1–4, Black ranks 5–8). Pawns can't be placed on your back rank.
- **Check:** a move or placement must not leave your king in check. Placing a piece can block a check, give check, or deliver mate.
- **Pawns:** they double-step only from rank 2 (White) or rank 7 (Black). En passant and promotion follow the normal rules.
- **Castling:** allowed when the king and a rook on its corner square (a1/h1, a8/h8) have never moved since being placed, with the usual conditions: empty path, and the king is not in check, does not pass through check and does not land in check.
- **Captured pieces** leave the game.
- **Game end:**
  - checkmate
  - stalemate: no legal move **and** no legal placement
  - resignation
  - timeout
  - draw by agreement
  - 50-move rule: placements reset the count
  - threefold repetition: reserves are part of the position
  - insufficient material: only once both reserves are empty

### Time controls
You pick a time control when you create the game: a preset (1+0 up to 30+20), a custom base time plus an increment in seconds (the increment can be 0), or untimed.

- The increment is a Fischer increment, added after each of your turns.
- Clocks start with White's first action, which is not timed.
- If your flag falls you lose, unless your opponent could never mate (lone king or king plus one minor piece, with an empty reserve). In that case the game is a draw.

## Project layout

```
GenesisChess.slnx
GenesisChess.Engine/          rules only: board, reserves, move/placement generation, outcomes, notation
GenesisChess.Clock/           time controls and a TimeProvider-driven chess clock (no engine dependency)
GenesisChess.Application/     game sessions, seats/tokens, clock integration, IGameStore, cleanup
GenesisChess.Web/             Blazor Web App (Interactive Server): UI only
Tests/
  GenesisChess.Engine.Tests/  GenesisChess.Clock.Tests/  GenesisChess.Application.Tests/
```

Dependencies point inward: Web → Application → Engine and Clock. The server is the authority: every action is validated in `GameSession`, and clients only render a `GameView`.

## Running locally

Requires the .NET 10 SDK.

```bash
dotnet test GenesisChess.slnx
```

```bash
dotnet run --project GenesisChess.Web -c Release --launch-profile http
```

Then open http://localhost:5000:
1. Create a game.
2. Copy the invite link.
3. Open it in another browser, or in a private window. A normal tab shares the first tab's stored seat token.

> **Windows Smart App Control:** on some machines, the freshly built Debug `GenesisChess.Web.dll` is blocked with "An Application Control policy has blocked this file". The Release build runs, which is why the commands above use `-c Release`.

## Playing with a friend over a dev tunnel

1. Install the tunnel CLI and sign in once:
   ```bash
   winget install Microsoft.devtunnel
   ```
   ```bash
   devtunnel user login
   ```
2. Start the app (see above). Then, in a second terminal:
   ```bash
   devtunnel host -p 5000 --allow-anonymous
   ```
3. Open the `https://…devtunnels.ms` URL it prints, create a game, and send your friend the invite link. The app trusts forwarded headers, so the link uses the tunnel URL. The first person to open the link takes the open seat; anyone after that spectates.

Games live in memory. They are lost when the app restarts, and idle games are removed after 24 hours (`Games:IdleTimeout` in `appsettings.json`).

## Hosting later

`IGameStore` is the seam for persistence. Before moving to a host like Azure App Service:
- add a shared store, such as Redis or a database
- use sticky sessions or Azure SignalR Service if you run more than one instance

Nothing else depends on the hosting model.
