using System.Globalization;
using System.Reflection;
using BlackjackApp.Models;
using BlackjackApp.Services;
using Xunit;

namespace BlackjackApp.Tests;

public class GameServiceTests
{
    [Theory]
    [InlineData("Ace,King", "Ten,Nine", 25, "1037.5", PlayerStatus.Won)]
    [InlineData("Ace,Queen", "Ten,Nine", 11, "1016.5", PlayerStatus.Won)]
    [InlineData("Ace,King", "Ace,Queen", 25, "1000", PlayerStatus.Push)]
    [InlineData("Seven,Seven,Seven", "Ace,Queen", 25, "975", PlayerStatus.Lost)]
    [InlineData("Ten,Nine", "Ace,Queen", 25, "975", PlayerStatus.Lost)]
    [InlineData("Ace,King", "Seven,Seven,Seven", 25, "1037.5", PlayerStatus.Won)]
    [InlineData("Ten,Ten", "Ten,Nine", 25, "1025", PlayerStatus.Won)]
    [InlineData("Ten,Eight", "Ten,Nine", 25, "975", PlayerStatus.Lost)]
    [InlineData("Ten,Nine", "Ten,Nine", 25, "1000", PlayerStatus.Push)]
    [InlineData("Ten,King,Two", "Ten,Nine", 25, "975", PlayerStatus.Lost)]
    [InlineData("Ten,King,Two", "Ten,Queen,Two", 25, "975", PlayerStatus.Lost)]
    [InlineData("Ten,Eight", "Ten,Queen,Two", 25, "1025", PlayerStatus.Won)]
    [InlineData("Ten,Eight", "Ten,Nine", 1000, "0", PlayerStatus.Lost)]
    [InlineData("Ten,Eight", "Ten,Nine", 999, "1", PlayerStatus.Lost)]
    public void SettlementAppliesExactPayouts(string hand, string dealerHand, int bet,
        string expectedBalance, PlayerStatus expectedStatus)
    {
        var (service, game) = CreateTable();
        Assert.True(service.StartBetting(game.GameId, "player", out _));
        Assert.True(service.PlaceBet(game.GameId, "player", bet, out _));
        Assert.True(service.PlaceBet(game.GameId, "finisher", 10, out _));

        // Seed known hands after dealing, leaving the final player's public Stand
        // action to drive dealer play, settlement, and DTO mapping.
        var player = game.Players[0];
        player.Chips = 1000m;
        player.Hand = Cards(hand);
        player.Status = player.IsBusted ? PlayerStatus.Busted
            : player.HasBlackjack ? PlayerStatus.Blackjack : PlayerStatus.Standing;
        game.Dealer.Hand = Cards(dealerHand);
        game.Players[1].Hand = Cards("Ten,Nine");
        game.Players[1].Status = PlayerStatus.Playing;
        game.Phase = GamePhase.Playing;
        game.CurrentPlayerIndex = 1;

        Assert.True(service.Stand(game.GameId, "finisher"));
        var result = service.GetGameState(game.GameId)!;
        Assert.Equal("RoundOver", result.Phase);
        Assert.Equal(decimal.Parse(expectedBalance, CultureInfo.InvariantCulture), result.Players[0].Chips);
        Assert.Equal(expectedStatus.ToString(), result.Players[0].Status);
        Assert.False(service.Stand(game.GameId, "finisher"));
        Assert.Equal(result.Players[0].Chips, service.GetGameState(game.GameId)!.Players[0].Chips);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(1001)]
    [InlineData(int.MaxValue)]
    public void InvalidBetsDoNotChangeTheBalance(int amount)
    {
        var (service, game) = CreateTable();
        Assert.True(service.StartBetting(game.GameId, "player", out _));
        Assert.False(service.PlaceBet(game.GameId, "player", amount, out var error));
        Assert.NotEmpty(error);
        Assert.Equal(1000m, game.Players[0].Chips);
        Assert.Equal(0, game.Players[0].CurrentBet);
    }

    [Fact]
    public void BetRequiresMembershipOpenBettingAndOnlyOneSubmission()
    {
        var (service, game) = CreateTable();
        Assert.False(service.PlaceBet(game.GameId, "player", 25, out _));
        Assert.True(service.StartBetting(game.GameId, "player", out _));
        Assert.False(service.PlaceBet(game.GameId, "outsider", 25, out _));
        game.Players[0].Chips = 25.5m;
        Assert.False(service.PlaceBet(game.GameId, "player", 26, out _));
        Assert.True(service.PlaceBet(game.GameId, "player", 25, out _));
        Assert.False(service.PlaceBet(game.GameId, "player", 10, out _));
        Assert.Equal(25, game.Players[0].CurrentBet);
        Assert.Equal(25.5m, game.Players[0].Chips);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.5")]
    [InlineData("9")]
    [InlineData("9.5")]
    public void NewRoundRemovesUnfundedSeatAndTransfersHost(string balance)
    {
        var (service, game) = CreateTable();
        game.Phase = GamePhase.RoundOver;
        game.Players[0].Chips = decimal.Parse(balance, CultureInfo.InvariantCulture);
        Assert.True(service.ResetRound(game.GameId));
        Assert.Single(service.GetGameState(game.GameId)!.Players);
        Assert.Equal("finisher", game.HostConnectionId);
        Assert.Null(service.FindGameForConnection("player"));
        Assert.True(service.TryJoinGame(game.GameId, "friend", "Friend", null, out _));
        Assert.True(service.StartBetting(game.GameId, "finisher", out _));
        Assert.True(service.PlaceBet(game.GameId, "finisher", 10, out _));
        Assert.True(service.PlaceBet(game.GameId, "friend", 10, out _));
        Assert.NotEqual(GamePhase.Betting, game.Phase);
    }

    [Fact]
    public void ExactlyMinimumBalanceRemainsEligible()
    {
        var (service, game) = CreateTable();
        game.Phase = GamePhase.RoundOver;
        game.Players[0].Chips = GameService.MinimumBet;
        Assert.True(service.ResetRound(game.GameId));
        Assert.Equal(2, game.Players.Count);
        Assert.True(service.StartBetting(game.GameId, "player", out _));
        Assert.True(service.PlaceBet(game.GameId, "player", GameService.MinimumBet, out _));
    }

    [Fact]
    public void EmptyUnfundedTableIsDeletedAndFreshTableStartsWith1000()
    {
        var (service, game) = CreateTable();
        game.Phase = GamePhase.RoundOver;
        game.Players.ForEach(p => p.Chips = 9.5m);
        Assert.True(service.ResetRound(game.GameId));
        Assert.Null(service.GetGameState(game.GameId));
        Assert.Null(service.FindGameForConnection("player"));
        Assert.Null(service.FindGameForConnection("finisher"));
        var fresh = service.CreateGame("fresh", "Keely", GameMode.Solo);
        Assert.Equal(1000m, service.GetGameState(fresh)!.Players[0].Chips);
    }

    [Fact]
    public void ResetDuringLiveRoundDoesNotChangeBalancesOrSeats()
    {
        var (service, game) = CreateTable();
        Assert.True(service.StartBetting(game.GameId, "player", out _));
        Assert.False(service.ResetRound(game.GameId));
        Assert.Equal(2, game.Players.Count);
        Assert.All(game.Players, p => Assert.Equal(1000m, p.Chips));
    }

    private static (GameService Service, GameState Game) CreateTable()
    {
        var service = new GameService();
        var id = service.CreateGame("player", "Keely", GameMode.Multiplayer);
        Assert.True(service.TryJoinGame(id, "player", "Keely", null, out _));
        Assert.True(service.TryJoinGame(id, "finisher", "Friend", null, out _));
        // Keep deterministic fixtures out of the production service API.
        var games = (Dictionary<string, GameState>)typeof(GameService)
            .GetField("_games", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        return (service, games[id]);
    }

    private static List<Card> Cards(string ranks) => ranks.Split(',')
        .Select(rank => new Card { Rank = Enum.Parse<Rank>(rank), Suit = Suit.Spades }).ToList();
}
