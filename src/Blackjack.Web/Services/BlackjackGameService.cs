using Blackjack.Web.Models;

namespace Blackjack.Web.Services;

public sealed class BlackjackGameService
{
    public LobbyActionResult PlaceBetAndDeal(LobbyState lobby, PlayerSessionService session, int bet)
    {
        lock (lobby.SyncRoot)
        {
            var player = GetPlayer(lobby, session);
            if (player is null) return LobbyActionResult.Fail("Player not found.");
            if (!lobby.GameStarted) return LobbyActionResult.Fail("The game has not started.");
            if (lobby.Round.State is RoundState.PlayerTurn or RoundState.DealerTurn) return LobbyActionResult.Fail("Finish the current round first.");
            if (bet < lobby.Settings.MinimumBet) return LobbyActionResult.Fail($"Minimum bet is ${lobby.Settings.MinimumBet}.");
            if (bet > player.Bankroll) return LobbyActionResult.Fail("You do not have enough bankroll for that bet.");

            PrepareShoe(lobby);
            lobby.Round.PlayerHand.Clear();
            lobby.Round.DealerHand.Clear();
            lobby.Round.Bet = bet;
            lobby.Round.State = RoundState.PlayerTurn;
            lobby.Round.PlayerHand.Cards.Add(Draw(lobby));
            lobby.Round.DealerHand.Cards.Add(Draw(lobby));
            lobby.Round.PlayerHand.Cards.Add(Draw(lobby));
            lobby.Round.DealerHand.Cards.Add(Draw(lobby));

            if (lobby.Round.PlayerHand.IsBlackjack)
            {
                ResolveDealerAndRound(lobby, player);
            }
            else
            {
                lobby.Round.Message = "Your turn. Hit, stand, or double down.";
            }
            return LobbyActionResult.Ok(lobby);
        }
    }

    public LobbyActionResult Hit(LobbyState lobby, PlayerSessionService session)
    {
        lock (lobby.SyncRoot)
        {
            var player = GetPlayer(lobby, session);
            if (player is null) return LobbyActionResult.Fail("Player not found.");
            if (lobby.Round.State != RoundState.PlayerTurn) return LobbyActionResult.Fail("It is not the player's turn.");
            lobby.Round.PlayerHand.Cards.Add(Draw(lobby));
            if (lobby.Round.PlayerHand.IsBust)
            {
                player.Bankroll -= lobby.Round.Bet;
                lobby.Round.State = RoundState.Resolved;
                lobby.Round.Message = $"Bust. You lost ${lobby.Round.Bet}.";
            }
            else if (lobby.Round.PlayerHand.Value == 21)
            {
                ResolveDealerAndRound(lobby, player);
            }
            else
            {
                lobby.Round.Message = $"Hand value: {lobby.Round.PlayerHand.Value}.";
            }
            return LobbyActionResult.Ok(lobby);
        }
    }

    public LobbyActionResult Stand(LobbyState lobby, PlayerSessionService session)
    {
        lock (lobby.SyncRoot)
        {
            var player = GetPlayer(lobby, session);
            if (player is null) return LobbyActionResult.Fail("Player not found.");
            if (lobby.Round.State != RoundState.PlayerTurn) return LobbyActionResult.Fail("It is not the player's turn.");
            ResolveDealerAndRound(lobby, player);
            return LobbyActionResult.Ok(lobby);
        }
    }

    public LobbyActionResult DoubleDown(LobbyState lobby, PlayerSessionService session)
    {
        lock (lobby.SyncRoot)
        {
            var player = GetPlayer(lobby, session);
            if (player is null) return LobbyActionResult.Fail("Player not found.");
            if (lobby.Round.State != RoundState.PlayerTurn) return LobbyActionResult.Fail("It is not the player's turn.");
            if (lobby.Round.PlayerHand.Cards.Count != 2) return LobbyActionResult.Fail("Double down is only available on the first two cards.");
            if (lobby.Round.Bet * 2 > player.Bankroll) return LobbyActionResult.Fail("Not enough bankroll to double down.");
            lobby.Round.Bet *= 2;
            lobby.Round.PlayerHand.Cards.Add(Draw(lobby));
            if (lobby.Round.PlayerHand.IsBust)
            {
                player.Bankroll -= lobby.Round.Bet;
                lobby.Round.State = RoundState.Resolved;
                lobby.Round.Message = $"Bust after doubling. You lost ${lobby.Round.Bet}.";
            }
            else
            {
                ResolveDealerAndRound(lobby, player);
            }
            return LobbyActionResult.Ok(lobby);
        }
    }

    public void ResetRound(LobbyState lobby)
    {
        lock (lobby.SyncRoot)
        {
            lobby.Round.PlayerHand.Clear();
            lobby.Round.DealerHand.Clear();
            lobby.Round.Bet = 0;
            lobby.Round.State = RoundState.Betting;
            lobby.Round.Message = "Place your bet to begin the next round.";
        }
    }

    private static LobbyPlayer? GetPlayer(LobbyState lobby, PlayerSessionService session)
        => lobby.Players.FirstOrDefault(p => p.PlayerId == session.PlayerId);

    private static void PrepareShoe(LobbyState lobby)
    {
        if (lobby.Round.Shoe.Count >= 20) return;
        lobby.Round.Shoe.Clear();
        var cards = new List<Card>();
        for (var d = 0; d < lobby.Settings.DeckCount; d++)
            foreach (var suit in Enum.GetValues<Suit>())
                foreach (var rank in Enum.GetValues<Rank>())
                    cards.Add(new Card(suit, rank));
        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
        lobby.Round.Shoe.AddRange(cards);
    }

    private static Card Draw(LobbyState lobby)
    {
        if (lobby.Round.Shoe.Count == 0) PrepareShoe(lobby);
        var card = lobby.Round.Shoe[^1];
        lobby.Round.Shoe.RemoveAt(lobby.Round.Shoe.Count - 1);
        return card;
    }

    private static void ResolveDealerAndRound(LobbyState lobby, LobbyPlayer player)
    {
        lobby.Round.State = RoundState.DealerTurn;
        while (ShouldDealerHit(lobby)) lobby.Round.DealerHand.Cards.Add(Draw(lobby));

        var playerHand = lobby.Round.PlayerHand;
        var dealerHand = lobby.Round.DealerHand;
        var bet = lobby.Round.Bet;

        if (playerHand.IsBust)
        {
            player.Bankroll -= bet;
            lobby.Round.Message = $"Bust. You lost ${bet}.";
        }
        else if (playerHand.IsBlackjack && !dealerHand.IsBlackjack)
        {
            var win = (int)Math.Round(bet * 1.5m, MidpointRounding.AwayFromZero);
            player.Bankroll += win;
            lobby.Round.Message = $"Blackjack! You won ${win}.";
        }
        else if (dealerHand.IsBust || playerHand.Value > dealerHand.Value)
        {
            player.Bankroll += bet;
            lobby.Round.Message = $"You win ${bet}.";
        }
        else if (playerHand.Value < dealerHand.Value)
        {
            player.Bankroll -= bet;
            lobby.Round.Message = $"Dealer wins. You lost ${bet}.";
        }
        else
        {
            lobby.Round.Message = "Push. Your bet is returned.";
        }

        lobby.Round.State = RoundState.Resolved;
    }

    private static bool ShouldDealerHit(LobbyState lobby)
    {
        var value = lobby.Round.DealerHand.Value;
        if (value < 17) return true;
        if (value > 17) return false;
        if (!lobby.Settings.DealerHitsSoft17) return false;
        var raw = lobby.Round.DealerHand.Cards.Sum(c => c.Rank switch { Rank.Ace => 11, Rank.King or Rank.Queen or Rank.Jack => 10, _ => (int)c.Rank });
        return raw == 17 && lobby.Round.DealerHand.Cards.Any(c => c.Rank == Rank.Ace);
    }
}
