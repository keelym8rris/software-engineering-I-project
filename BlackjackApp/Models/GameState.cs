namespace BlackjackApp.Models;

public enum GamePhase { Lobby, Betting, Playing, DealerTurn, RoundOver }
public enum GameMode { Solo, Multiplayer }

public class GameState
{
    public string GameId { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
    public string HostConnectionId { get; set; } = "";
    public GameMode Mode { get; set; } = GameMode.Multiplayer;
    public GamePhase Phase { get; set; } = GamePhase.Lobby;
    public List<Player> Players { get; set; } = new();
    public List<Spectator> Spectators { get; set; } = new();
    public List<ChatMessage> ChatMessages { get; set; } = new();
    public Player Dealer { get; set; } = new() { Name = "Dealer", IsDealer = true };
    public int CurrentPlayerIndex { get; set; } = 0;
    public string Message { get; set; } = "Waiting for players...";

    public Player? CurrentPlayer =>
        Phase == GamePhase.Playing && CurrentPlayerIndex < Players.Count
            ? Players[CurrentPlayerIndex]
            : null;

    public bool AllBetsPlaced => Players.Count > 0 && Players.All(p => p.CurrentBet > 0);
}

public class Spectator
{
    public string ConnectionId { get; set; } = "";
    public string Name { get; set; } = "";
}

public class ChatMessage
{
    public string Sender { get; set; } = "";
    public string Text { get; set; } = "";
}
