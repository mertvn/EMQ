using System.Data;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20240711_000)]
public class AddTableMusicVote : Migration
{
    private string tableName = "music_vote";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("music_id").AsInt32().PrimaryKey().ForeignKey("music", "id").OnDelete(Rule.Cascade)
            .WithColumn("user_id").AsInt32().PrimaryKey() // .ForeignKey("users", "id") // todo FK
            .WithColumn("vote").AsInt16().NotNullable()
            .WithColumn("updated_at").AsDateTimeOffset().NotNullable();

        Create.Index().OnTable(tableName).InSchema("public").OnColumn("user_id");

        Execute.Sql(
            @"ALTER TABLE music_vote ADD CONSTRAINT check_vote CHECK ( vote >= 10 AND vote <= 100 );");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
