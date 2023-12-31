using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Auth;

[Tags("AUTH")]
[Migration(20231231_000)]
public class AddTableUsersQuizSettings : Migration
{
    private string tableName = "users_quiz_settings";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("user_id").AsInt32().NotNullable().ForeignKey("users", "id")
            .WithColumn("name").AsString().NotNullable()
            .WithColumn("b64").AsString().NotNullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable();

        Execute.Sql(
            $@"CREATE UNIQUE INDEX UC_{tableName}_user_id_name ON {tableName}(user_id, name);");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
