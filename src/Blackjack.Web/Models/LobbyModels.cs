namespace Blackjack.Web.Models;

public sealed class LobbySettings
{
    public string GameName { get; set; } = "Blackjack";
    public int MaxPlayers { get; set; } = 4;
    public int StartingBankroll { get; set; } = 1000;
    public int MinimumBet { get; set; } = 25;
    public bool DealerHitsSoft17 { get; set; }
    public bool DoubleAfterSplit { get; set; } = true;
}

public sealed class LobbyPlayer
{
    public string DisplayName { get; set; } = "Player";
    public bool IsHost { get; set; }
    public bool IsReady { get; set; }
    public int Bankroll { get; set; } = 1000;
}
