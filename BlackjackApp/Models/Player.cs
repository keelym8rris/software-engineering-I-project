namespace BlackjackApp.Models;

public enum PlayerStatus { Waiting, Playing, Standing, Busted, Blackjack, Won, Lost, Push }

public class Player
{
    public string ConnectionId { get; set; } = "";
    public string Name { get; set; } = "";
    public List<Card> Hand { get; set; } = new();
    public PlayerStatus Status { get; set; } = PlayerStatus.Waiting;
    public int Chips { get; set; } = 1000;
    public int CurrentBet { get; set; } = 0;
    public bool IsDealer { get; set; } = false;

    public int HandValue
    {
        get
        {
            int total = Hand.Sum(c => c.Value);
            int aces = Hand.Count(c => c.Rank == Rank.Ace);
            while (total > 21 && aces > 0) { total -= 10; aces--; }
            return total;
        }
    }

    public bool IsBusted => HandValue > 21;
    public bool HasBlackjack => Hand.Count == 2 && HandValue == 21;
}
