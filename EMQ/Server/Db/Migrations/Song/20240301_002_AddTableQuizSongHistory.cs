using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20240301_002)]
public class AddTableQuizSongHistory : Migration
{
    private string tableName = "quiz_song_history";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("quiz_id").AsGuid().PrimaryKey().ForeignKey("quiz", "id")
            .WithColumn("sp").AsInt32().PrimaryKey()
            .WithColumn("music_id").AsInt32().PrimaryKey().ForeignKey("music", "id")
            .WithColumn("user_id").AsInt32().PrimaryKey() // todo FK
            .WithColumn("guess").AsString().NotNullable()
            .WithColumn("first_guess_ms").AsInt32().NotNullable()
            .WithColumn("is_correct").AsBoolean().NotNullable()
            .WithColumn("is_on_list").AsBoolean().NotNullable()
            .WithColumn("played_at").AsDateTime().NotNullable();

        Create.Index().OnTable(tableName).InSchema("public")
            .OnColumn("music_id").Ascending()
            .OnColumn("is_correct").Ascending()
            .OnColumn("first_guess_ms");
        Create.Index().OnTable(tableName).InSchema("public")
            .OnColumn("user_id").Ascending()
            .OnColumn("played_at");

        Alter.Table("music").AddColumn("stat_uniqueusers").AsInt32().NotNullable();
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
