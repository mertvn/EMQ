using System;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20251112_000)]
public class AlterTableArtistExternalLinkDropConstraint : Migration
{
    public override void Up()
    {
        Execute.Sql(
            "ALTER TABLE artist_external_link DROP CONSTRAINT IF EXISTS \"UC_artist_external_link_artist_id_type\"");
    }

    public override void Down() => throw new NotImplementedException();
}
