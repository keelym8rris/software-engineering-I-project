using BlackjackApp.Models;
using BlackjackApp.Services;
using Microsoft.AspNetCore.SignalR;

namespace BlackjackApp.Hubs;

public record GameSummary(string GameId, int PlayerCount);

public class BlackjackHub : Hub
{
    private readonly GameService _gameService;

    public BlackjackHub(GameService gameService) => _gameService = gameService;

    public async Task CreateGame(string playerName)
    {
        var game = _gameService.CreateGame();
        // Add player WITHOUT registering their lobby connection in the disconnect index.
        // The lobby connection will disconnect on navigation — we don't want that to remove the player.
        // The game-page connection will register itself via JoinGame/UpdateConnectionId.
        _gameService.AddPlayerNoIndex(game.GameId, Context.ConnectionId, playerName);
        await Groups.AddToGroupAsync(Context.ConnectionId, game.GameId);
        await Clients.Caller.SendAsync("GameCreated", game.GameId, Context.ConnectionId);
        await BroadcastState(game.GameId);
    }

    public async Task JoinGame(string gameId, string playerName, string? existingConnectionId = null)
    {
        var game = _gameService.GetGame(gameId);
        if (game == null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found.");
            return;
        }

        // If the caller already has a slot (matched by prior connection ID passed from the client)
        // just re-add them to the SignalR group without duplicating the player record
        var alreadyInGame = existingConnectionId != null &&
                            game.Players.Any(p => p.ConnectionId == existingConnectionId);

        if (!alreadyInGame)
        {
            // New joiner — update connection ID on any existing slot or add fresh
            var existing = game.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (existing == null)
            {
                if (!_gameService.AddPlayer(gameId, Context.ConnectionId, playerName))
                {
                    await Clients.Caller.SendAsync("Error", "Cannot join game — it may be full or already started.");
                    return;
                }
            }
        }
        else
        {
            // Reconnecting creator: update their stored connection ID to the new one
            var slot = game.Players.FirstOrDefault(p => p.ConnectionId == existingConnectionId);
            if (slot != null)
                _gameService.UpdateConnectionId(gameId, existingConnectionId, Context.ConnectionId);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
        await Clients.Caller.SendAsync("GameJoined", gameId, Context.ConnectionId);
        await BroadcastState(gameId);
    }

    public async Task StartBetting(string gameId)
    {
        _gameService.StartBetting(gameId);
        await BroadcastState(gameId);
    }

    public async Task PlaceBet(string gameId, int amount)
    {
        _gameService.PlaceBet(gameId, Context.ConnectionId, amount);
        await BroadcastState(gameId);
    }

    public async Task Hit(string gameId)
    {
        _gameService.Hit(gameId, Context.ConnectionId);
        await BroadcastState(gameId);
    }

    public async Task Stand(string gameId)
    {
        _gameService.Stand(gameId, Context.ConnectionId);
        await BroadcastState(gameId);
    }

    public async Task NewRound(string gameId)
    {
        _gameService.ResetRound(gameId);
        await BroadcastState(gameId);
    }

    public async Task GetOpenGames()
    {
        var games = _gameService.GetOpenGames()
            .Select(g => new GameSummary(g.GameId, g.Players.Count))
            .ToList();
        await Clients.Caller.SendAsync("OpenGames", games);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);

        // Find which game this connection belongs to and handle their departure
        var gameId = _gameService.FindGameForConnection(Context.ConnectionId);
        if (gameId == null) return;

        _gameService.RemovePlayer(gameId, Context.ConnectionId);

        var game = _gameService.GetGame(gameId);
        if (game != null)
            await BroadcastState(gameId);
    }

    private async Task BroadcastState(string gameId)
    {
        var game = _gameService.GetGame(gameId);
        if (game == null) return;

        var dto = new GameStateDto
        {
            GameId = game.GameId,
            Phase = game.Phase.ToString(),
            Message = game.Message,
            CurrentPlayerConnectionId = game.CurrentPlayer?.ConnectionId,
            Dealer = MapPlayer(game.Dealer),
            Players = game.Players.Select(MapPlayer).ToList()
        };

        await Clients.Group(gameId).SendAsync("GameStateUpdated", dto);
    }

    private static PlayerDto MapPlayer(Player p) => new()
    {
        ConnectionId = p.ConnectionId,
        Name = p.Name,
        Chips = p.Chips,
        CurrentBet = p.CurrentBet,
        HandValue = p.HandValue,
        Status = p.Status.ToString(),
        Hand = p.Hand.Select(c => new CardDto { Display = c.Display, IsRed = c.IsRed }).ToList()
    };
}
