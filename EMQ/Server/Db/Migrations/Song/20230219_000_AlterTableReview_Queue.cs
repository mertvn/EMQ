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

        Alter.Table(tableName).AddColumn("sha256").AsString().Nullable(); // todo not null
        Execute.Sql("CREATE INDEX idx_review_queue_sha256 ON review_queue(sha256);");
    }

    public override void Down()
    {
        // todo
    }
}
