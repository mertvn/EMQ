using System.Data;
using EMQ.Shared.Quiz.Entities.Concrete;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20221025_002)]
public class AddTableMusic_Source_External_Link : Migration
{
    private string tableName = "music_source_external_link";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("music_source_id").AsInt32().PrimaryKey().ForeignKey("music_source", "id").OnDelete(Rule.Cascade)
            .WithColumn("url").AsString().PrimaryKey()
            .WithColumn("type").AsInt32().NotNullable()
            .WithColumn("name").AsString().NotNullable();

        Execute.Sql($@"CREATE UNIQUE INDEX UC_{tableName}_url ON {tableName}(url) where (type != {(int)SongSourceLinkType.MusicBrainzRelease} AND type != {(int)SongSourceLinkType.VGMdbAlbum})");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
