using System;
using System.Collections.Generic;
using System.Linq;

namespace Blackjack.Core.Domain;

public sealed class Hand
{
    private readonly List<Card> _cards = new();
    public IReadOnlyList<Card> Cards => _cards.AsReadOnly();

    public void Add(Card c) => _cards.Add(c);

    public int BestValue()
    {
        // treat aces as 11 where possible without busting
        int total = 0;
        int aces = 0;
        foreach (var c in _cards)
        {
            if (c.Rank == Rank.Ace) { aces++; total += 11; }
            else if (c.Rank == Rank.Jack || c.Rank == Rank.Queen || c.Rank == Rank.King) total += 10;
            else total += (int)c.Rank;
        }

        while (total > 21 && aces > 0)
        {
            total -= 10; // count one ace as 1 instead of 11
            aces--;
        }

        return total;
    }

    public bool IsBlackjack() => _cards.Count == 2 && BestValue() == 21 && _cards.Any(c => c.Rank == Rank.Ace);
    public bool IsBusted() => BestValue() > 21;
    public void Clear() => _cards.Clear();
}