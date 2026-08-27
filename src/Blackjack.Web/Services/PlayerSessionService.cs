namespace Blackjack.Web.Services;

public sealed class PlayerSessionService
{
    public Guid PlayerId { get; } = Guid.NewGuid();
    public string DisplayName { get; set; } = "Guest";
    public string? CurrentLobbyCode { get; set; }
}
