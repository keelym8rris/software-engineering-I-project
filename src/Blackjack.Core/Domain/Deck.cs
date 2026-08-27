using System;
using System.Collections.Generic;
using System.Linq;

namespace Blackjack.Core.Domain;

public sealed class Deck
{
    private readonly List<Card> _cards;

    public Deck()
    {
        _cards = new List<Card>();
        foreach (Suit s in Enum.GetValues(typeof(Suit)))
        {
            // ranks: 2-10, J Q K as 10, Ace as Ace
            for (int v = 2; v <= 10; v++) _cards.Add(new Card(s, (Rank)v));
            _cards.Add(new Card(s, Rank.Jack));
            _cards.Add(new Card(s, Rank.Queen));
            _cards.Add(new Card(s, Rank.King));
            _cards.Add(new Card(s, Rank.Ace));
        }
    }

    public IReadOnlyList<Card> Cards => _cards.AsReadOnly();

    public void Shuffle(Random? rng = null)
    {
        rng ??= Random.Shared;
        for (int i = _cards.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var t = _cards[i];
            _cards[i] = _cards[j];
            _cards[j] = t;
        }
    }

    public Card DrawTop()
    {
        if (_cards.Count == 0) throw new InvalidOperationException("Deck is empty");
        var c = _cards[0];
        _cards.RemoveAt(0);
        return c;
    }

    public int Count => _cards.Count;
}