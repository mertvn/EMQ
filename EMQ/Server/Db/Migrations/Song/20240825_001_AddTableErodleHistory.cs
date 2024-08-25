using System.Data;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20240825_001)]
public class AddTableErodleHistory : Migration
{
    private string tableName = "erodle_history";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("erodle_id").AsInt32().PrimaryKey().ForeignKey("erodle", "id")
            .WithColumn("user_id").AsInt32().PrimaryKey() // .ForeignKey("users", "id") // todo FK
            .WithColumn("sp").AsInt32().PrimaryKey()
            .WithColumn("guess").AsString().NotNullable();
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
