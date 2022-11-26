namespace EMQ.Shared.Quiz.Entities.Concrete;

public class Avatar
{
    public Avatar(AvatarCharacter character, string skin = "default")
    {
        Character = character;
        Skin = skin;
    }

    public AvatarCharacter Character { get; }

    public string Skin { get; }

    public static string GetUrlByPlayerState(Avatar? avatar, PlayerState playerState)
    {
        if (avatar is null)
        {
            return $"assets/avatars/auu/default/{playerState.ToString()}";
        }

        return $"assets/avatars/{avatar.Character.ToString()}/{avatar.Skin}/{playerState.ToString()}.jpg";
    }
}
