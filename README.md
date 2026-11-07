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

Then open http://localhost:5080:
1. Create a game.
2. Copy the invite link.
3. Open it in another browser, or in a private window. A normal tab shares the first tab's stored seat token.

> **Windows Smart App Control:** on some machines, the freshly built Debug `GenesisChess.Web.dll` is blocked with "An Application Control policy has blocked this file". The Release build runs, which is why the commands above use `-c Release`.

## Running in VS Code (macOS, Windows, Linux)

This is the easiest way to run and host games, especially on a Mac.

### One-time setup
1. Install the **.NET 10 SDK** from https://dotnet.microsoft.com/download. On a Mac, choose **Arm64** for Apple Silicon (M1 and later) or **x64** for Intel. Homebrew works too:
   ```bash
   brew install --cask dotnet-sdk
   ```
   Check it with `dotnet --version` in a new terminal. It should print 10.x.
2. Install **VS Code** and the **C# Dev Kit** extension. VS Code offers it when you open the folder.
3. Clone the repo and open the folder in VS Code (**File → Open Folder…**).

### Run
- Press **F5** (or **Run → Start Debugging**). Pick **Run Genesis Chess** in the Run and Debug panel if asked. It builds the app, starts it on http://localhost:5080 and opens your browser.
- **Debug Genesis Chess** does the same with a Debug build, for breakpoints. (On Windows machines with Smart App Control, use the Release option instead; see above.)
- To run the tests, open the Command Palette and choose **Tasks: Run Test Task**.
- To stop the app, press **Shift+F5** or click the red square.

## Playing with friends using VS Code port forwarding

VS Code can give the running app a public link, so friends anywhere can join without installing anything.

1. Start the app with **F5** (see above).
2. Open the **Ports** panel: **View → Open View… → Ports**, or the **Ports** tab next to the Terminal.
3. Click **Forward a Port** and enter `5080`. Sign in with GitHub if VS Code asks (first time only).
4. Right-click the new row → **Port Visibility → Public**. Without this, friends get a sign-in page.
5. Open the **Forwarded Address** (`https://…devtunnels.ms`) in *your* browser, create a game, and send your friend the invite link. The app trusts forwarded headers, so the invite link uses the public address.

Notes:
- Friends may see a one-time "you are about to connect to a dev tunnel" page. Clicking **Continue** is fine.
- The link only works while VS Code is open and the app is running on your computer. Stopping the app ends all games in progress.
- The first person to open an invite link takes the open seat. Anyone after that spectates.

### Before you share a public link
A **Public** port means anyone who has the link can open the app, with no sign-in. Only the forwarded port is exposed, not the rest of your computer, but keep these in mind:

- **Share links only with people you know.** Don't post them publicly. Anyone with the link can create games or spectate, and someone could create lots of games and slow your computer down.
- **Forward only port `5080`.** Never make other ports public. Other apps running on your computer may have no protection at all.
- **Error pages can show details about your computer.** F5 runs the app in Development mode, so if something crashes, visitors may see an error page with code and file paths (which can include your username). Nothing sensitive is stored in the app, but it's more than strangers need to see.
- **Your traffic passes through Microsoft's dev tunnel service** on its way to your computer. That's fine for chess, but don't send anything private through it.
- **Only click through the dev tunnel warning page for links from people you trust.** Scammers sometimes host fake pages on dev tunnels.

### Stopping when you're done
1. **Stop the app:** press **Shift+F5**, or click the red square in the debug toolbar. The public link stops working right away and all games end.
2. **Remove the forwarded port:** in the **Ports** panel, right-click the `5080` row → **Stop Forwarding Port**. Otherwise VS Code keeps the public tunnel set up for the next time you start the app.
3. Closing VS Code also closes the tunnel.

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
   devtunnel host -p 5080 --allow-anonymous
   ```
3. Open the `https://…devtunnels.ms` URL it prints, create a game, and send your friend the invite link. The app trusts forwarded headers, so the link uses the tunnel URL. The first person to open the link takes the open seat; anyone after that spectates.
4. When you're done, press **Ctrl+C** in the tunnel terminal and in the app terminal. The link stops working as soon as either one stops.

`--allow-anonymous` makes the link public, so the same cautions apply as for VS Code port forwarding (see [Before you share a public link](#before-you-share-a-public-link)).

Games live in memory. They are lost when the app restarts, and idle games are removed after 24 hours (`Games:IdleTimeout` in `appsettings.json`).

## Hosting later

`IGameStore` is the seam for persistence. Before moving to a host like Azure App Service:
- add a shared store, such as Redis or a database
- use sticky sessions or Azure SignalR Service if you run more than one instance

Nothing else depends on the hosting model.
