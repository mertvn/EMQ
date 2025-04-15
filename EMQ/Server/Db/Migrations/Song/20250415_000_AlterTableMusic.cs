using System;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20250415_000)]
public class AlterTableMusic : Migration
{
    public override void Up()
    {
        Execute.Sql(@"INSERT INTO music_external_link (music_id, url, type, is_video, duration)
SELECT
    id,
    'https://musicbrainz.org/recording/' || musicbrainz_recording_gid::text,
    3,
    false,
    '00:00:00'
FROM music
WHERE musicbrainz_recording_gid IS NOT NULL
ON CONFLICT (music_id, url) DO NOTHING;

ALTER TABLE music DROP COLUMN musicbrainz_recording_gid;
");
    }

    public override void Down() => throw new NotImplementedException();
}
