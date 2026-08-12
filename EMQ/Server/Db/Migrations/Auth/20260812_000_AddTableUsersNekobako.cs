using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Auth;

[Tags("AUTH")]
[Migration(20260812_000)]
public class AddTableUsersNekobako : Migration
{
    private string tableName = "users_nekobako";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("extension").AsString(16).NotNullable()
            .WithColumn("user_id").AsInt32().ForeignKey("users", "id")
            .WithColumn("size_bytes").AsInt64().NotNullable()
            .WithColumn("sha256").AsString(64).NotNullable()
            .WithColumn("orig_name").AsString(256).NotNullable()
            .WithColumn("uploaded_at").AsDateTimeOffset().NotNullable();

        Execute.Sql($@"CREATE UNIQUE INDEX UC_{tableName}_user_id_sha256 ON {tableName}(user_id, sha256);");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
