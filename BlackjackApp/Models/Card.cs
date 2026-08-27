namespace BlackjackApp.Models;

public enum Suit { Hearts, Diamonds, Clubs, Spades }
public enum Rank { Two=2, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King, Ace }

public class Card
{
    public Suit Suit { get; set; }
    public Rank Rank { get; set; }

    public int Value => Rank switch
    {
        Rank.Jack or Rank.Queen or Rank.King => 10,
        Rank.Ace => 11,
        _ => (int)Rank
    };

    public string Display => $"{RankSymbol}{SuitSymbol}";

    public string RankSymbol => Rank switch
    {
        Rank.Jack => "J", Rank.Queen => "Q", Rank.King => "K", Rank.Ace => "A",
        _ => ((int)Rank).ToString()
    };

    public string SuitSymbol => Suit switch
    {
        Suit.Hearts => "♥", Suit.Diamonds => "♦", Suit.Clubs => "♣", Suit.Spades => "♠", _ => ""
    };

    public bool IsRed => Suit == Suit.Hearts || Suit == Suit.Diamonds;
}
