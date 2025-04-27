namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum PlayerStatus
{
    Default,
    Thinking,
    Guessed,
    Correct, // this means at least one guess type correct, not all
    Wrong,
    Dead, // todo this should be a completely different property on Player
    Looting
}
