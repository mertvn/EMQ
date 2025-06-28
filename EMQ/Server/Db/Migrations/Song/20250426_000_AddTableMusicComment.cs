using System;
using System.Data;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20252604_000)] // oops
public class AddTableMusicComment : Migration
{
    private string tableName = "music_comment";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("music_id").AsInt32().ForeignKey("music", "id").OnDelete(Rule.Cascade)
            .WithColumn("user_id").AsInt32() // .ForeignKey("users", "id") // todo FK
            .WithColumn("comment").AsString(4096).NotNullable()
            .WithColumn("urls").AsCustom("text[]").NotNullable().WithDefaultValue("{}")
            .WithColumn("kind").AsInt32().NotNullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable();

        Create.Index().OnTable(tableName).InSchema("public").OnColumn("music_id");
        Create.Index().OnTable(tableName).InSchema("public").OnColumn("user_id");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
