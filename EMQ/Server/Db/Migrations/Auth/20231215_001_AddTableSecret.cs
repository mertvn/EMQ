using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Auth;

[Tags("AUTH")]
[Migration(20231215_001)]
public class AddTableSecret : Migration
{
    private string tableName = "secret";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("user_id").AsInt32().NotNullable().ForeignKey("users", "id").Unique()
            .WithColumn("ip_created").AsString().NotNullable()
            .WithColumn("ip_last").AsString().NotNullable()
            .WithColumn("token").AsGuid().NotNullable().Unique()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable()
            .WithColumn("last_used_at").AsDateTimeOffset().NotNullable();
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
