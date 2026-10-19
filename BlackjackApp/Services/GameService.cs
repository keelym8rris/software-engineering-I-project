using BlackjackApp.Models;

namespace BlackjackApp.Services;

public class GameService
{
    private readonly object _sync = new();
    private readonly Dictionary<string, GameState> _games = new();
    private readonly Dictionary<string, Deck> _decks = new();
    // Maps connectionId -> gameId for fast disconnect lookup
    private readonly Dictionary<string, string> _connectionIndex = new();

    public string CreateGame(string hostConnectionId, string hostName)
    {
        lock (_sync)
        {
            var game = new GameState { HostConnectionId = hostConnectionId };
            while (_games.ContainsKey(game.GameId))
                game.GameId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            game.Players.Add(new Player { ConnectionId = hostConnectionId, Name = hostName });
            _games.Add(game.GameId, game);
            try
            {
                _decks.Add(game.GameId, new Deck());
            }
            catch
            {
                _games.Remove(game.GameId);
                throw;
            }

            // The lobby connection is intentionally not indexed. It closes when the
            // host navigates into the game; JoinGame registers the new connection.
            return game.GameId;
        }
    }

    public void DeleteGame(string gameId)
    {
        lock (_sync)
        {
            _games.Remove(gameId);
            _decks.Remove(gameId);
            foreach (var connectionId in _connectionIndex
                         .Where(entry => entry.Value == gameId)
                         .Select(entry => entry.Key)
                         .ToList())
                _connectionIndex.Remove(connectionId);
        }
    }

    public bool TryJoinGame(string gameId, string connectionId, string name, string? priorConnectionId, out string error)
    {
        lock (_sync)
        {
            error = "";
            if (!_games.TryGetValue(gameId, out var game))
            {
                error = "Game not found.";
                return false;
            }

            var returningPlayer = priorConnectionId == null
                ? null
                : game.Players.FirstOrDefault(p => p.ConnectionId == priorConnectionId);

            if (returningPlayer != null)
            {
                _connectionIndex.Remove(returningPlayer.ConnectionId);
                returningPlayer.ConnectionId = connectionId;
                _connectionIndex[connectionId] = gameId;
                if (game.HostConnectionId == priorConnectionId)
                    game.HostConnectionId = connectionId;
                return true;
            }

            if (game.Players.Any(p => p.ConnectionId == connectionId))
                return true;

            if (game.Phase != GamePhase.Lobby || game.Players.Count >= 6)
            {
                error = "Cannot join this game. It may be full or already started.";
                return false;
            }

            game.Players.Add(new Player { ConnectionId = connectionId, Name = name });
            _connectionIndex[connectionId] = gameId;
            return true;
        }
    }

    public List<GameSummary> GetOpenGameSummaries()
    {
        lock (_sync)
            return _games.Values
                .Where(g => g.Phase == GamePhase.Lobby && g.Players.Count < 6)
                .Select(g => new GameSummary(g.GameId, g.Players.Count))
                .ToList();
    }

    public GameStateDto? GetGameState(string gameId)
    {
        lock (_sync)
        {
            if (!_games.TryGetValue(gameId, out var game)) return null;

            return new GameStateDto
            {
                GameId = game.GameId,
                HostConnectionId = game.HostConnectionId,
                Phase = game.Phase.ToString(),
                Message = game.Message,
                CurrentPlayerConnectionId = game.CurrentPlayer?.ConnectionId,
                Dealer = MapPlayer(game.Dealer),
                Players = game.Players.Select(MapPlayer).ToList()
            };
        }
    }

    public string? FindGameForConnection(string connectionId)
    {
        lock (_sync)
            return _connectionIndex.TryGetValue(connectionId, out var id) ? id : null;
    }

    public bool RemovePlayer(string gameId, string connectionId)
    {
        lock (_sync)
        {
            if (!_games.TryGetValue(gameId, out var game)) return false;

            var player = game.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (player == null) return false;

            int removedIndex = game.Players.IndexOf(player);
            bool wasCurrentPlayer = game.Phase == GamePhase.Playing &&
                                    game.CurrentPlayer?.ConnectionId == connectionId;
            bool wasHost = game.HostConnectionId == connectionId;

            game.Players.Remove(player);
            _connectionIndex.Remove(connectionId);

            if (game.Players.Count == 0)
            {
                _games.Remove(gameId);
                _decks.Remove(gameId);
                return true;
            }

            if (wasHost)
            {
                game.HostConnectionId = game.Players[0].ConnectionId;
                if (game.Phase == GamePhase.Lobby)
                    game.Message = $"{game.Players[0].Name} is now the host.";
            }

            if (game.Phase == GamePhase.Playing)
            {
                if (removedIndex < game.CurrentPlayerIndex)
                    game.CurrentPlayerIndex--;

                if (wasCurrentPlayer)
                    AdvanceIfNeeded(game);
            }

            if (game.Phase == GamePhase.Betting && game.Players.All(p => p.CurrentBet > 0))
                DealInitialCards(game);

            return true;
        }
    }

    public bool StartBetting(string gameId, string connectionId, out string error)
    {
        lock (_sync)
        {
            error = "";
            if (!_games.TryGetValue(gameId, out var game))
                error = "Game not found.";
            else if (game.HostConnectionId != connectionId)
                error = "Only the host can start the game.";
            else if (game.Phase != GamePhase.Lobby)
                error = "This game has already started.";
            else if (game.Players.Count < 2)
                error = "At least two players are required to start.";

            if (!string.IsNullOrEmpty(error)) return false;

            game!.Phase = GamePhase.Betting;
            game.Message = "Place your bets!";
            foreach (var p in game.Players)
            {
                p.CurrentBet = 0;
                p.Hand.Clear();
                p.Status = PlayerStatus.Waiting;
            }
            game.Dealer.Hand.Clear();
            return true;
        }
    }

    public bool PlaceBet(string gameId, string connectionId, int amount, out string error)
    {
        lock (_sync)
        {
            var game = GetGameUnsafe(gameId);
            var player = game?.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (player == null)
            {
                error = "You are not a player in this game.";
                return false;
            }

            if (game!.Phase != GamePhase.Betting)
            {
                error = "Betting is not open right now.";
                return false;
            }

            if (amount < 10)
            {
                error = "The minimum bet is $10.";
                return false;
            }

            if (amount > player.Chips)
            {
                error = "Your bet cannot be greater than your available balance.";
                return false;
            }

            player.CurrentBet = amount;

            if (game.AllBetsPlaced)
                DealInitialCards(game);

            error = "";
            return true;
        }
    }

    public bool Hit(string gameId, string connectionId)
    {
        lock (_sync)
        {
            var game = GetGameUnsafe(gameId);
            var player = game?.CurrentPlayer;
            if (player == null || player.ConnectionId != connectionId) return false;

            player.Hand.Add(_decks[gameId].Deal());
            if (player.IsBusted)
            {
                player.Status = PlayerStatus.Busted;
                AdvancePlayer(game!);
            }
            return true;
        }
    }

    public bool Stand(string gameId, string connectionId)
    {
        lock (_sync)
        {
            var game = GetGameUnsafe(gameId);
            var player = game?.CurrentPlayer;
            if (player == null || player.ConnectionId != connectionId) return false;

            player.Status = PlayerStatus.Standing;
            AdvancePlayer(game!);
            return true;
        }
    }

    public void ResetRound(string gameId)
    {
        lock (_sync)
        {
            var game = GetGameUnsafe(gameId);
            if (game == null) return;

            var eliminated = game.Players.Where(p => p.Chips <= 0).ToList();
            foreach (var player in eliminated)
            {
                game.Players.Remove(player);
                _connectionIndex.Remove(player.ConnectionId);
            }

            if (game.Players.Count == 0)
            {
                _games.Remove(gameId);
                _decks.Remove(gameId);
                return;
            }

            if (!game.Players.Any(p => p.ConnectionId == game.HostConnectionId))
                game.HostConnectionId = game.Players[0].ConnectionId;

            game.Phase = GamePhase.Lobby;
            game.Message = "Waiting for players...";
        }
    }

    private GameState? GetGameUnsafe(string gameId) =>
        _games.TryGetValue(gameId, out var game) ? game : null;

    private static PlayerDto MapPlayer(Player player) => new()
    {
        ConnectionId = player.ConnectionId,
        Name = player.Name,
        Chips = player.Chips,
        CurrentBet = player.CurrentBet,
        HandValue = player.HandValue,
        Status = player.Status.ToString(),
        Hand = player.Hand.Select(card => new CardDto { Display = card.Display, IsRed = card.IsRed }).ToList()
    };

    private void DealInitialCards(GameState game)
    {
        var deck = _decks[game.GameId];
        foreach (var p in game.Players) { p.Hand.Add(deck.Deal()); p.Hand.Add(deck.Deal()); }
        game.Dealer.Hand.Add(deck.Deal());
        game.Dealer.Hand.Add(deck.Deal());

        game.Phase = GamePhase.Playing;
        game.CurrentPlayerIndex = 0;
        game.Message = $"{game.Players[0].Name}'s turn";

        foreach (var p in game.Players)
            if (p.HasBlackjack) p.Status = PlayerStatus.Blackjack;

        AdvanceIfNeeded(game);
    }

    private void AdvanceIfNeeded(GameState game)
    {
        while (game.CurrentPlayerIndex < game.Players.Count &&
               game.Players[game.CurrentPlayerIndex].Status != PlayerStatus.Waiting)
        {
            game.CurrentPlayerIndex++;
        }

        if (game.CurrentPlayerIndex >= game.Players.Count)
            DealerTurn(game);
        else
            game.Message = $"{game.Players[game.CurrentPlayerIndex].Name}'s turn";
    }

    private void AdvancePlayer(GameState game)
    {
        game.CurrentPlayerIndex++;
        AdvanceIfNeeded(game);
    }

    private void DealerTurn(GameState game)
    {
        game.Phase = GamePhase.DealerTurn;
        var deck = _decks[game.GameId];

        while (game.Dealer.HandValue < 17)
            game.Dealer.Hand.Add(deck.Deal());

        SettleRound(game);
    }

    private void SettleRound(GameState game)
    {
        int dealerValue = game.Dealer.HandValue;
        bool dealerBusted = game.Dealer.IsBusted;

        foreach (var p in game.Players)
        {
            if (p.Status == PlayerStatus.Busted) { p.Chips -= p.CurrentBet; p.Status = PlayerStatus.Lost; continue; }

            if (p.Status == PlayerStatus.Blackjack && !game.Dealer.HasBlackjack)
            {
                p.Chips += (int)(p.CurrentBet * 1.5); p.Status = PlayerStatus.Won; continue;
            }

            int pv = p.HandValue;
            if (dealerBusted || pv > dealerValue) { p.Chips += p.CurrentBet; p.Status = PlayerStatus.Won; }
            else if (pv < dealerValue) { p.Chips -= p.CurrentBet; p.Status = PlayerStatus.Lost; }
            else { p.Status = PlayerStatus.Push; }
        }

        game.Phase = GamePhase.RoundOver;
        game.Message = "Round over! Ready for next round?";
    }

}
