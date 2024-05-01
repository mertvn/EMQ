using EMQ.Shared.Core;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Auth;

[Tags("AUTH")]
[Migration(20231227_000)]
public class AddTableUsersLabel : Migration
{
    private string tableName = "users_label";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("user_id").AsInt32().NotNullable() // .ForeignKey("users", "id") // todo FK
            .WithColumn("vndb_uid").AsString().NotNullable()
            .WithColumn("vndb_label_id").AsInt32().NotNullable()
            .WithColumn("vndb_label_name").AsString().NotNullable()
            .WithColumn("vndb_label_is_private").AsString().NotNullable()
            .WithColumn("kind").AsInt32().NotNullable()
            .WithColumn("preset_name").AsString(64).NotNullable();

        // multiple EMQ users can use the same VNDB account, and one user can use multiple VNDB accounts using presets
        Execute.Sql(
            $@"CREATE UNIQUE INDEX UC_{tableName}_user_id_vndbuid_vndblabelid_presetname ON {tableName}(user_id, vndb_uid, vndb_label_id, preset_name);");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
