using FluentMigrator;

namespace EMQ.Server.Db.Migrations;

[Migration(20221216_000)]
public class AddTableReview_Queue : Migration
{
    private string tableName = "review_queue";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("music_id").AsInt32().NotNullable().ForeignKey("music", "id")
            .WithColumn("url").AsString().NotNullable()
            .WithColumn("type").AsInt32().NotNullable()
            .WithColumn("is_video").AsBoolean().NotNullable()
            .WithColumn("submitted_by").AsString().NotNullable()
            .WithColumn("submitted_on").AsDateTime().NotNullable()
            .WithColumn("status").AsInt32().NotNullable()
            .WithColumn("reason").AsString().Nullable()
            .WithColumn("analysis").AsString().Nullable()
            .WithColumn("duration").AsTime().Nullable();
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
