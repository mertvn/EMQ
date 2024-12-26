using System;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SHSongStats
{
    public int MusicId { get; set; }

    public int UserId { get; set; }

    public bool IsCorrect { get; set; }

    public int FirstGuessMs { get; set; }

    public string Guess { get; set; } = "";

    public DateTime PlayedAt { get; set; }

    public int RowNumber { get; set; }

    public string Username { get; set; } = "";

    public GuessKind GuessKind { get; set; }
}
