using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Auth;

[Tags("AUTH")]
[Migration(20260210_000)]
public class AddTableRegistrationCode : Migration
{
    private string tableName = "registration_code";

    public override void Up()
    {
        Create.Table(tableName).WithColumn("code").AsString().PrimaryKey();
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
