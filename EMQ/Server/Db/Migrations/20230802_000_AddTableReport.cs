using FluentMigrator;

namespace EMQ.Server.Db.Migrations;

[Migration(20230802_000)]
public class AddTableReport : Migration
{
    private string tableName = "report";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("music_id").AsInt32().NotNullable().ForeignKey("music", "id")
            .WithColumn("url").AsString().NotNullable()
            .WithColumn("report_kind").AsInt32().NotNullable()
            .WithColumn("submitted_by").AsString().NotNullable()
            .WithColumn("submitted_on").AsDateTime().NotNullable()
            .WithColumn("status").AsInt32().NotNullable()
            .WithColumn("note_mod").AsString().Nullable()
            .WithColumn("note_user").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
