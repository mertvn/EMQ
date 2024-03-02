using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("quiz_song_history")]
public class QuizSongHistory
{
    [Key]
    public Guid quiz_id { get; set; }

    [Key]
    public int sp { get; set; }

    [Key]
    public int music_id { get; set; }

    [Key]
    public int user_id { get; set; }

    public string guess { get; set; } = "";

    public int first_guess_ms { get; set; }

    public bool is_correct { get; set; }

    public bool is_on_list { get; set; }

    public DateTime played_at { get; set; }
}
