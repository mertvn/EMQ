using System.Data;
using EMQ.Shared.Core;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Auth;

[Tags("AUTH")]
[Migration(20231227_001)]
public class AddTableUsersLabelVn : Migration
{
    private string tableName = "users_label_vn";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("users_label_id").AsInt64().PrimaryKey().ForeignKey("users_label", "id").OnDelete(Rule.Cascade)
            .WithColumn("vnid").AsString().PrimaryKey().NotNullable()
            .WithColumn("vote").AsInt32().NotNullable();

        Create.Index().OnTable(tableName).InSchema("public").OnColumn("users_label_id");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
