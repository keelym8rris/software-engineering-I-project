namespace Blackjack.Web.Models;

public sealed class LobbySettings
{
    public string GameName { get; set; } = "Blackjack";
    public int MaxPlayers { get; set; } = 4;
    public int StartingBankroll { get; set; } = 1000;
    public int MinimumBet { get; set; } = 25;
    public int DeckCount { get; set; } = 4;
    public bool DealerHitsSoft17 { get; set; }
    public bool DoubleAfterSplit { get; set; } = true;
    public bool SurrenderEnabled { get; set; }
    public bool InsuranceEnabled { get; set; }
}

public sealed class LobbyPlayer
{
    public Guid PlayerId { get; init; }
    public string DisplayName { get; set; } = "Player";
    public bool IsHost { get; set; }
    public bool IsReady { get; set; }
    public int Bankroll { get; set; } = 1000;
}

public sealed class LobbyState
{
    public string Code { get; init; } = "";
    public LobbySettings Settings { get; init; } = new();
    public List<LobbyPlayer> Players { get; } = new();
    public bool GameStarted { get; set; }
    public BlackjackRound Round { get; } = new();
    public object SyncRoot { get; } = new();
}

public sealed record LobbyActionResult(bool Success, string Message, LobbyState? Lobby = null)
{
    public static LobbyActionResult Ok(LobbyState lobby, string message = "") => new(true, message, lobby);
    public static LobbyActionResult Fail(string message) => new(false, message);
}
