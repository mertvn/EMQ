namespace EMQ.Shared.Erodle.Entities.Concrete;

public class ErodlePlayerInfo
{
    public int UserId { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    public int Plays { get; set; }

    public int Guesses { get; set; }

    public float AvgGuesses { get; set; }

    public string Username { get; set; } = "";
}
