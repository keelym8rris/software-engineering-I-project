using Blackjack.Web.Models;

namespace Blackjack.Web.Services;

public sealed class LobbyPrototypeService
{
    public string DisplayName { get; set; } = "Guest";
    public string JoinCode { get; private set; } = "BLKJ21";
    public LobbySettings Settings { get; } = new();
    public List<LobbyPlayer> Players { get; } = new()
    {
        new() { DisplayName = "Host", IsHost = true, IsReady = true },
        new() { DisplayName = "Guest 2", IsReady = false }
    };

    public void CreatePrototypeLobby(string displayName)
    {
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Host" : displayName.Trim();
        JoinCode = Random.Shared.Next(100000, 999999).ToString();
        Players.Clear();
        Players.Add(new LobbyPlayer
        {
            DisplayName = DisplayName,
            IsHost = true,
            IsReady = true,
            Bankroll = Settings.StartingBankroll
        });
    }

    public void AddPrototypePlayer(string displayName)
    {
        if (Players.Count >= Settings.MaxPlayers) return;
        Players.Add(new LobbyPlayer
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? $"Player {Players.Count + 1}" : displayName.Trim(),
            Bankroll = Settings.StartingBankroll
        });
    }

    // TODO(multiplayer): replace this singleton prototype with a real lobby repository/service.
    // TODO(realtime): add SignalR or equivalent server-authoritative updates across clients.
    // TODO(persistence): store users, stats and completed games in a database.
}
