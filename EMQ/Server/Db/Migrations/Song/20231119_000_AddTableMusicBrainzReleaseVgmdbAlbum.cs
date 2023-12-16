using FluentMigrator;

namespace EMQ.Server.Db.Migrations;

[Tags("SONG")]
[Migration(20231119_000)]
public class AddTableMusicBrainzReleaseVgmdbAlbum : Migration
{
    private string tableName = "musicbrainz_release_vgmdb_album";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("release").AsGuid().NotNullable()
            .WithColumn("album_id").AsInt32().NotNullable();

        Create.UniqueConstraint()
            .OnTable(tableName)
            .Columns("release", "album_id");

        Create.Index().OnTable(tableName).InSchema("public").OnColumn("album_id");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
