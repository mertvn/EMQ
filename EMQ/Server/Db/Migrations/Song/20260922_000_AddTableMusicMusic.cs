using System.Data;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20260922_000)]
public class AddTableMusicMusic : Migration
{
    private string tableName = "music_music";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("source").AsInt32().PrimaryKey().ForeignKey("music", "id").OnDelete(Rule.Cascade)
            .WithColumn("target").AsInt32().PrimaryKey().ForeignKey("music", "id").OnDelete(Rule.Cascade)
            .WithColumn("rel").AsInt32().PrimaryKey();

        Execute.Sql("ALTER TABLE music_music ADD CHECK (source != target);");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
