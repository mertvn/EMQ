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

    public string Guess { get; set; } = "";

    public int Score { get; set; }

    public Avatar? Avatar { get; set; }

    public PlayerState PlayerState { get; set; }

    public int TeamId { get; set; }

    public int Lives { get; set; }

    public bool IsBuffered { get; set; }
}
