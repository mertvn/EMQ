namespace EMQ.Shared.Quiz.Entities.Concrete;

public class Player
{
    public Player(int id, string username)
    {
        Id = id;
        Username = username;
    }

    public int Id { get; }

    public string Username { get; }

    // public string DisplayName { get; }

    public string? Guess { get; set; }

    // todo: might want to keep this within Songs instead
    public bool? IsCorrect { get; set; } // todo maybe get rid of this and use PlayerState instead

    public int Score { get; set; }

    public Avatar? Avatar { get; set; }

    public PlayerState PlayerState { get; set; }
}
