using System;

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
        string pose = playerState switch
        {
            PlayerState.Default => "default",
            PlayerState.Thinking => "thinking",
            PlayerState.Guessed => "guessed",
            PlayerState.Correct => "correct",
            PlayerState.Wrong => "wrong",
            PlayerState.Dead => "wrong",
            _ => "default"
        };

        if (avatar is null)
        {
            return $"assets/avatars/auu/default/{pose}.jpg".ToLowerInvariant();
        }

        return $"assets/avatars/{avatar.Character.ToString()}/{avatar.Skin}/{pose}.jpg".ToLowerInvariant();
    }
}
