using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20240307_000)]
public class AddTableUserSpacedRepetition : Migration
{
    private string tableName = "user_spaced_repetition";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("user_id").AsInt32().PrimaryKey() // todo FK
            .WithColumn("music_id").AsInt32().PrimaryKey().ForeignKey("music", "id")
            .WithColumn("n").AsInt32().NotNullable()
            .WithColumn("ease").AsFloat().NotNullable()
            .WithColumn("interval_days").AsFloat().NotNullable()
            .WithColumn("reviewed_at").AsDateTimeOffset().NotNullable()
            .WithColumn("due_at").AsDateTimeOffset().NotNullable();

        Create.Index().OnTable(tableName).InSchema("public").OnColumn("user_id").Ascending().OnColumn("due_at");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
