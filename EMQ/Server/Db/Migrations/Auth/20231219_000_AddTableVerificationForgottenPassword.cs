using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Auth;

[Tags("AUTH")]
[Migration(20231219_000)]
public class AddTableVerificationForgottenPassword : Migration
{
    private string tableName = "verification_forgottenpassword";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("user_id").AsInt32().NotNullable().ForeignKey("users", "id").Unique()
            .WithColumn("token").AsString().NotNullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable();
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
