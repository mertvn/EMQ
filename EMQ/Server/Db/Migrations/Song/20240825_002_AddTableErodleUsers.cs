using System.Data;
using FluentMigrator;

namespace EMQ.Server.Db.Migrations.Song;

[Tags("SONG")]
[Migration(20240825_002)]
public class AddTableErodleUsers : Migration
{
    private string tableName = "erodle_users";

    public override void Up()
    {
        Create.Table(tableName)
            .WithColumn("erodle_id").AsInt32().PrimaryKey().ForeignKey("erodle", "id")
            .WithColumn("user_id").AsInt32().PrimaryKey() // .ForeignKey("users", "id") // todo FK
            .WithColumn("status").AsInt32().NotNullable();
    }

    public override void Down()
    {
        Delete.Table(tableName);
    }
}
