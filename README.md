# Multiplayer Blackjack

A casino games app with real-time multiplayer Blackjack and solo Roulette, built with Blazor Server.

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

## Blackjack

1. Enter your name and click **Create New Game**
2. Share the game ID with other players — they enter their name and click **Join** on the lobby screen
3. Once players have joined, click **Start Game** to begin the betting round
4. Each player places a bet (use the quick-bet buttons or type an amount)
5. Once all bets are placed, cards are dealt automatically
6. On your turn, click **Hit** to draw a card or **Stand** to hold
7. After all players act, the dealer draws to 17
8. Winnings are settled automatically — blackjack pays 1.5x

### Chip balances

Each Blackjack seat starts with 1,000 virtual chips. Balances belong to the current in-memory seat/session only; leaving and creating a new table starts a fresh balance. They are not saved across server restarts or shared with Roulette. Bets are whole chips, with a minimum of 10. Ordinary wins add the wager, losses subtract it, pushes leave the balance unchanged, and natural blackjack adds 1.5 times the wager without rounding (a 25-chip bet earns 37.5). A dealer natural beats any non-natural hand, including a three-card 21.

After a round, players with fewer than 10 chips can select **Start a fresh table**. They are removed when the next round is prepared, and the host role passes to a remaining player if needed. This prevents fractional or zero balances from blocking everyone in betting.

### Regression tests

Run `dotnet test BlackjackApp.Tests` for deterministic payout, bet-validation, and low-balance lifecycle checks.

## Roulette

Choose **Roulette** from the home page to play with 1,000 virtual chips. Bet on a single number, red or black, or odd or even, then spin the wheel. Straight-up bets pay 35:1 and red/black or odd/even bets pay 1:1. Chips are not real currency.

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
│   ├── Game.razor             # Blackjack game table
│   └── Roulette.razor         # Solo Roulette
└── Program.cs
```

## Troubleshooting

**Port already in use** — another process is on 5400. Either stop it or change the port in `BlackjackApp/Properties/launchSettings.json`.

**Connection disconnected error** — make sure you're running .NET 8, not .NET 7. The project requires `net8.0`.
