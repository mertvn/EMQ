namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum PlayerStatus
{
    Default,
    Thinking,
    Guessed,
    Correct,
    Wrong,
    Dead, // todo this should be a completely different property on Player
    Looting
}
