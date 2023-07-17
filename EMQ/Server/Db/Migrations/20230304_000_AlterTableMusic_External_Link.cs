using FluentMigrator;

namespace EMQ.Server.Db.Migrations;

[Migration(20230304_000)]
public class AlterTableMusic_External_Link : Migration
{
    private string tableName = "music_external_link";

    public override void Up()
    {
        Alter.Table(tableName)
            .AddColumn("duration").AsTime().NotNullable();
        Alter.Table(tableName)
            .AddColumn("submitted_by").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column("duration").FromTable(tableName);
        Delete.Column("submitted_by").FromTable(tableName);
    }
}
