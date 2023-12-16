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
            .WithColumn("user_id").AsInt32().NotNullable().ForeignKey("users", "id")
            .WithColumn("token").AsGuid().NotNullable()
            .WithColumn("created_at").AsDateTime2().NotNullable()
            .WithColumn("last_used_at").AsDateTime2().NotNullable();

        Create.UniqueConstraint()
            .OnTable(tableName)
            .Columns("token");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
