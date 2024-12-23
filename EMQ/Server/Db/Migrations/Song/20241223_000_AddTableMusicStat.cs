using System;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20241223_000)]
public class AddTableMusicStat : Migration
{
    // private string tableName = "music_stat";

    public override void Up()
    {
        Execute.Sql(@"CREATE TABLE music_stat
(
    music_id                  integer
        constraint ""FK_music_stat_music""
            references music (id),
    guess_kind                integer not null,
    stat_correct              bigint  default 0 not null,
    stat_played               bigint  default 0 not null,
    stat_guessed              bigint  default 0 not null,
    stat_totalguessms         bigint  default 0 not null,
    stat_correctpercentage    real generated always as ((((1.0 * (stat_correct)::numeric) / (COALESCE(NULLIF(stat_played, 0), (1)::bigint))::numeric) * (100)::numeric)) stored,
    stat_averageguessms       integer generated always as ((stat_totalguessms / COALESCE(NULLIF(stat_guessed, 0), (1)::bigint))) stored,
    stat_uniqueusers          integer default 0 not null,
    constraint ""PK_music_stat""
        primary key (music_id, guess_kind),
    constraint check_stats
        check ((stat_correct <= stat_played) AND (stat_guessed <= stat_played))
);

-- Copy data from music table to music_stat
INSERT INTO music_stat (
    music_id,
    guess_kind,
    stat_correct,
    stat_played,
    stat_guessed,
    stat_totalguessms,
    stat_uniqueusers
)
SELECT
    id,
    0,
    stat_correct,
    stat_played,
    stat_guessed,
    stat_totalguessms,
    stat_uniqueusers
FROM music;

-- Drop statistics columns from music table
ALTER TABLE music
    DROP COLUMN stat_correctpercentage,
    DROP COLUMN stat_averageguessms,
    DROP COLUMN stat_correct,
    DROP COLUMN stat_played,
    DROP COLUMN stat_guessed,
    DROP COLUMN stat_totalguessms,
    DROP COLUMN stat_uniqueusers;");
    }

    public override void Down() => throw new NotImplementedException();
}
