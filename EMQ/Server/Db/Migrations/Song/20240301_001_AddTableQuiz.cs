using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20240301_001)]
public class AddTableQuiz : Migration
{
    private string tableName = "quiz";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("room_id").AsGuid().NotNullable().ForeignKey("room", "id")
            .WithColumn("settings_b64").AsString().NotNullable()
            .WithColumn("should_update_stats").AsBoolean().NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable();

        Create.Index().OnTable(tableName).InSchema("public").OnColumn("room_id");
        Create.Index().OnTable(tableName).InSchema("public").OnColumn("should_update_stats");
        Create.Index().OnTable(tableName).InSchema("public").OnColumn("created_at");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
