# Multiplayer Blackjack

A real-time multiplayer Blackjack game built with Blazor Server and SignalR.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)

Check your version:
```
dotnet --version
```
Should output `8.x.x`.

## Running the app

```
dotnet run --project BlackjackApp
```

Then open `http://localhost:5400` in your browser.

To test multiplayer, open the same URL in a second browser tab or window.

## How to play

1. From **/play**, choose Solo to start a single-player game, or Multiplayer to host or join a table
2. For multiplayer, click **Host Lobby**, then share the 8-character code; other players choose Multiplayer and click **Join Lobby**
3. Once players have joined, click **Start Game** to begin the betting round
4. Each player places a bet (use the quick-bet buttons or type an amount)
5. Once all bets are placed, cards are dealt automatically
6. On your turn, click **Hit** to draw a card or **Stand** to hold
7. After all players act, the dealer draws to 17
8. Winnings are settled automatically — blackjack pays 1.5x

### Chip balances

Each Blackjack seat starts with 1,000 virtual chips. Balances belong to the current in-memory seat/session only; leaving and creating a new table starts a fresh balance. They are not saved across server restarts. Bets are whole chips, with a minimum of 10. Ordinary wins add the wager, losses subtract it, pushes leave the balance unchanged, and natural blackjack adds 1.5 times the wager without rounding (a 25-chip bet earns 37.5). A dealer natural beats any non-natural hand, including a three-card 21.

After a round, players with fewer than 10 chips can select **Start a fresh table**. They are removed when the next round is prepared, and the host role passes to a remaining player if needed. This prevents fractional or zero balances from blocking everyone in betting.

### Multiplayer chat and spectating

Players can send short text-only messages to their multiplayer table during lobby setup and gameplay. Messages are assigned to the connected player's name by the server, limited to 200 characters, and only the most recent 40 are held in memory. Spectators can chat too. Chat is not persistent and is not available in solo games.

After a multiplayer round, players with fewer than 10 chips can select **Spectate table** instead of starting a new table. Spectators keep their table connection and can watch and chat without wagering or taking turns. A player eliminated by the next-round reset automatically becomes a spectator. If only one funded player remains, that player may continue playing while the others spectate. If nobody can play, the table closes. Reconnection currently relies on the app's existing connection-ID behavior; full security hardening is still a separate task.

### Automated tests and GitHub Actions

Run locally from the repository root:

```shell
dotnet restore BlackjackApp.Tests/BlackjackApp.Tests.csproj
dotnet test BlackjackApp.Tests/BlackjackApp.Tests.csproj --configuration Release
```

GitHub Actions runs the xUnit suite on pushes to `keelys-edits`, `dev`, and `main`, on pull requests to `dev` or `main`, and with the manual **Run workflow** button.

For your presentation, open **Actions → .NET tests → latest run**, expand the Test step to see the results, or download the TRX artifact. These are unit/service tests; also manually verify multiplayer chat and spectating in two browsers.

## Project structure

```
BlackjackApp/
├── Hubs/
│   └── BlackjackHub.cs       # SignalR hub — handles all game actions
├── Models/
│   ├── Card.cs
│   ├── Deck.cs
│   ├── Player.cs
│   ├── GameState.cs
│   └── GameDto.cs            # Wire transfer objects
├── Services/
│   └── GameService.cs        # Game logic (singleton)
├── Pages/
│   ├── Index.razor            # Home and Blackjack lobby
│   └── Game.razor             # Blackjack game table
└── Program.cs
```

## Troubleshooting

**Port already in use** — another process is on 5400. Either stop it or change the port in `BlackjackApp/Properties/launchSettings.json`.

**Connection disconnected error** — make sure you're running .NET 8, not .NET 7. The project requires `net8.0`.
