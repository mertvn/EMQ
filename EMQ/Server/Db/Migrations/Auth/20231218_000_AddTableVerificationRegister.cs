using EMQ.Shared.Core;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Auth;

[Tags("AUTH")]
[Migration(20231218_000)]
public class AddTableVerificationRegister : Migration
{
    private string tableName = "verification_register";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("username").AsString().NotNullable()
            .WithColumn("email").AsString().NotNullable()
            .WithColumn("token").AsString().NotNullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable();

        Execute.Sql(
            $@"CREATE UNIQUE INDEX UC_{tableName}_username_lower ON {tableName}(lower(username));");
        Execute.Sql($@"ALTER TABLE {tableName} ADD CHECK (username ~* '{RegexPatterns.UsernameRegex}');");

        Execute.Sql(
            $@"CREATE UNIQUE INDEX UC_{tableName}_email_lower ON {tableName}(lower(email));");
        Execute.Sql($@"ALTER TABLE {tableName} ADD CHECK (email ~* '{RegexPatterns.EmailRegex}');");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
