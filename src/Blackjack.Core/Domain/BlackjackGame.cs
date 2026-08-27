using System;

namespace Blackjack.Core.Domain;

public enum PlayerAction { Hit, Stand, Double }
public enum GameResult { Pending, PlayerWin, DealerWin, Push, Blackjack }

public sealed class BlackjackGame
{
    public Shoe Shoe { get; }
    public Hand Dealer { get; } = new();
    public Hand Player { get; } = new();
    public bool PlayerTurn { get; private set; } = true;
    public bool RoundActive { get; private set; }

    public BlackjackGame(int decks = 6, Random? rng = null)
    {
        Shoe = new Shoe(decks, rng);
    }

    public void StartRound()
    {
        Dealer.Clear();
        Player.Clear();
        PlayerTurn = true;
        RoundActive = true;
        // deal two cards each: player, dealer, player, dealer
        Player.Add(Shoe.Draw());
        Dealer.Add(Shoe.Draw());
        Player.Add(Shoe.Draw());
        Dealer.Add(Shoe.Draw());
    }

    public void ApplyPlayerAction(PlayerAction action)
    {
        if (!RoundActive || !PlayerTurn) return;
        switch (action)
        {
            case PlayerAction.Hit:
                Player.Add(Shoe.Draw());
                if (Player.IsBusted()) EndRound();
                break;
            case PlayerAction.Double:
                Player.Add(Shoe.Draw());
                // doubling ends player's turn
                PlayerTurn = false;
                DealerPlay();
                EndRound();
                break;
            case PlayerAction.Stand:
                PlayerTurn = false;
                DealerPlay();
                EndRound();
                break;
        }
    }

    private void DealerPlay()
    {
        // simple dealer: hit until 17 or more
        while (Dealer.BestValue() < 17)
        {
            Dealer.Add(Shoe.Draw());
        }
    }

    private void EndRound()
    {
        RoundActive = false;
    }

    public GameResult Resolve()
    {
        if (RoundActive) throw new InvalidOperationException("Round still active");
        if (Player.IsBusted()) return GameResult.DealerWin;
        if (Player.IsBlackjack() && !Dealer.IsBlackjack()) return GameResult.Blackjack;
        if (Dealer.IsBusted()) return GameResult.PlayerWin;
        var p = Player.BestValue();
        var d = Dealer.BestValue();
        if (p > d) return GameResult.PlayerWin;
        if (p < d) return GameResult.DealerWin;
        return GameResult.Push;
    }
}