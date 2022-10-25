using FluentMigrator;

namespace BlazorApp1.Server.Migrations;

[Migration(20221025_011)]
public class AddTableArtist_Music : Migration
{
    private string tableName = "artist_music";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("artist_alias_id").AsInt32().PrimaryKey().ForeignKey("artist_alias", "id")
            .WithColumn("music_id").AsInt32().PrimaryKey().ForeignKey("music", "id")
            .WithColumn("role").AsInt32().Nullable()
            ;
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
