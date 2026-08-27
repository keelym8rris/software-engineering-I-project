namespace Blackjack.Core.Domain;

public enum Suit { Clubs, Diamonds, Hearts, Spades }
public enum Rank
{
    Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten,
    Jack = 10, Queen = 10, King = 10,
    Ace = 11
}

public sealed record Card(Suit Suit, Rank Rank)
{
    public override string ToString() => $"{Rank} of {Suit}";
}