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

    public async Task CreateGame(string playerName)
    {
        var name = NormalizePlayerName(playerName);
        if (name.Length == 0)
        {
            await Clients.Caller.SendAsync("Error", "Enter your name before creating a game.");
            return;
        }

        string? gameId = null;
        try
        {
            gameId = _gameService.CreateGame(Context.ConnectionId, name);
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
            await BroadcastState(gameId);
            await BroadcastOpenGames();
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

                try { await BroadcastOpenGames(); }
                catch (Exception refreshException)
                {
                    _logger.LogDebug(refreshException, "Could not refresh the open game list after rolling back {GameId}", gameId);
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
        await BroadcastOpenGames();
    }

    public async Task StartBetting(string gameId)
    {
        if (!_gameService.StartBetting(gameId, Context.ConnectionId, out var error))
        {
            await Clients.Caller.SendAsync("Error", error);
            return;
        }

        await BroadcastState(gameId);
        await BroadcastOpenGames();
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
        await Clients.Caller.SendAsync("OpenGames", _gameService.GetOpenGameSummaries());
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
            await BroadcastOpenGames();
        }
    }

    private async Task BroadcastState(string gameId)
    {
        var dto = _gameService.GetGameState(gameId);
        if (dto == null) return;
        await Clients.Group(gameId).SendAsync("GameStateUpdated", dto);
    }

    private Task BroadcastOpenGames() =>
        Clients.All.SendAsync("OpenGames", _gameService.GetOpenGameSummaries());

    private static string NormalizePlayerName(string? name)
    {
        var trimmed = name?.Trim() ?? "";
        return trimmed[..Math.Min(trimmed.Length, 20)];
    }
}
