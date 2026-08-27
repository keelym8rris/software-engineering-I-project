using System.Collections.Concurrent;
using Blackjack.Web.Models;

namespace Blackjack.Web.Services;

public sealed class InMemoryLobbyService
{
    private readonly ConcurrentDictionary<string, LobbyState> _lobbies = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string>? LobbyChanged;

    public LobbyActionResult CreateLobby(PlayerSessionService session, LobbySettings settings)
    {
        var validation = ValidateSettings(settings);
        if (validation is not null) return LobbyActionResult.Fail(validation);

        var code = GenerateCode();
        while (_lobbies.ContainsKey(code)) code = GenerateCode();

        var lobby = new LobbyState
        {
            Code = code,
            Settings = CloneSettings(settings)
        };

        lobby.Players.Add(new LobbyPlayer
        {
            PlayerId = session.PlayerId,
            DisplayName = NormalizeName(session.DisplayName),
            IsHost = true,
            IsReady = true,
            Bankroll = settings.StartingBankroll
        });

        _lobbies[code] = lobby;
        session.CurrentLobbyCode = code;
        Notify(code);
        return LobbyActionResult.Ok(lobby, "Lobby created.");
    }

    public LobbyActionResult JoinLobby(PlayerSessionService session, string code)
    {
        code = NormalizeCode(code);
        if (code.Length != 6) return LobbyActionResult.Fail("Enter a valid six-character lobby code.");
        if (!_lobbies.TryGetValue(code, out var lobby)) return LobbyActionResult.Fail("That lobby does not exist.");

        lock (lobby.SyncRoot)
        {
            if (lobby.GameStarted) return LobbyActionResult.Fail("That game has already started.");
            if (lobby.Players.Count >= lobby.Settings.MaxPlayers) return LobbyActionResult.Fail("That lobby is full.");
            if (lobby.Players.Any(p => p.PlayerId == session.PlayerId))
            {
                session.CurrentLobbyCode = code;
                return LobbyActionResult.Ok(lobby);
            }

            lobby.Players.Add(new LobbyPlayer
            {
                PlayerId = session.PlayerId,
                DisplayName = NormalizeName(session.DisplayName),
                Bankroll = lobby.Settings.StartingBankroll
            });
            session.CurrentLobbyCode = code;
        }

        Notify(code);
        return LobbyActionResult.Ok(lobby, "Joined lobby.");
    }

    public LobbyState? GetLobby(string? code)
        => code is not null && _lobbies.TryGetValue(NormalizeCode(code), out var lobby) ? lobby : null;

    public LobbyActionResult ToggleReady(PlayerSessionService session)
    {
        var lobby = GetLobby(session.CurrentLobbyCode);
        if (lobby is null) return LobbyActionResult.Fail("Lobby not found.");
        lock (lobby.SyncRoot)
        {
            var player = lobby.Players.FirstOrDefault(p => p.PlayerId == session.PlayerId);
            if (player is null) return LobbyActionResult.Fail("You are not in this lobby.");
            if (player.IsHost) return LobbyActionResult.Fail("The host is always ready.");
            player.IsReady = !player.IsReady;
        }
        Notify(lobby.Code);
        return LobbyActionResult.Ok(lobby);
    }

    public LobbyActionResult StartGame(PlayerSessionService session)
    {
        var lobby = GetLobby(session.CurrentLobbyCode);
        if (lobby is null) return LobbyActionResult.Fail("Lobby not found.");
        lock (lobby.SyncRoot)
        {
            var host = lobby.Players.FirstOrDefault(p => p.PlayerId == session.PlayerId && p.IsHost);
            if (host is null) return LobbyActionResult.Fail("Only the host can start the game.");
            if (lobby.Players.Count < 2) return LobbyActionResult.Fail("At least two players are required.");
            if (lobby.Players.Any(p => !p.IsReady)) return LobbyActionResult.Fail("Everyone must be ready first.");
            lobby.GameStarted = true;
            lobby.Round.State = RoundState.Betting;
            lobby.Round.Message = "Place your bet to begin the round.";
        }
        Notify(lobby.Code);
        return LobbyActionResult.Ok(lobby);
    }

    public LobbyActionResult LeaveLobby(PlayerSessionService session)
    {
        var lobby = GetLobby(session.CurrentLobbyCode);
        if (lobby is null) { session.CurrentLobbyCode = null; return LobbyActionResult.Fail("Lobby not found."); }
        lock (lobby.SyncRoot)
        {
            var player = lobby.Players.FirstOrDefault(p => p.PlayerId == session.PlayerId);
            if (player is null) return LobbyActionResult.Fail("You are not in this lobby.");
            var wasHost = player.IsHost;
            lobby.Players.Remove(player);
            if (wasHost && lobby.Players.Count > 0)
            {
                lobby.Players[0].IsHost = true;
                lobby.Players[0].IsReady = true;
            }
            if (lobby.Players.Count == 0) _lobbies.TryRemove(lobby.Code, out _);
        }
        session.CurrentLobbyCode = null;
        Notify(lobby.Code);
        return LobbyActionResult.Ok(lobby);
    }

    private static string? ValidateSettings(LobbySettings s)
    {
        if (s.MaxPlayers is < 2 or > 8) return "Maximum players must be between 2 and 8.";
        if (s.StartingBankroll < 100) return "Starting bankroll must be at least $100.";
        if (s.MinimumBet < 1) return "Minimum bet must be positive.";
        if (s.MinimumBet > s.StartingBankroll) return "Minimum bet cannot exceed the starting bankroll.";
        if (s.DeckCount is < 1 or > 8) return "Deck count must be between 1 and 8.";
        return null;
    }

    private static LobbySettings CloneSettings(LobbySettings s) => new()
    {
        GameName = s.GameName,
        MaxPlayers = s.MaxPlayers,
        StartingBankroll = s.StartingBankroll,
        MinimumBet = s.MinimumBet,
        DeckCount = s.DeckCount,
        DealerHitsSoft17 = s.DealerHitsSoft17,
        DoubleAfterSplit = s.DoubleAfterSplit,
        SurrenderEnabled = s.SurrenderEnabled,
        InsuranceEnabled = s.InsuranceEnabled
    };

    private static string NormalizeName(string name)
    {
        name = (name ?? "").Trim();
        return string.IsNullOrWhiteSpace(name) ? "Guest" : name[..Math.Min(name.Length, 24)];
    }

    private static string NormalizeCode(string code)
        => new((code ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 6).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
    }

    private void Notify(string code) => LobbyChanged?.Invoke(code);
}
