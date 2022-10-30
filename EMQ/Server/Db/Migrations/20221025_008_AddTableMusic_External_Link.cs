using FluentMigrator;

namespace EMQ.Server.Db.Migrations;

[Migration(20221025_008)]
public class AddTableMusic_External_Link : Migration
{
    private string tableName = "music_external_link";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("music_id").AsInt32().PrimaryKey().ForeignKey("music", "id")
            .WithColumn("url").AsString().PrimaryKey()
            .WithColumn("type").AsInt32().NotNullable()
            .WithColumn("is_video").AsBoolean().NotNullable();
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
