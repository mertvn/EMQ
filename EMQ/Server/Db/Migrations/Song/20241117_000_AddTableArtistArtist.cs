using System.Data;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20241117_000)]
public class AddTableArtistArtist : Migration
{
    private string tableName = "artist_artist";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("source").AsInt32().PrimaryKey().ForeignKey("artist", "id").OnDelete(Rule.Cascade)
            .WithColumn("target").AsInt32().PrimaryKey().ForeignKey("artist", "id").OnDelete(Rule.Cascade)
            .WithColumn("rel").AsInt32().PrimaryKey();

        Execute.Sql("ALTER TABLE artist_artist ADD CHECK (source != target);");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
