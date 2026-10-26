using BlackjackApp.Models;
using BlackjackApp.Services;
using Microsoft.AspNetCore.SignalR;

namespace BlackjackApp.Hubs;

public class BlackjackHub : Hub
{
    private readonly GameService _gameService;
    private readonly ILogger<BlackjackHub> _logger;

    public BlackjackHub(GameService gameService, ILogger<BlackjackHub> logger)
    {
        _gameService = gameService;
        _logger = logger;
    }

    public async Task CreateGame(string playerName, string mode)
    {
        var name = NormalizePlayerName(playerName);
        if (name.Length == 0)
        {
            await Clients.Caller.SendAsync("Error", "Enter your name before creating a game.");
            return;
        }

        if (!Enum.TryParse<GameMode>(mode, true, out var gameMode) || !Enum.IsDefined(gameMode))
        {
            await Clients.Caller.SendAsync("Error", "Choose Solo or Multiplayer before creating a game.");
            return;
        }

        string? gameId = null;
        try
        {
            gameId = _gameService.CreateGame(Context.ConnectionId, name, gameMode);
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
            await BroadcastState(gameId);
            await Clients.Caller.SendAsync("GameCreated", gameId, Context.ConnectionId);
        }
        catch (Exception ex)
        {
            if (gameId != null)
            {
                _gameService.DeleteGame(gameId);
                try { await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId); }
                catch (Exception cleanupException)
                {
                    _logger.LogWarning(cleanupException, "Could not remove failed game {GameId} from its SignalR group", gameId);
                }
            }

            _logger.LogError(ex, "Could not create a game for connection {ConnectionId}", Context.ConnectionId);
            try { await Clients.Caller.SendAsync("Error", "The game could not be created. Please try again."); }
            catch (Exception sendException)
            {
                _logger.LogDebug(sendException, "Could not return the game creation error to the caller");
            }
        }
    }

    public async Task JoinGame(string gameId, string playerName, string? existingConnectionId = null)
    {
        gameId = gameId?.Trim().ToUpperInvariant() ?? "";
        var name = NormalizePlayerName(playerName);
        if (name.Length == 0)
        {
            await Clients.Caller.SendAsync("Error", "Enter your name before joining a game.");
            return;
        }

        if (!_gameService.TryJoinGame(gameId, Context.ConnectionId, name, existingConnectionId, out var error))
        {
            await Clients.Caller.SendAsync("Error", error);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
        await Clients.Caller.SendAsync("GameJoined", gameId, Context.ConnectionId);
        await BroadcastState(gameId);
    }

    public async Task StartBetting(string gameId)
    {
        if (!_gameService.StartBetting(gameId, Context.ConnectionId, out var error))
        {
            await Clients.Caller.SendAsync("Error", error);
            return;
        }

        await BroadcastState(gameId);
    }

    public async Task PlaceBet(string gameId, int amount)
    {
        if (!_gameService.PlaceBet(gameId, Context.ConnectionId, amount, out var error))
        {
            await Clients.Caller.SendAsync("Error", error);
            return;
        }

        await BroadcastState(gameId);
    }

    public async Task Hit(string gameId)
    {
        if (!_gameService.Hit(gameId, Context.ConnectionId))
        {
            await Clients.Caller.SendAsync("Error", "You can only hit during your turn.");
            return;
        }
        await BroadcastState(gameId);
    }

    public async Task Stand(string gameId)
    {
        if (!_gameService.Stand(gameId, Context.ConnectionId))
        {
            await Clients.Caller.SendAsync("Error", "You can only stand during your turn.");
            return;
        }
        await BroadcastState(gameId);
    }

    public async Task NewRound(string gameId)
    {
        if (!_gameService.ResetRound(gameId))
        {
            await Clients.Caller.SendAsync("Error", "A new round can only start after the current round is over.");
            return;
        }
        await BroadcastState(gameId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);

        // Find which game this connection belongs to and handle their departure
        var gameId = _gameService.FindGameForConnection(Context.ConnectionId);
        if (gameId == null) return;

        if (_gameService.RemovePlayer(gameId, Context.ConnectionId))
        {
            await BroadcastState(gameId);
        }
    }

    private async Task BroadcastState(string gameId)
    {
        var dto = _gameService.GetGameState(gameId);
        if (dto == null) return;
        await Clients.Group(gameId).SendAsync("GameStateUpdated", dto);
    }

    private static string NormalizePlayerName(string? name)
    {
        var trimmed = name?.Trim() ?? "";
        return trimmed[..Math.Min(trimmed.Length, 20)];
    }
}
