using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Auth;

[Tags("AUTH")]
[Migration(20231221_000)]
public class AddTableRatelimited : Migration
{
    private string tableName = "ratelimited";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("ip").AsString().NotNullable()
            .WithColumn("action").AsString().NotNullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable();
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
