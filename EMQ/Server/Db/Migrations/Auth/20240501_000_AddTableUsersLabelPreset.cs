using EMQ.Shared.Core;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Auth;

[Tags("AUTH")]
[Migration(20240501_000)]
public class AddTableUsersLabelPreset : Migration
{
    private string tableName = "users_label_preset";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("user_id").AsInt32().PrimaryKey() // .ForeignKey("users", "id") // todo FK
            .WithColumn("name").AsString(64).PrimaryKey()
            .WithColumn("is_active").AsBoolean().NotNullable();

        // Users may only have a single preset active
        Execute.Sql(
            $@"CREATE UNIQUE INDEX UC_{tableName}_user_id_is_active ON {tableName}(user_id, is_active) where (is_active);");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
