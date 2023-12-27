using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20221025_003)]
public class AddTableCategory : Migration
{
    private string tableName = "category";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("name").AsString().NotNullable()
            .WithColumn("type").AsInt32().NotNullable();
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
