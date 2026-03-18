using System.Data;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Auth;

[Tags("AUTH")]
[Migration(20260318_001)]
public class AddTableUnregisterQueue : Migration
{
    private string tableName = "unregister_queue";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("user_id").AsInt32().NotNullable().ForeignKey("users", "id").OnDelete(Rule.Cascade).Unique()
            .WithColumn("token").AsString().NotNullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable();
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
