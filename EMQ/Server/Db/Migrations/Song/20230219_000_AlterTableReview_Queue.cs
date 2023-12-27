using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20230219_000)]
public class AlterTableReview_Queue : Migration
{
    private string tableName = "review_queue";

    public override void Up()
    {
        Create.UniqueConstraint()
            .OnTable(tableName)
            .Columns("music_id", "url");
    }

    public override void Down()
    {
        // todo
    }
}
