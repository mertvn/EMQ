using FluentMigrator;

namespace EMQ.Server.Db.Migrations;

[Migration(20221025_005)]
public class AddTableMusic : Migration
{
    private string tableName = "music";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("type").AsInt32().NotNullable()
            ;
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
