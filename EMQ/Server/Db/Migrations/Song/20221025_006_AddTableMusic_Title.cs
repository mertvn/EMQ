using System.Data;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20221025_006)]
public class AddTableMusic_Title : Migration
{
    private string tableName = "music_title";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("music_id").AsInt32().PrimaryKey().ForeignKey("music", "id").OnDelete(Rule.Cascade)
            .WithColumn("latin_title").AsString().PrimaryKey()
            .WithColumn("non_latin_title").AsString().Nullable()
            .WithColumn("language").AsString().PrimaryKey()
            .WithColumn("is_main_title").AsBoolean().NotNullable();
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
