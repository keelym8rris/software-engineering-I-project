using BlackjackApp.Models;

namespace BlackjackApp.Services;

public class BlackjackGameService
{
    private readonly Dictionary<string, GameState> _games = new();
    private readonly Dictionary<string, Deck> _decks = new();
    // Maps connectionId -> gameId for fast disconnect lookup
    private readonly Dictionary<string, string> _connectionIndex = new();

    public GameState CreateGame()
    {
        var game = new GameState();
        _games[game.GameId] = game;
        _decks[game.GameId] = new Deck(deckCount: 6);
        return game;
    }

    public GameState? GetGame(string gameId) =>
        _games.TryGetValue(gameId, out var g) ? g : null;

    public string? FindGameForConnection(string connectionId) =>
        _connectionIndex.TryGetValue(connectionId, out var id) ? id : null;

    public List<GameState> GetOpenGames() =>
        _games.Values.Where(g => g.Phase == GamePhase.Lobby && g.Players.Count < 6).ToList();

    public bool AddPlayer(string gameId, string connectionId, string name)
    {
        var game = GetGame(gameId);
        if (game == null || game.Players.Count >= 6 || game.Phase != GamePhase.Lobby) return false;
        if (game.Players.Any(p => p.ConnectionId == connectionId)) return false;

        game.Players.Add(new Player { ConnectionId = connectionId, Name = name });
        _connectionIndex[connectionId] = gameId;
        return true;
    }

    // Used by CreateGame — skips registering the lobby connection in the index
    // so that the inevitable lobby disconnect doesn't remove the player
    public bool AddPlayerNoIndex(string gameId, string connectionId, string name)
    {
        var game = GetGame(gameId);
        if (game == null || game.Players.Count >= 6 || game.Phase != GamePhase.Lobby) return false;
        if (game.Players.Any(p => p.ConnectionId == connectionId)) return false;

        game.Players.Add(new Player { ConnectionId = connectionId, Name = name });
        // intentionally NOT adding to _connectionIndex
        return true;
    }

    // Called when a player navigates from lobby -> game page (updates their connection ID)
    public void UpdateConnectionId(string gameId, string oldConnectionId, string newConnectionId)
    {
        var game = GetGame(gameId);
        var player = game?.Players.FirstOrDefault(p => p.ConnectionId == oldConnectionId);
        if (player == null) return;

        player.ConnectionId = newConnectionId;
        _connectionIndex.Remove(oldConnectionId);
        _connectionIndex[newConnectionId] = gameId;
    }

    public void RemovePlayer(string gameId, string connectionId)
    {
        var game = GetGame(gameId);
        if (game == null) return;

        var player = game.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
        if (player == null) return;

        int removedIndex = game.Players.IndexOf(player);
        bool wasCurrentPlayer = game.Phase == GamePhase.Playing &&
                                game.CurrentPlayer?.ConnectionId == connectionId;

        game.Players.Remove(player);
        _connectionIndex.Remove(connectionId);

        if (game.Players.Count == 0)
        {
            _games.Remove(gameId);
            _decks.Remove(gameId);
            return;
        }

        if (game.Phase == GamePhase.Playing)
        {
            if (removedIndex < game.CurrentPlayerIndex)
                game.CurrentPlayerIndex--;

            if (wasCurrentPlayer)
                AdvanceIfNeeded(game);
        }

        // If betting and the leaver hadn't bet yet, check if remaining players have all bet
        if (game.Phase == GamePhase.Betting && game.Players.All(p => p.CurrentBet > 0))
            DealInitialCards(game);
    }

    public bool StartBetting(string gameId)
    {
        var game = GetGame(gameId);
        if (game == null || game.Players.Count == 0 || game.Phase != GamePhase.Lobby) return false;
        game.Phase = GamePhase.Betting;
        game.Message = "Place your bets!";
        foreach (var p in game.Players) { p.CurrentBet = 0; p.Hand.Clear(); p.Status = PlayerStatus.Waiting; }
        game.Dealer.Hand.Clear();
        return true;
    }

    public bool PlaceBet(string gameId, string connectionId, int amount)
    {
        var game = GetGame(gameId);
        var player = game?.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
        if (player == null || game!.Phase != GamePhase.Betting || player.CurrentBet > 0) return false;
        if (amount < 10 || amount > player.Chips) return false;

        player.CurrentBet = amount;

        if (game.AllBetsPlaced)
            DealInitialCards(game);

        return true;
    }

    private void DealInitialCards(GameState game)
    {
        var deck = _decks[game.GameId];
        deck.Reset();
        foreach (var p in game.Players) p.Hand.Add(deck.Deal());
        game.Dealer.Hand.Add(deck.Deal());
        foreach (var p in game.Players) p.Hand.Add(deck.Deal());
        game.Dealer.Hand.Add(deck.Deal());

        game.Phase = GamePhase.Playing;
        game.CurrentPlayerIndex = 0;

        foreach (var p in game.Players)
            p.Status = p.HasBlackjack ? PlayerStatus.Blackjack : PlayerStatus.Playing;

        AdvanceIfNeeded(game);
    }

    public bool Hit(string gameId, string connectionId)
    {
        var game = GetGame(gameId);
        var player = game?.CurrentPlayer;
        if (game?.Phase != GamePhase.Playing || player == null ||
            player.ConnectionId != connectionId || player.Status != PlayerStatus.Playing) return false;

        player.Hand.Add(_decks[gameId].Deal());

        if (player.IsBusted)
        {
            player.Status = PlayerStatus.Busted;
            AdvancePlayer(game!);
        }
        else if (player.HandValue == 21)
        {
            player.Status = PlayerStatus.Standing;
            AdvancePlayer(game!);
        }
        return true;
    }

    public bool Stand(string gameId, string connectionId)
    {
        var game = GetGame(gameId);
        var player = game?.CurrentPlayer;
        if (game?.Phase != GamePhase.Playing || player == null ||
            player.ConnectionId != connectionId || player.Status != PlayerStatus.Playing) return false;

        player.Status = PlayerStatus.Standing;
        AdvancePlayer(game!);
        return true;
    }

    private void AdvanceIfNeeded(GameState game)
    {
        while (game.CurrentPlayerIndex < game.Players.Count &&
               game.Players[game.CurrentPlayerIndex].Status != PlayerStatus.Playing)
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

            if (game.Dealer.HasBlackjack)
            {
                if (p.HasBlackjack)
                    p.Status = PlayerStatus.Push;
                else
                {
                    p.Chips -= p.CurrentBet;
                    p.Status = PlayerStatus.Lost;
                }
                continue;
            }

            if (p.Status == PlayerStatus.Blackjack && !game.Dealer.HasBlackjack)
            {
                p.Chips += p.CurrentBet * 1.5m; p.Status = PlayerStatus.Won; continue;
            }

            int pv = p.HandValue;
            if (dealerBusted || pv > dealerValue) { p.Chips += p.CurrentBet; p.Status = PlayerStatus.Won; }
            else if (pv < dealerValue) { p.Chips -= p.CurrentBet; p.Status = PlayerStatus.Lost; }
            else { p.Status = PlayerStatus.Push; }
        }

        game.Phase = GamePhase.RoundOver;
        game.Message = "Round over! Ready for next round?";
    }

    public bool ResetRound(string gameId)
    {
        var game = GetGame(gameId);
        if (game == null || game.Phase != GamePhase.RoundOver) return false;
        var eliminatedPlayers = game.Players.Where(p => p.Chips < 10).ToList();
        foreach (var player in eliminatedPlayers)
        {
            game.Players.Remove(player);
            _connectionIndex.Remove(player.ConnectionId);
        }
        game.Phase = GamePhase.Lobby;
        game.Message = "Waiting for players...";
        return true;
    }
}
