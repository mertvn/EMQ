using FluentMigrator;

namespace BlazorApp1.Server.Migrations;

[Migration(20221025_010)]
public class AddTableArtist_Alias : Migration
{
    private string tableName = "artist_alias";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("artist_id").AsInt32().ForeignKey("artist", "id")
            .WithColumn("latin_alias").AsString()
            .WithColumn("non_latin_alias").AsString();

        Create.UniqueConstraint()
            .OnTable(tableName)
            .Columns("artist_id", "latin_alias");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
