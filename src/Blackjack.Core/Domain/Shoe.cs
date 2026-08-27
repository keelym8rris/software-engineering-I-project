using System;
using System.Collections.Generic;

namespace Blackjack.Core.Domain;

public sealed class Shoe
{
    private readonly Queue<Card> _cards;
    public Shoe(int decks = 6, Random? rng = null)
    {
        if (decks <= 0) throw new ArgumentOutOfRangeException(nameof(decks));
        var list = new List<Card>();
        for (int i = 0; i < decks; i++)
        {
            var d = new Deck();
            list.AddRange(d.Cards);
        }
        rng ??= Random.Shared;
        // Fisher-Yates shuffle
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var t = list[i]; list[i] = list[j]; list[j] = t;
        }
        _cards = new Queue<Card>(list);
    }

    public Card Draw() => _cards.Dequeue();
    public int Remaining => _cards.Count;
}