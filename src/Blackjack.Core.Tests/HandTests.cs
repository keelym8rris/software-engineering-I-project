using Blackjack.Core.Domain;
using Xunit;

namespace Blackjack.Core.Tests;

public class HandTests
{
    [Fact]
    public void AceCountsAsElevenUnlessBusted()
    {
        var h = new Hand();
        h.Add(new Card(Suit.Spades, Rank.Ace));
        h.Add(new Card(Suit.Clubs, Rank.Nine));
        Assert.Equal(20, h.BestValue());
    }

    [Fact]
    public void MultipleAcesHandledCorrectly()
    {
        var h = new Hand();
        h.Add(new Card(Suit.Spades, Rank.Ace));
        h.Add(new Card(Suit.Clubs, Rank.Ace));
        h.Add(new Card(Suit.Diamonds, Rank.Nine));
        Assert.Equal(21, h.BestValue());
    }

    [Fact]
    public void BlackjackDetected()
    {
        var h = new Hand();
        h.Add(new Card(Suit.Spades, Rank.Ace));
        h.Add(new Card(Suit.Clubs, Rank.King));
        Assert.True(h.IsBlackjack());
    }

    [Fact]
    public void BustDetected()
    {
        var h = new Hand();
        h.Add(new Card(Suit.Spades, Rank.King));
        h.Add(new Card(Suit.Clubs, Rank.King));
        h.Add(new Card(Suit.Diamonds, Rank.Two));
        Assert.True(h.IsBusted());
    }
}