using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20221025_004)]
public class AddTableMusic_Source_Category : Migration
{
    private string tableName = "music_source_category";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("music_source_id").AsInt32().PrimaryKey().ForeignKey("music_source", "id")
            .WithColumn("category_id").AsInt32().PrimaryKey().ForeignKey("category", "id");

        Create.Index().OnTable(tableName).InSchema("public").OnColumn("category_id");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
