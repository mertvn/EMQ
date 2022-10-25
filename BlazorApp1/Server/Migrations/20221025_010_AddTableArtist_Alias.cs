using FluentMigrator;

namespace BlazorApp1.Server.Migrations;

[Migration(20221025_010)]
public class AddTableMusic_Artist_Alias : Migration
{
    private string tableName = "artist_alias";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("music_id").AsInt32().PrimaryKey().ForeignKey("artist", "id")
            .WithColumn("alias").AsString().PrimaryKey()
            ;
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
