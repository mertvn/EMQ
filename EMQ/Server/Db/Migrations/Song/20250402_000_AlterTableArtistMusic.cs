using System;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20250402_000)]
public class AlterTableArtistMusic : Migration
{
    public override void Up()
    {
        Execute.Sql(@"CREATE OR REPLACE FUNCTION check_consistent_artist_alias() RETURNS TRIGGER AS $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM public.artist_music
    WHERE artist_id = NEW.artist_id
    AND music_id = NEW.music_id
    AND artist_alias_id != NEW.artist_alias_id
  ) THEN
    RAISE EXCEPTION 'Inconsistent artist_alias_id for same artist_id and music_id';
  END IF;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER enforce_consistent_artist_alias
BEFORE INSERT OR UPDATE ON public.artist_music
FOR EACH ROW EXECUTE FUNCTION check_consistent_artist_alias();");
    }

    public override void Down() => throw new NotImplementedException();
}
