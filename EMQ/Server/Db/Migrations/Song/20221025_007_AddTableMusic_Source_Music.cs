using System.Data;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20221025_007)]
public class AddTableMusic_Source_Music : Migration
{
    private string tableName = "music_source_music";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("music_source_id").AsInt32().PrimaryKey().ForeignKey("music_source", "id") // .OnDelete(Rule.Cascade)
            .WithColumn("music_id").AsInt32().PrimaryKey().ForeignKey("music", "id").OnDelete(Rule.Cascade)
            .WithColumn("type").AsInt32().PrimaryKey().NotNullable();

        Create.Index().OnTable(tableName).InSchema("public")
            .OnColumn("music_id").Ascending()
            .OnColumn("music_source_id").Ascending()
            .OnColumn("type").Ascending();
        Create.Index().OnTable(tableName).InSchema("public").OnColumn("type");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
