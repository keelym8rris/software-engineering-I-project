namespace BlackjackApp.Models;

// Flat DTOs for safe serialization over the wire — no computed properties, no null surprises
public class CardDto
{
    public string Display { get; set; } = "";
    public bool IsRed { get; set; }
}

public class PlayerDto
{
    public string ConnectionId { get; set; } = "";
    public string Name { get; set; } = "";
    public List<CardDto> Hand { get; set; } = new();
    public string Status { get; set; } = "Waiting";
    public int Chips { get; set; }
    public int CurrentBet { get; set; }
    public int HandValue { get; set; }
}

public class GameStateDto
{
    public string GameId { get; set; } = "";
    public string Phase { get; set; } = "Lobby";
    public string Message { get; set; } = "";
    public List<PlayerDto> Players { get; set; } = new();
    public PlayerDto Dealer { get; set; } = new();
    public string? CurrentPlayerConnectionId { get; set; }
}
