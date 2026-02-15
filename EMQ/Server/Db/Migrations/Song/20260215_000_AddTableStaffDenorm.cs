using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20260215_000)]
public class AddTableStaffDenorm : Migration
{
    private string tableName = "staff_denorm";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("vid").AsString().NotNullable()
            .WithColumn("sid").AsString().NotNullable()
            .WithColumn("alias_id").AsString().NotNullable()
            .WithColumn("detail_id").AsString().NotNullable()
            .WithColumn("latin").AsString().Nullable()
            .WithColumn("name").AsString().NotNullable()
            .WithColumn("role").AsInt32().NotNullable()
            .WithColumn("role_detail").AsString().Nullable();

        Create.Index().OnTable(tableName).InSchema("public").OnColumn("vid");
        Create.Index().OnTable(tableName).InSchema("public").OnColumn("sid");
        Create.Index().OnTable(tableName).InSchema("public").OnColumn("detail_id");
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
