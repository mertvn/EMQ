using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db.Entities;

[Table("quiz_song_history")]
public class QuizSongHistory
{
    [Key]
    public Guid quiz_id { get; set; }

    [Key]
    public int sp { get; set; }

    public int music_id { get; set; }

    [Key]
    public int user_id { get; set; }

    [Key]
    public GuessKind guess_kind { get; set; }

    public string guess { get; set; } = "";

    public int first_guess_ms { get; set; }

    public bool is_correct { get; set; }

    public bool is_on_list { get; set; }

    public DateTime played_at { get; set; }

    public TimeSpan? start_time { get; set; }

    public TimeSpan? duration { get; set; }
}
