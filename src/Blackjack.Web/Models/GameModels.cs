namespace Blackjack.Web.Models;

public enum Suit { Clubs, Diamonds, Hearts, Spades }
public enum Rank { Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King, Ace }
public enum RoundState { Betting, PlayerTurn, DealerTurn, Resolved }

public sealed record Card(Suit Suit, Rank Rank)
{
    public string RankLabel => Rank switch { Rank.Jack => "J", Rank.Queen => "Q", Rank.King => "K", Rank.Ace => "A", _ => ((int)Rank).ToString() };
    public string SuitSymbol => Suit switch { Suit.Clubs => "♣", Suit.Diamonds => "♦", Suit.Hearts => "♥", _ => "♠" };
    public bool IsRed => Suit is Suit.Diamonds or Suit.Hearts;
}

public sealed class BlackjackHand
{
    public List<Card> Cards { get; } = new();
    public int Value
    {
        get
        {
            var value = Cards.Sum(c => c.Rank switch { Rank.Ace => 11, Rank.King or Rank.Queen or Rank.Jack => 10, _ => (int)c.Rank });
            var aces = Cards.Count(c => c.Rank == Rank.Ace);
            while (value > 21 && aces-- > 0) value -= 10;
            return value;
        }
    }
    public bool IsBlackjack => Cards.Count == 2 && Value == 21;
    public bool IsBust => Value > 21;
    public void Clear() => Cards.Clear();
}

public sealed class BlackjackRound
{
    public BlackjackHand PlayerHand { get; } = new();
    public BlackjackHand DealerHand { get; } = new();
    public List<Card> Shoe { get; } = new();
    public RoundState State { get; set; } = RoundState.Betting;
    public int Bet { get; set; }
    public string Message { get; set; } = "Place your bet to begin.";
    public bool DealerHoleCardHidden => State == RoundState.PlayerTurn;
}
