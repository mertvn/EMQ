using System.Data;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20221025_008)]
public class AddTableMusic_External_Link : Migration
{
    private string tableName = "music_external_link";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("music_id").AsInt32().PrimaryKey().ForeignKey("music", "id").OnDelete(Rule.Cascade)
            .WithColumn("url").AsString().PrimaryKey()
            .WithColumn("type").AsInt32().NotNullable()
            .WithColumn("is_video").AsBoolean().NotNullable()
            .WithColumn("analysis_raw").AsString().Nullable();

        Create.Index().OnTable(tableName).InSchema("public").OnColumn("url");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
