using FluentMigrator;

namespace BlazorApp1.Server.Migrations;

[Migration(20221025_006)]
public class AddTableMusic_Title : Migration
{
    private string tableName = "music_title";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("music_id").AsInt32().PrimaryKey().ForeignKey("music", "id")
            .WithColumn("title").AsString().PrimaryKey()
            .WithColumn("language").AsInt32().PrimaryKey()
            .WithColumn("is_main_title").AsBoolean().NotNullable()
            ;
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
