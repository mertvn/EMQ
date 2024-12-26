using System;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20241226_000)]
public class AlterTableQuizSongHistory : Migration
{
    // private string tableName = "quiz_song_history";

    public override void Up()
    {
        Execute.Sql(@"
ALTER TABLE quiz_song_history ADD COLUMN guess_kind integer NOT NULL DEFAULT 0;
ALTER TABLE quiz_song_history DROP CONSTRAINT ""PK_quiz_song_history"", ADD CONSTRAINT ""PK_quiz_song_history""  PRIMARY KEY (quiz_id, sp, user_id, guess_kind);
create index idx_quiz_song_history_music_id on quiz_song_history (music_id);
drop index if exists quiz_song_history_played_at_idx;


CREATE TABLE unique_quiz_plays (
    music_id integer NOT NULL,
    quiz_id uuid NOT NULL,
    sp integer NOT NULL,
    user_id integer NOT NULL,
    is_correct boolean NOT NULL,
    is_on_list boolean NOT NULL,
    first_guess_ms integer NOT NULL,
    played_at timestamp NOT NULL,
    CONSTRAINT unique_quiz_plays_pk PRIMARY KEY (quiz_id, sp, user_id)
);

CREATE INDEX idx_unique_quiz_plays_music_id ON unique_quiz_plays(music_id);
CREATE INDEX idx_unique_quiz_plays_user_id ON unique_quiz_plays(user_id);
CREATE INDEX idx_unique_quiz_plays_quiz_id_user_id ON unique_quiz_plays(quiz_id, user_id);
create index idx_unique_quiz_plays_played_at_user_id on unique_quiz_plays (played_at, user_id);

INSERT INTO unique_quiz_plays
SELECT DISTINCT ON (quiz_id, sp, user_id)
    music_id,
    quiz_id,
    sp,
    user_id,
    is_correct,
    is_on_list,
    first_guess_ms,
    played_at
FROM quiz_song_history
ORDER BY quiz_id, sp, user_id, played_at DESC;

CREATE OR REPLACE FUNCTION update_unique_quiz_plays()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        INSERT INTO unique_quiz_plays (
            quiz_id, sp, music_id, user_id,
            is_correct, is_on_list, first_guess_ms, played_at
        )
        VALUES (
            NEW.quiz_id, NEW.sp, NEW.music_id, NEW.user_id,
            NEW.is_correct, NEW.is_on_list, NEW.first_guess_ms, NEW.played_at
        )
        ON CONFLICT (quiz_id, sp, user_id) DO NOTHING; -- non-PK columns are technically inaccurate when this happens but meh
    ELSE
        RAISE 'this table only allows INSERT statements';
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tg_quiz_song_history_update_unique_quiz_plays
AFTER INSERT ON quiz_song_history
FOR EACH ROW EXECUTE FUNCTION update_unique_quiz_plays();
");
    }

    public override void Down() => throw new NotImplementedException();
}
