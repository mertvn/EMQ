using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Auth;

[Tags("AUTH")]
[Migration(20231215_000)]
public class AddTableUsers : Migration
{
    private string tableName = "users";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("username").AsString().NotNullable()
            .WithColumn("email").AsString().NotNullable()
            .WithColumn("roles").AsInt32().NotNullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable()
            .WithColumn("salt").AsString().NotNullable()
            .WithColumn("hash").AsString().NotNullable()
            .WithColumn("avatar").AsInt32().NotNullable().WithDefaultValue(Avatar.DefaultAvatar.Character)
            .WithColumn("skin").AsString().NotNullable().WithDefaultValue(Avatar.DefaultAvatar.Skin);

        Execute.Sql(@"CREATE UNIQUE INDEX UC_users_username_lower ON users(lower(username));");
        Execute.Sql($@"ALTER TABLE users ADD CHECK (username ~* '{RegexPatterns.UsernameRegex}');");

        Execute.Sql(@"CREATE UNIQUE INDEX UC_users_email_lower ON users(lower(email));");
        Execute.Sql($@"ALTER TABLE users ADD CHECK (email ~* '{RegexPatterns.EmailRegex}');");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
